using System;
using HarmonyLib;
using nel;
using XX;

namespace Polaris.Magic.Game.Patch
{
    /// <summary>
    /// 自定义魔法的初始化必须整段接管 <c>MDAT.initMagicItem</c>，不能只往 MKind/选择器里加个数字。
    ///
    /// 原因是原版那个 <c>switch</c> 的 default 分支会强行给未知 kind 打上 <c>IMMEDIATE</c> 并清掉
    /// <c>CHANTED</c>，而"准备圆"的条件恰恰是"非 IMMEDIATE 且 casttime != 0"——落进 default 就等于
    /// 绕过咏唱直接释放，咏唱时间、暂存 MP 和释放动作全都不会发生。
    ///
    /// Prefix 跳过原方法之后，原方法在类型分派前做的那几步重置、以及方法末尾的谜题魔法覆盖，
    /// 都得自己补上：池对象不重置会继承上一代施法的数值。
    /// </summary>
    [HarmonyPatch(typeof(MDAT), nameof(MDAT.initMagicItem))]
    internal static class Patch_MDAT_initMagicItem
    {
        [HarmonyPrefix]
        private static bool Prefix(MagicItem Mg, ref bool init_aimpos_to_d)
        {
            if (Mg == null || !MagicRegistry.TryGet(Mg.kind, out MagicRegistration registration))
            {
                return true;
            }

            init_aimpos_to_d = true;

            try
            {
                Initialize(Mg, registration);
            }
            catch (Exception ex)
            {
                PolarisAPI.Errors.Report(
                    ex,
                    "initializing magic " + registration.Definition.Id,
                    registration.Definition.ProviderAssembly);

                // 初始化没走完的实体不能留在场上：立刻结束它，而不是让一个半初始化的魔法运行。
                Mg.hittype |= MGHIT.IMMEDIATE;
                Mg.casttime = 0f;
            }

            return false;
        }

        private static void Initialize(MagicItem Mg, MagicRegistration registration)
        {
            bool immediate = (Mg.hittype & MGHIT.IMMEDIATE) != 0;

            // 原版在类型分派前做的重置。
            Mg.casttime = 0f;
            Mg.mp_crystalize = 0.5f;
            Mg.hittype |= MGHIT.CHANTED;

            if (!immediate)
            {
                Mg.hittype &= (MGHIT)~(int)MGHIT.IMMEDIATE;
                Mg.phase = -14;
            }

            // Atk0 必须在 MKind.initMagicS 之前建好：MKind.initMagic 只在 Atk0 非 null 时才会写
            // knockback_len 与 tired_time_to_super_armor，也就是 .pmagic 里的 SuperArmorTiredTime。
            if (Mg.Atk0 == null)
            {
                Mg.Atk0 = Mg.MGC.makeAtk();
            }

            MKind.initMagicS(Mg);

            // 显式模板：MagicNotifiearData 的内置表在容器构造时就封板了，认不出自定义 kind。
            Mg.MGC.Notf.GetForCaster(Mg, registration.NotifierTemplate);

            if (immediate)
            {
                // 正式态才装运行委托。准备态留给 MagicItem.init 装通用咏唱圆——准备态就装正式 handler
                // 会让作者 Task 在咏唱期间提前开跑。
                MagicTaskHolder holder = MagicHolderInstaller.Require(Mg.MGC, registration);
                if (holder == null)
                {
                    throw new InvalidOperationException(
                        "No holder is installed for magic '" + registration.Definition.Id + "' in this MGContainer.");
                }

                holder.initFunc(Mg);

                // 必须放在 MKind 之后：initMagic 会把基础 casttime 写回来，而正式实体的 casttime 是 0。
                Mg.casttime = 0f;
            }

            ApplyPuzzleOverrides(Mg);
        }

        /// <summary>
        /// 复现原方法末尾的谜题魔法覆盖。Prefix 跳过原方法也跳过了这一段，不补的话自定义魔法在
        /// 谜题房间里的消耗和咏唱时间会与原版八种不一致。
        /// </summary>
        private static void ApplyPuzzleOverrides(MagicItem Mg)
        {
            if (!Mg.is_chanted_magic || !PUZ.IT.isPuzzleManagingMp())
            {
                return;
            }

            Mg.mp_crystalize = 0f;

            if (Mg.casttime > 0f)
            {
                Mg.casttime = X.Mx(10f, X.NI(Mg.casttime, 20f, PUZ.IT.casttime_reduce_level));
            }

            if (Mg.reduce_mp > 0f)
            {
                // 原版这里对 PR_BURST 翻倍；自定义魔法永远不是 PR_BURST。
                Mg.reduce_mp = 64f;
            }
        }
    }
}
