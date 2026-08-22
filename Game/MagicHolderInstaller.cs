using System;
using System.Runtime.CompilerServices;
using nel;
using Polaris.Magic.Runtime;

namespace Polaris.Magic.Game
{
    /// <summary>
    /// 往每个 <c>MGContainer</c> 的 holder 字典里装自定义 holder；原版没有公开注册入口，
    /// <c>OHoldFD</c> 是 private 字典，公开的 <c>initFunc</c>/<c>GetHoldFD</c> 都假定键已存在。
    /// 这里靠 Krafs.Publicizer 直接写那张原字典而非自建旁路字典，这样 <c>MGContainer.destruct</c>、
    /// 再次施法和销毁路径都不需要额外处理。
    /// </summary>
    internal static class MagicHolderInstaller
    {
        /// <summary>
        /// 已经装过 holder 的容器。用弱表：容器随地图销毁，这张表不能成为让它们活下来的唯一引用。
        /// </summary>
        private static readonly ConditionalWeakTable<MGContainer, object> Installed =
            new ConditionalWeakTable<MGContainer, object>();

        /// <summary>幂等安装。容器构造 Postfix 与首次施法前的兜底都会调它。</summary>
        internal static void Install(MGContainer container)
        {
            if (container == null || !MagicRegistry.IsReady)
            {
                return;
            }

            if (Installed.TryGetValue(container, out _))
            {
                return;
            }

            Installed.Add(container, string.Empty);

            foreach (MagicRegistration registration in MagicRegistry.All)
            {
                try
                {
                    if (!container.OHoldFD.ContainsKey(registration.Kind))
                    {
                        container.OHoldFD[registration.Kind] = new MagicTaskHolder(registration, container);
                    }
                }
                catch (Exception ex)
                {
                    MagicLog.Error(
                        "Failed to install the holder for magic '" + registration.Definition.Id +
                        "' into an MGContainer; casting it in this map would do nothing: " + ex.Message);
                }
            }
        }

        /// <summary>取这个容器里某种自定义魔法的 holder，必要时先补装。</summary>
        internal static MagicTaskHolder Require(MGContainer container, MagicRegistration registration)
        {
            Install(container);
            return container.GetHoldFD(registration.Kind) as MagicTaskHolder;
        }
    }
}
