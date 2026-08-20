using HarmonyLib;
using nel;

namespace Polaris.Magic.Game.Patch
{
    /// <summary>
    /// 每个 <c>MGContainer</c> 建好之后补装自定义 holder。
    ///
    /// 每个 <c>NelM2DBase</c> 构造时都会新建一个 <c>MGContainer</c>，而 holder 的字典是容器私有的，
    /// 所以这是"每张地图都要做一次"的注册，不是全局一次性的。
    /// </summary>
    [HarmonyPatch(typeof(MGContainer), MethodType.Constructor, new[] { typeof(NelM2DBase) })]
    internal static class Patch_MGContainer_ctor
    {
        [HarmonyPostfix]
        private static void Postfix(MGContainer __instance) => MagicHolderInstaller.Install(__instance);
    }
}
