using System;
using System.Collections.Generic;
using System.Threading;
using nel;
using Polaris.Magic.Runtime;

namespace Polaris.Magic.Game
{
    /// <summary>
    /// 施法实例的总表：把原版 <c>MagicItem</c> 映射到中间层实例，并驱动它们的 Tick。
    ///
    /// <c>MagicItem</c> 是池化对象，会在换地图后被下一代施法复用，而 <c>MGContainer.clear</c> 还会把
    /// <c>id</c> 计数器归零。所以这里的身份判定用"同一个对象引用 + 同一个 id"两条一起（见
    /// <see cref="MagicRuntimeInstance.OwnsItem"/>）；只认对象引用会把复用后的新实例当成旧实例。
    /// </summary>
    internal static class MagicRuntimeHost
    {
        private static readonly Dictionary<MagicItem, MagicRuntimeInstance> Active =
            new Dictionary<MagicItem, MagicRuntimeInstance>();

        private static readonly MagicCancellationPump Pump = new MagicCancellationPump();

        private static int nextInstanceId;
        private static int mainThreadId;

        /// <summary>记下游戏主线程。作者续体只允许在这个线程上恢复。</summary>
        internal static void Initialize()
        {
            mainThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        /// <summary>
        /// holder 的每 Tick 入口。第一次见到某个正式实体时创建中间层实例并调用一次作者回调，
        /// 之后只推进它。返回 false 时原版实体立即结束。
        /// </summary>
        internal static bool Run(MagicRegistration registration, MagicItem item, float fcnt)
        {
            if (item == null)
            {
                return false;
            }

            MagicRuntimeInstance instance = Resolve(registration, item);
            if (instance == null)
            {
                return false;
            }

            bool alive;
            try
            {
                alive = instance.Tick(fcnt);
            }
            catch (Exception ex)
            {
                PolarisAPI.Errors.Report(
                    ex,
                    "ticking magic " + registration.Definition.Id,
                    registration.Definition.ProviderAssembly);
                alive = false;
            }

            if (alive)
            {
                return true;
            }

            Active.Remove(item);
            instance.Release();
            return false;
        }

        /// <summary>
        /// 原版实体被结束（击杀、切图、原版自己回收）。
        ///
        /// Task 可能还停在某个 await 上，它的 <c>finally</c> 还没跑。这里请求取消并把实例交给取消泵：
        /// 之后不再有 holder 的 Tick，只有泵能把 <c>finally</c> 推完并释放资源。
        /// </summary>
        internal static void OnKilled(MagicItem item)
        {
            if (item == null || !Active.TryGetValue(item, out MagicRuntimeInstance instance))
            {
                return;
            }

            Active.Remove(item);
            instance.RequestCancel("the vanilla MagicItem was killed");
            Pump.Adopt(instance);
        }

        /// <summary>某个容器整体销毁：它名下的实例一个都不能留到下一张地图。</summary>
        internal static void DropContainer(MGContainer container)
        {
            if (container == null)
            {
                return;
            }

            List<MagicItem> doomed = null;
            foreach (KeyValuePair<MagicItem, MagicRuntimeInstance> entry in Active)
            {
                if (ReferenceEquals(entry.Key.MGC, container))
                {
                    (doomed ?? (doomed = new List<MagicItem>())).Add(entry.Key);
                }
            }

            if (doomed == null)
            {
                return;
            }

            foreach (MagicItem item in doomed)
            {
                MagicRuntimeInstance instance = Active[item];
                Active.Remove(item);
                instance.RequestCancel("the owning MGContainer was destroyed");
                Pump.Adopt(instance);
            }
        }

        /// <summary>组件每帧调用：只驱动取消泵。存活实例由原版的魔法循环驱动，不在这里重复推进。</summary>
        internal static void Update()
        {
            // 泵里的实例已经脱离原版魔法循环，拿不到原版的 fcnt 了；按标准速度 1 帧推进。
            Pump.Update(1f);
        }

        /// <summary>组件关闭：取消并释放全部实例，不等 finally 走完。</summary>
        internal static void Shutdown()
        {
            foreach (KeyValuePair<MagicItem, MagicRuntimeInstance> entry in Active)
            {
                entry.Value.RequestCancel("the PolarisMagic component is shutting down");
                entry.Value.Release();
            }

            Active.Clear();
            Pump.Clear();
        }

        private static MagicRuntimeInstance Resolve(MagicRegistration registration, MagicItem item)
        {
            if (Active.TryGetValue(item, out MagicRuntimeInstance existing))
            {
                if (existing.OwnsItem(item))
                {
                    return existing;
                }

                // 池对象已经被下一代施法复用，而上一代的 kill 没走到我们这儿。上一代交给泵收尾。
                Active.Remove(item);
                existing.RequestCancel("the pooled MagicItem was reused by a newer cast");
                Pump.Adopt(existing);
            }

            MagicRuntimeInstance created;
            try
            {
                created = new MagicRuntimeInstance(
                    registration.Definition,
                    item,
                    ++nextInstanceId,
                    mainThreadId);
                created.Start();
            }
            catch (Exception ex)
            {
                PolarisAPI.Errors.Report(
                    ex,
                    "starting magic " + registration.Definition.Id,
                    registration.Definition.ProviderAssembly);
                return null;
            }

            Active.Add(item, created);
            return created;
        }
    }
}
