using HarmonyLib;
using nel;

namespace Polaris.Magic.Game.Patch
{
    /// <summary>
    /// 注入自定义 <c>MKind</c> 的时机。
    ///
    /// 挂 Postfix 而不是自己找时机调用：此时原字典和小图标序列都已经建好，构造 <c>MKind</c> 不会碰到
    /// 半初始化的图标资源。<c>reloadKindDataScript</c> 自带"已加载就直接返回"的早退，因此这个 Postfix
    /// 可能在字典已存在时被再次触发；注入本身是幂等的。
    /// </summary>
    [HarmonyPatch(typeof(MKind), nameof(MKind.reloadKindDataScript))]
    internal static class Patch_MKind_reloadKindDataScript
    {
        [HarmonyPostfix]
        private static void Postfix() => MagicKindInjector.Inject();
    }
}
