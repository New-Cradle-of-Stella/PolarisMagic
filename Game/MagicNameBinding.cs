using System;
using nel;
using Polaris.Magic.Runtime;
using XX;

namespace Polaris.Magic.Game
{
    /// <summary>
    /// 名字必须双向解析：<c>FEnum&lt;MGKIND&gt;.PreDefineToStr</c> 只解决"值 → 名字"，但点击、悬停、
    /// 事件命令和菜单 MAGIC_LEARN 读回时走的是 <c>TryParse</c>，另一张私有缓存；只登记一半会出现
    /// "菜单里画得出来、点下去没反应"。反向那半张表是 <c>FEnum&lt;T&gt;</c> 的私有静态字段
    /// <c>Ostr</c>，靠 Krafs.Publicizer 直接写入一条，这与原版自身回写缓存的行为一致，比再 patch
    /// 一次 <c>TryParse</c> 干净。
    /// </summary>
    internal static class MagicNameBinding
    {
        internal static void Register(MagicRegistration registration)
        {
            // 值 → 名字。
            FEnum<MGKIND>.PreDefineToStr(registration.Kind, registration.EnumName);

            // 名字 → 值。先解析一个原版名字，逼 FEnum 完成惰性 Init，再往缓存里塞自己那一条。
            FEnum<MGKIND>.TryParse(nameof(MGKIND.FIREBALL), out _);

            try
            {
                FEnum<MGKIND>.Ostr[registration.EnumName] = registration.Kind;
            }
            catch (Exception ex)
            {
                MagicLog.Error(
                    "Could not register the reverse name lookup for '" + registration.EnumName +
                    "'; the magic menu and event commands will not resolve it: " + ex.Message);
            }
        }
    }
}
