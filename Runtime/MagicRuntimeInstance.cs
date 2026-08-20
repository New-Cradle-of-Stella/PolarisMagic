using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using nel;
using Polaris.API;
using Polaris.Magic.Definitions;

namespace Polaris.Magic.Runtime
{
    /// <summary>
    /// 一次施法的中间层实例：拥有时钟、同步上下文、等待表、取消源、Context 和作者 Task。
    ///
    /// 生命周期只有一条主线：
    /// <list type="number">
    ///   <item>正式魔法实体建立时 <see cref="Start"/> 一次，且仅一次调用作者回调。</item>
    ///   <item>holder 每 Tick 调 <see cref="Tick"/>：推进时钟、完成到期等待、在主线程执行续体。</item>
    ///   <item>Task 未完成时 <see cref="Tick"/> 返回 true，原版实体继续存活。</item>
    ///   <item>Task 完成、取消或异常时返回 false，原版实体立即结束。</item>
    ///   <item>外部结束时 <see cref="RequestCancel"/> 取消 Token；取消泵继续 Tick 驱动 <c>finally</c>，
    ///         完成后 <see cref="Release"/>。</item>
    /// </list>
    /// </summary>
    internal sealed class MagicRuntimeInstance
    {
        private readonly object waitGate = new object();
        private readonly List<MagicWaiter> waiters = new List<MagicWaiter>();
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private readonly MagicClock clock = new MagicClock();
        private readonly MagicSynchronizationContext syncContext;
        private readonly MagicApi api;
        private readonly MagicRuntimeContext context;
        private readonly MagicItem item;
        private readonly int itemId;

        private Task rootTask;
        private bool outcomeReported;
        private bool released;
        private string cancelReason;

        internal MagicRuntimeInstance(MagicDefinition definition, MagicItem item, int instanceId, int mainThreadId)
        {
            Definition = definition;
            InstanceId = instanceId;
            this.item = item;
            itemId = item.id;

            syncContext = new MagicSynchronizationContext(mainThreadId);
            string label = definition.Id + "/" + instanceId.ToString(CultureInfo.InvariantCulture);
            api = new MagicApi(definition.ProviderAssembly, label);

            context = new MagicRuntimeContext(
                this,
                definition,
                instanceId,
                clock,
                new MagicEntity(item),
                GameCharacter.Wrap(item.Caster as m2d.M2Attackable),
                new MagicWorldServices(instanceId),
                api);
        }

        internal MagicDefinition Definition { get; }

        internal int InstanceId { get; }

        internal bool IsStarted => rootTask != null;

        /// <summary>作者 Task 是否已经走完（含取消与异常）。</summary>
        internal bool IsFinished => rootTask != null && rootTask.IsCompleted;

        /// <summary>绑定的原版实体是否还是这一次施法的那一个。</summary>
        internal bool OwnsItem(MagicItem candidate) => ReferenceEquals(candidate, item) && candidate.id == itemId;

        /// <summary>
        /// 调用作者回调，且只调用一次。回调同步抛出的异常与 Task 内抛出的异常同等处理：
        /// 都只结束这一个实例。
        /// </summary>
        internal void Start()
        {
            if (rootTask != null)
            {
                throw new InvalidOperationException(
                    "RunAsync must be invoked exactly once per magic instance; instance " + InstanceId + " was started twice.");
            }

            MagicTaskCallback callback;
            try
            {
                callback = Definition.CreateCallback();
            }
            catch (Exception ex)
            {
                rootTask = FromException(ex);
                return;
            }

            SynchronizationContext previous = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(syncContext);
            try
            {
                rootTask = callback(context, cancellation.Token) ?? Task.CompletedTask;
            }
            catch (Exception ex)
            {
                rootTask = FromException(ex);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previous);
            }
        }

        /// <summary>推进一个 Tick。返回 false 表示这次施法已经结束，原版实体可以回收。</summary>
        internal bool Tick(float deltaFrames)
        {
            if (rootTask == null)
            {
                return false;
            }

            clock.Advance(deltaFrames);

            SynchronizationContext previous = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(syncContext);
            try
            {
                CompleteDueWaiters();
                syncContext.Drain(ReportLooseException);
                api.Tick(clock);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previous);
            }

            if (!rootTask.IsCompleted)
            {
                return true;
            }

            ReportOutcomeOnce();
            return false;
        }

        /// <summary>请求取消。幂等；重复调用只保留第一个原因。</summary>
        internal void RequestCancel(string reason)
        {
            if (cancellation.IsCancellationRequested)
            {
                return;
            }

            cancelReason = reason;
            try
            {
                cancellation.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // 已经释放过；取消这件事已经没有意义了。
            }
        }

        internal bool IsCancellationRequested => cancellation.IsCancellationRequested;

        /// <summary>
        /// 最终清理。只在 Task 已经走完之后调用：把剩余等待全部取消、丢弃排队续体、
        /// 释放这次施法创建的全部对象与资源租约。幂等。
        ///
        /// 无论 Task 是正常完成、被取消还是抛了异常，走到这里的含义都一样：这次施法结束了。
        /// 因此这里也一定会取消 Token——正常完成那条路径上作者并没有取消过它，而作者可能已经把它
        /// 交给了别的东西（后台任务、外部注册），不取消就等于留下一个永远不会被通知的引用。
        /// </summary>
        internal void Release()
        {
            if (released)
            {
                return;
            }

            released = true;

            RequestCancel("the magic task ended");
            ReportOutcomeOnce();
            CancelAllWaiters();
            syncContext.Clear();

            try
            {
                api.Close();
            }
            catch (Exception ex)
            {
                PolarisAPI.Errors.Report(ex, "releasing magic " + Definition.Id, Definition.ProviderAssembly);
            }

            cancellation.Dispose();
        }

        // ==================== 等待 ====================

        internal ValueTask Wait(MagicWaitKind kind, float targetFrames, Func<bool> predicate, CancellationToken callerToken)
        {
            if (cancellation.IsCancellationRequested)
            {
                return new ValueTask(Task.FromCanceled(cancellation.Token));
            }

            if (callerToken.IsCancellationRequested)
            {
                return new ValueTask(Task.FromCanceled(callerToken));
            }

            var waiter = new MagicWaiter(kind, targetFrames, predicate, callerToken);

            // 正常路径上等待都在主线程注册（续体总被同步上下文接回主线程），但作者用了
            // ConfigureAwait(false) 之后就可能从线程池线程进来，所以这里仍然加锁。
            lock (waitGate)
            {
                waiters.Add(waiter);
            }

            return waiter.Task;
        }

        private void CompleteDueWaiters()
        {
            bool cancelled = cancellation.IsCancellationRequested;

            List<MagicWaiter> resolved = null;
            lock (waitGate)
            {
                for (int i = waiters.Count - 1; i >= 0; i--)
                {
                    MagicWaiter waiter = waiters[i];
                    bool take = cancelled || waiter.CallerToken.IsCancellationRequested;

                    if (!take)
                    {
                        try
                        {
                            take = waiter.IsDue(clock);
                        }
                        catch (Exception ex)
                        {
                            waiters.RemoveAt(i);
                            waiter.Fault(ex);
                            continue;
                        }
                    }

                    if (!take)
                    {
                        continue;
                    }

                    waiters.RemoveAt(i);
                    (resolved ?? (resolved = new List<MagicWaiter>())).Add(waiter);
                }
            }

            if (resolved == null)
            {
                return;
            }

            // 完成动作放在锁外：TrySetResult 会让 await 的续体排进同步上下文，
            // 而作者续体里可能同步地再注册新的等待——那需要重新拿这把锁。
            foreach (MagicWaiter waiter in resolved)
            {
                if (cancellation.IsCancellationRequested)
                {
                    waiter.Cancel(cancellation.Token);
                }
                else if (waiter.CallerToken.IsCancellationRequested)
                {
                    waiter.Cancel(waiter.CallerToken);
                }
                else
                {
                    waiter.Complete();
                }
            }
        }

        private void CancelAllWaiters()
        {
            MagicWaiter[] snapshot;
            lock (waitGate)
            {
                snapshot = waiters.ToArray();
                waiters.Clear();
            }

            foreach (MagicWaiter waiter in snapshot)
            {
                waiter.Cancel(cancellation.Token);
            }
        }

        // ==================== 结果与归因 ====================

        private void ReportOutcomeOnce()
        {
            if (outcomeReported || rootTask == null || !rootTask.IsCompleted)
            {
                return;
            }

            outcomeReported = true;

            if (rootTask.IsFaulted)
            {
                AggregateException error = rootTask.Exception;
                if (error != null)
                {
                    PolarisAPI.Errors.Report(
                        error.GetBaseException(),
                        "running magic " + Definition.Id + " (instance " + InstanceId + ")",
                        Definition.ProviderAssembly);
                }

                return;
            }

            if (rootTask.IsCanceled && cancelReason != null)
            {
                MagicLog.Debug("magic " + Definition.Id + " instance " + InstanceId + " cancelled: " + cancelReason);
            }
        }

        private void ReportLooseException(Exception exception)
        {
            PolarisAPI.Errors.Report(
                exception,
                "resuming magic " + Definition.Id + " (instance " + InstanceId + ")",
                Definition.ProviderAssembly);
        }

        private static Task FromException(Exception exception)
        {
            var source = new TaskCompletionSource<bool>();
            source.SetException(exception);
            return source.Task;
        }
    }
}
