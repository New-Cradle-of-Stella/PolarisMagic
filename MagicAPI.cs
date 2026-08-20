using nel;
using Polaris.API;
using Polaris.Magic.Definitions;
using Polaris.Magic.Game;
using Polaris.Magic.Runtime;

namespace Polaris.Magic
{
    /// <summary>
    /// PolarisMagic 的公开门面：查注册结果、给玩家授予/收回自定义魔法、登记特效规格。
    ///
    /// 只有这一层是模组代码需要主动调用的东西。魔法本身的注册不走这里——它由生成的
    /// <c>.pmagic.g.cs</c> 上的提供器特性完成，因为注册必须早于读档，不能依赖模组代码跑得够早。
    /// </summary>
    public static class MagicAPI
    {
        /// <summary>特效规格注册表，见 <see cref="MagicEffects.Register"/>。</summary>
        public static void RegisterEffect(string effectId, MagicEffectSpec spec) =>
            MagicEffects.Register(effectId, spec);

        /// <summary>这个字符串 Id 是否已经注册成功。注册失败的魔法不会出现在这里。</summary>
        public static bool IsRegistered(string magicId) => MagicRegistry.TryGet(magicId, out _);

        /// <summary>取注册好的静态定义；未注册返回 <c>null</c>。</summary>
        public static MagicDefinition GetDefinition(string magicId) =>
            MagicRegistry.TryGet(magicId, out MagicRegistration registration) ? registration.Definition : null;

        /// <summary>
        /// 这个魔法分配到的数字 <c>MGKIND</c>，未注册返回 0。
        /// 数字会写进玩家存档，因此一旦分配就不再变动；日志和迁移映射用得上。
        /// </summary>
        public static int GetNumericId(string magicId) =>
            MagicRegistry.TryGet(magicId, out MagicRegistration registration) ? (int)registration.Kind : 0;

        /// <summary>玩家是否已经学会这个自定义魔法。</summary>
        public static bool IsGranted(string magicId)
        {
            if (!MagicRegistry.TryGet(magicId, out MagicRegistration registration))
            {
                return false;
            }

            MagicSelector selector = ResolveSelector();
            return selector != null && selector.isAssigned(registration.Kind);
        }

        /// <summary>
        /// 授予玩家这个自定义魔法（等价于原版的"学会"）。玩家不在场或魔法未注册时返回 false。
        ///
        /// 故意做成显式调用：自定义魔法默认不解锁——注册了就自动出现在魔法菜单里，会让玩家在
        /// 装了模组的存档里看到一堆自己没学过的东西，也让"什么时候学会"这件事脱离模组的剧情设计。
        /// </summary>
        public static bool Grant(string magicId, int maxGrade = 0)
        {
            if (!MagicRegistry.TryGet(magicId, out MagicRegistration registration))
            {
                MagicLog.Warn("Cannot grant '" + magicId + "': no such registered magic.");
                return false;
            }

            MagicSelector selector = ResolveSelector();
            if (selector == null)
            {
                return false;
            }

            // 授予前先确认 MKind 已经在表里：setObtainFlag 内部同样是 MKind.Get 查不到就什么都不做。
            MagicKindInjector.Inject();

            selector.setObtainFlag(registration.Kind, maxGrade);
            return true;
        }

        /// <summary>收回这个自定义魔法。</summary>
        public static bool Revoke(string magicId)
        {
            if (!MagicRegistry.TryGet(magicId, out MagicRegistration registration))
            {
                return false;
            }

            MagicSelector selector = ResolveSelector();
            if (selector == null)
            {
                return false;
            }

            // 原版用负的 max_grade 表示"移除"，见 MagicSelector.setObtainFlag。
            selector.setObtainFlag(registration.Kind, -1);
            return true;
        }

        private static MagicSelector ResolveSelector()
        {
            PR player = GameBinding.Player;
            return player?.Skill?.MagicSel;
        }
    }
}
