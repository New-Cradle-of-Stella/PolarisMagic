using HarmonyLib;
using nel;

namespace Polaris.Magic.Game.Patch
{
    /// <summary>
    /// 存档保护：<c>MagicSelector.readBinaryFrom</c> 逐条读 <c>ushort</c> 并调 <c>MKind.Get</c>，
    /// 查不到就<b>静默跳过</b>，自定义 MKind 只要晚一步注入，玩家已学会的魔法就会在读档中彻底消失
    /// 且没有提示。所以这里在读档前再兜一次注入，保证任何进入读档的路径都不会漏，即便
    /// <c>MKind.reloadKindDataScript</c> 的 Postfix 通常已经做完。
    /// </summary>
    [HarmonyPatch(typeof(MagicSelector), nameof(MagicSelector.readBinaryFrom))]
    internal static class Patch_MagicSelector_readBinaryFrom
    {
        [HarmonyPrefix]
        private static void Prefix() => MagicKindInjector.Inject();
    }
}
