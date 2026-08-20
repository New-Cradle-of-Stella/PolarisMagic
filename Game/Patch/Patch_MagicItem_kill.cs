using HarmonyLib;
using nel;

namespace Polaris.Magic.Game.Patch
{
    /// <summary>
    /// 原版实体结束时把中间层实例交给取消泵。
    ///
    /// 挂 <c>kill</c> 的 Postfix 而不是 <c>releasePooledObject</c>：后者会用不同参数组合被多次调用，
    /// 也会在新一代 <c>init</c> 里清上一代残留，当成"魔法结束"事件会重复触发。
    /// <c>kill</c> 本身也没有开头即返回的重入保护，因此下游的 <c>OnKilled</c> 必须幂等——
    /// 它按表里有没有这一项来判断，重复调用是空操作。
    /// </summary>
    [HarmonyPatch(typeof(MagicItem), nameof(MagicItem.kill))]
    internal static class Patch_MagicItem_kill
    {
        [HarmonyPostfix]
        private static void Postfix(MagicItem __instance) => MagicRuntimeHost.OnKilled(__instance);
    }
}
