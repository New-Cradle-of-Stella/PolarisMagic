using HarmonyLib;
using nel;

namespace Polaris.Magic.Game.Patch
{
    /// <summary>
    /// 存档保护。
    ///
    /// <c>MagicSelector.readBinaryFrom</c> 逐条读 <c>ushort</c> 并调 <c>MKind.Get</c>；查不到就
    /// <b>静默跳过</b>（整个循环体还包在 <c>try/catch</c> 里，连异常都不会冒出来）。也就是说自定义
    /// MKind 只要晚一步注入，玩家已经学会的自定义魔法就会在这次读档中彻底消失，而且没有任何提示。
    ///
    /// 所以这里在读档前再兜一次注入。正常情况下 <c>MKind.reloadKindDataScript</c> 的 Postfix 早就做完了，
    /// 这个 Prefix 只是保证"任何进入读档的路径"都不可能漏。
    /// </summary>
    [HarmonyPatch(typeof(MagicSelector), nameof(MagicSelector.readBinaryFrom))]
    internal static class Patch_MagicSelector_readBinaryFrom
    {
        [HarmonyPrefix]
        private static void Prefix() => MagicKindInjector.Inject();
    }
}
