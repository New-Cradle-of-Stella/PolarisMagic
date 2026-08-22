using nel;
using Polaris.Magic.Definitions;

namespace Polaris.Magic.Game
{
    /// <summary>
    /// 一种自定义魔法在原版侧的全部登记信息：定义、分配到的数字 kind，以及那一份显式 Notifier 模板。
    ///
    /// 集中成一条记录是有意的：数字 Id、名称解析、MKind、holder、Notifier 与初始化如果各自散落，
    /// 就会出现"注册了一半"的魔法——能被选中却没有 handler，或者有 handler 却读档时被跳过。
    /// </summary>
    internal sealed class MagicRegistration
    {
        internal MagicRegistration(MagicDefinition definition, MGKIND kind, string enumName)
        {
            Definition = definition;
            Kind = kind;
            EnumName = enumName;
            NotifierTemplate = CreateNotifierTemplate();
        }

        internal MagicDefinition Definition { get; }

        internal MGKIND Kind { get; }

        /// <summary>供 <c>FEnum&lt;MGKIND&gt;</c> 双向解析用的大写名字，例如 <c>MYMOD_FIREBALL</c>。</summary>
        internal string EnumName { get; }

        /// <summary>
        /// 显式 Notifier 模板，必须自带一份：<c>MagicNotifiearData</c> 的构造器只硬编码复制原版模板，
        /// 且在 <c>MGContainer</c> 构造时就已封板，自定义魔法只能走 <c>GetForCaster(Mg, 模板)</c>
        /// 这个显式重载。模板里放一个 <c>no_draw</c> 的空 hit，因为自定义魔法的表现和判定都由作者
        /// Task 自己做，但 <c>Mn</c> 必须非 null 且 <c>_0</c> 可用，否则原版 <c>MagicItem.run</c>
        /// 会直接 NRE。
        /// </summary>
        internal MagicNotifiear NotifierTemplate { get; }

        private static MagicNotifiear CreateNotifierTemplate()
        {
            var template = new MagicNotifiear(1);
            template.AddHit(new MagicNotifiear.MnHit
            {
                no_draw = true,
                wall_hit = false,
                other_hit = false,
                len = 0f,
                maxt = -1f,
                need_fine = false,
            });
            return template;
        }
    }
}
