using System;
using System.Collections.Generic;

namespace Polaris.Magic.Runtime
{
    /// <summary>
    /// 组件级的取消泵：原版实体被外部结束（击杀、切图、组件关闭）后就不再有 holder 的 Tick 了，
    /// 但作者的 <c>finally</c> 里往往还有一段 <c>await</c> 之后才跑完的清理。这里接手继续每帧推进
    /// 这些实例直到 Task 真正结束再释放，否则 <c>finally</c> 会永远停在某个 await 上，对象也就
    /// 永远不会被回收。
    /// </summary>
    internal sealed class MagicCancellationPump
    {
        /// <summary>取消之后允许再泵多少帧。超时的实例强制释放，避免一个写坏的 finally 永久占住泵。</summary>
        private const int GraceTicks = 600;

        private readonly List<Entry> draining = new List<Entry>();

        /// <summary>接手一个已经请求取消的实例。已经结束的实例直接释放，不进泵。</summary>
        internal void Adopt(MagicRuntimeInstance instance)
        {
            if (instance == null)
            {
                return;
            }

            if (instance.IsFinished || !instance.IsStarted)
            {
                instance.Release();
                return;
            }

            draining.Add(new Entry(instance));
        }

        internal void Update(float deltaFrames)
        {
            for (int i = draining.Count - 1; i >= 0; i--)
            {
                Entry entry = draining[i];
                bool alive;

                try
                {
                    alive = entry.Instance.Tick(deltaFrames);
                }
                catch (Exception ex)
                {
                    MagicLog.Error(
                        "Draining magic " + entry.Instance.Definition.Id + " threw; releasing it now: " + ex.Message);
                    alive = false;
                }

                entry.Ticks++;

                if (alive && entry.Ticks < GraceTicks)
                {
                    draining[i] = entry;
                    continue;
                }

                if (alive)
                {
                    MagicLog.Warn(
                        "Magic " + entry.Instance.Definition.Id + " instance " + entry.Instance.InstanceId +
                        " did not finish within " + GraceTicks + " ticks after cancellation; releasing it anyway.");
                }

                draining.RemoveAt(i);
                entry.Instance.Release();
            }
        }

        /// <summary>组件关闭：不再等待，立刻释放全部在泵的实例。</summary>
        internal void Clear()
        {
            foreach (Entry entry in draining)
            {
                entry.Instance.Release();
            }

            draining.Clear();
        }

        private struct Entry
        {
            internal Entry(MagicRuntimeInstance instance)
            {
                Instance = instance;
                Ticks = 0;
            }

            internal MagicRuntimeInstance Instance { get; }

            internal int Ticks { get; set; }
        }
    }
}
