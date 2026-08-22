using HarmonyLib;
using nel;

namespace Polaris.Magic.Game.Patch
{
    /// <summary>
    /// 原版实体结束时把中间层实例交给取消泵，挂 <c>kill</c> 的 Postfix 而不是
    /// <c>releasePooledObject</c>：后者会用不同参数组合被多次调用，当成"魔法结束"事件会重复触发。
    /// <c>kill</c> 本身也没有重入保护，因此下游 <c>OnKilled</c> 必须幂等，按表里有没有这一项判断，
    /// 重复调用是空操作。
    /// </summary>
    [HarmonyPatch(typeof(MagicItem), nameof(MagicItem.kill))]
    internal static class Patch_MagicItem_kill
    {
        [HarmonyPostfix]
        private static void Postfix(MagicItem __instance) => MagicRuntimeHost.OnKilled(__instance);
    }
}
