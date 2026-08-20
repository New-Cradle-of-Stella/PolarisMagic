using System;
using System.Threading;
using System.Threading.Tasks;

namespace Polaris.Magic.Runtime
{
    internal enum MagicWaitKind
    {
        /// <summary>下一次 Tick 就到期。</summary>
        NextTick,

        /// <summary>累计帧数到达 <see cref="MagicWaiter.TargetFrames"/> 时到期。</summary>
        Frames,

        /// <summary>谓词为真时到期；每 Tick 求值一次。</summary>
        Predicate,
    }

    /// <summary>
    /// 一个挂起的等待。到期与取消的判定全部在游戏主线程的 Tick 里做，因此不需要注册
    /// <see cref="CancellationTokenRegistration"/>，也不会有"回调在别的线程上跑"的窗口。
    ///
    /// 完成用 <see cref="TaskCompletionSource{TResult}"/>：完成时 <c>await</c> 的续体会被
    /// <see cref="MagicSynchronizationContext"/> 接住排队，而不是在这里就地重入 Tick 循环。
    /// </summary>
    internal sealed class MagicWaiter
    {
        private readonly TaskCompletionSource<bool> source =
            new TaskCompletionSource<bool>(TaskCreationOptions.None);

        internal MagicWaiter(MagicWaitKind kind, float targetFrames, Func<bool> predicate, CancellationToken callerToken)
        {
            Kind = kind;
            TargetFrames = targetFrames;
            Predicate = predicate;
            CallerToken = callerToken;
        }

        internal MagicWaitKind Kind { get; }

        internal float TargetFrames { get; }

        internal Func<bool> Predicate { get; }

        /// <summary>作者传进来的令牌。实例自己的令牌由实例统一检查，这里只管额外的那一个。</summary>
        internal CancellationToken CallerToken { get; }

        internal ValueTask Task => new ValueTask(source.Task);

        internal bool IsDue(MagicClock clock)
        {
            switch (Kind)
            {
                case MagicWaitKind.NextTick:
                    return true;
                case MagicWaitKind.Frames:
                    return clock.ElapsedFrames >= TargetFrames;
                case MagicWaitKind.Predicate:
                    return Predicate == null || Predicate();
                default:
                    return true;
            }
        }

        internal void Complete() => source.TrySetResult(true);

        internal void Cancel(CancellationToken token) => source.TrySetCanceled(token);

        internal void Fault(Exception exception) => source.TrySetException(exception);
    }
}
