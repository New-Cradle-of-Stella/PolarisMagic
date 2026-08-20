using System;
using System.Runtime.CompilerServices;
using nel;
using Polaris.Magic.Runtime;

namespace Polaris.Magic.Game
{
    /// <summary>
    /// 往每个 <c>MGContainer</c> 的 holder 字典里装自定义 holder。
    ///
    /// 原版没有公开的注册入口：<c>OHoldFD</c> 是 private readonly 字典，而公开的
    /// <c>initFunc</c>/<c>GetHoldFD</c> 都假定键已经存在。这里直接写那张原字典（靠 Krafs.Publicizer
    /// 拿到访问权），而不是自建旁路字典再 patch 两个访问器——写进原字典之后，
    /// <c>MGContainer.destruct</c> 会自然调到我们 holder 的 <c>destruct</c>，手杖 listener、再次施法
    /// 和销毁路径也都不需要额外处理。
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
