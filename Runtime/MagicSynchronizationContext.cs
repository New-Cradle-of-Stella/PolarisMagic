using System;
using System.Collections.Generic;
using System.Threading;

namespace Polaris.Magic.Runtime
{
    /// <summary>
    /// 一次施法专属的同步上下文。作者 <c>await</c> 的续体全部落进这里排队，由 holder 在游戏主线程
    /// 逐个执行——这就是"续体一定回到主线程"的实现方式，作者不需要自己 marshal。
    ///
    /// 每个施法实例一个实例：并发施法的续体队列互不影响，一个魔法的续体抛异常也不会打断别人的。
    /// </summary>
    internal sealed class MagicSynchronizationContext : SynchronizationContext
    {
        private readonly object gate = new object();
        private readonly Queue<Work> pending = new Queue<Work>();
        private readonly int mainThreadId;

        internal MagicSynchronizationContext(int mainThreadId)
        {
            this.mainThreadId = mainThreadId;
        }

        /// <summary>一个 Tick 内最多执行多少个续体。防住"续体里同步地再排一个续体"变成死循环卡死整局游戏。</summary>
        private const int DrainBudget = 4096;

        internal bool HasPending
        {
            get
            {
                lock (gate)
                {
                    return pending.Count > 0;
                }
            }
        }

        public override void Post(SendOrPostCallback d, object state)
        {
            if (d == null)
            {
                return;
            }

            lock (gate)
            {
                pending.Enqueue(new Work(d, state));
            }
        }

        /// <summary>
        /// 主线程上等价于直接执行；其它线程上无法满足"同步等到主线程跑完"而不冒死锁风险，
        /// 因此直接拒绝——作者在后台线程要回主线程只能 <c>await</c>。
        /// </summary>
        public override void Send(SendOrPostCallback d, object state)
        {
            if (Thread.CurrentThread.ManagedThreadId == mainThreadId)
            {
                d?.Invoke(state);
                return;
            }

            throw new NotSupportedException(
                "A blocking Send() onto the Polaris magic context would deadlock the game thread; await instead.");
        }

        public override SynchronizationContext CreateCopy() => this;

        /// <summary>
        /// 执行当前排队的续体。返回实际执行条数。
        /// 续体自己再排队的部分留到下一轮循环，直到队列空或者预算用尽。
        /// </summary>
        internal int Drain(Action<Exception> onError)
        {
            int executed = 0;

            while (executed < DrainBudget)
            {
                Work work;
                lock (gate)
                {
                    if (pending.Count == 0)
                    {
                        break;
                    }

                    work = pending.Dequeue();
                }

                executed++;
                try
                {
                    work.Callback(work.State);
                }
                catch (Exception ex)
                {
                    // 续体异常正常情况下会被 async 状态机收进根 Task；能漏到这里的是 async void
                    // 之类的失控路径，不能让它掀掉本帧其余魔法。
                    onError?.Invoke(ex);
                }
            }

            return executed;
        }

        /// <summary>丢弃全部排队续体。实例已经彻底结束、不会再有 Tick 时调用。</summary>
        internal void Clear()
        {
            lock (gate)
            {
                pending.Clear();
            }
        }

        private readonly struct Work
        {
            internal Work(SendOrPostCallback callback, object state)
            {
                Callback = callback;
                State = state;
            }

            internal SendOrPostCallback Callback { get; }

            internal object State { get; }
        }
    }
}
