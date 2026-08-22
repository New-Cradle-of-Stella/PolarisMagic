using System;
using System.Globalization;
using Better;
using nel;
using Polaris.Magic.Runtime;

namespace Polaris.Magic.Game
{
    /// <summary>
    /// 往原版 <c>MKind</c> 表里注入自定义魔法的基础数据，时机必须早于
    /// <c>MagicSelector.readBinaryFrom</c>：读档时找不到对应记录会被<b>静默跳过</b>，玩家已学会的
    /// 魔法会无声消失。因此注入挂在 <c>MKind.reloadKindDataScript</c> 的 Postfix 上（此时原字典和
    /// 小图标序列已就绪），读档补丁再兜一次底。
    /// </summary>
    internal static class MagicKindInjector
    {
        private static bool injected;

        /// <summary>幂等注入。表还没加载时什么都不做，等 Postfix 再来。</summary>
        internal static void Inject()
        {
            BDic<MGKIND, MKind> table = MKind.getAllKindObject();
            if (table == null || !MagicRegistry.IsReady)
            {
                return;
            }

            foreach (MagicRegistration registration in MagicRegistry.All)
            {
                if (table.ContainsKey(registration.Kind))
                {
                    continue;
                }

                try
                {
                    table[registration.Kind] = BuildKind(registration, table);
                    RegisterMenuText(registration);
                }
                catch (Exception ex)
                {
                    MagicLog.Error(
                        "Failed to inject MKind data for magic '" + registration.Definition.Id +
                        "'; it cannot be selected or saved this session: " + ex.Message);
                }
            }

            if (!injected)
            {
                injected = true;
                MagicLog.Info("Injected " + MagicRegistry.All.Count + " custom MKind entry(-ies).");
            }
        }

        private static MKind BuildKind(MagicRegistration registration, BDic<MGKIND, MKind> table)
        {
            // index 参数会同时写进 icon_index：数字 kind 查不到自己的图标资源，因此借用原版条目的
            // 下标，避免 MTR.AMagicIconL[icon_index] 越界崩溃。模组要专属图标需要另外扩展图标资源。
            MKind donor = FindIconDonor(table);
            int iconIndex = donor?.icon_index ?? 0;

            var kind = new MKind(registration.Kind, iconIndex);
            if (kind.PFSmallIcon == null && donor != null)
            {
                kind.PFSmallIcon = donor.PFSmallIcon;
            }

            Definitions.MagicDefinition definition = registration.Definition;
            kind.reduce_mp = definition.MpCost;
            kind.casttime = (int)definition.CastTime;
            kind.mp_crystalize = definition.MpCrystalizeRatio;
            kind.crystalize_neutral_ratio = definition.NeutralCrystalizeRatio;
            kind.prepare_time = definition.PrepareTime;
            kind.mana_drain_lock = definition.ManaDrainLock;
            kind.projectile_power = definition.ProjectilePower;
            kind.shotgun_ratio = definition.ShotgunRatio;
            kind.tired_time_to_super_armor = definition.SuperArmorTiredTime;

            // 这一版固定 NORMAL：瞄准方向的选择器树是原版硬编码的，自定义魔法不引入专用 Aim。
            kind.def_aim = MagicSelector.MAGA.NORMAL;

            return kind;
        }

        /// <summary>借图标的来源：优先火球，没有就随便挑一个已存在的条目。</summary>
        private static MKind FindIconDonor(BDic<MGKIND, MKind> table)
        {
            if (table.TryGetValue(MGKIND.FIREBALL, out MKind fireball))
            {
                return fireball;
            }

            foreach (System.Collections.Generic.KeyValuePair<MGKIND, MKind> entry in table)
            {
                if (!MagicRegistry.IsCustom(entry.Key))
                {
                    return entry.Value;
                }
            }

            return null;
        }

        /// <summary>
        /// 菜单名与说明：原版按 <c>Mag_title_&lt;数字&gt;</c> / <c>Mag_desc_&lt;数字&gt;</c> 查表，但
        /// <c>MKind.localized_title_</c> 字段会被 <c>refineAllLanguageCache</c> 和
        /// <c>MagicSelector.newGame</c> 清掉，直接填字段留不住。因此走 Polaris 的本地化 resolver
        /// （挂在 <c>TX.Get</c> 上，语言刷新后仍在），这样模组用 <c>PolarisAPI.Localization.Register</c>
        /// 登记的正式译名能优先于内置表。
        /// </summary>
        private static void RegisterMenuText(MagicRegistration registration)
        {
            string number = ((int)registration.Kind).ToString(CultureInfo.InvariantCulture);
            string titleKey = "Mag_title_" + number;
            string descKey = "Mag_desc_" + number;
            string fallbackTitle = registration.Definition.Id;

            PolarisAPI.Localization.RegisterResolver(key =>
            {
                if (string.Equals(key, titleKey, StringComparison.Ordinal))
                {
                    return fallbackTitle;
                }

                return string.Equals(key, descKey, StringComparison.Ordinal) ? string.Empty : null;
            });
        }
    }
}
