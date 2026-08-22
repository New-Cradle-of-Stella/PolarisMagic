using System;
using System.Collections.Generic;

namespace Polaris.Magic.Runtime
{
    /// <summary>
    /// 一个特效的静态规格：一张精灵表加播放参数。特效走"先登记规格、再按 Id 挂载"而不是让作者
    /// 每次自己拼绘制回调，因为同一个特效通常被多个魔法复用，纹理租约、帧推进和清理都要跟着
    /// 施法 Task 的生命周期走。
    /// </summary>
    public sealed class MagicEffectSpec
    {
        public MagicEffectSpec(string textureResourceId)
        {
            TextureResourceId = textureResourceId ?? throw new ArgumentNullException(nameof(textureResourceId));
        }

        /// <summary>精灵表资源 Id，规则见 <c>AttachImage</c>。</summary>
        public string TextureResourceId { get; }

        /// <summary>精灵表的列数。</summary>
        public int Columns { get; set; } = 1;

        /// <summary>精灵表的行数。</summary>
        public int Rows { get; set; } = 1;

        /// <summary>实际帧数；0 表示 <see cref="Columns"/> × <see cref="Rows"/>。</summary>
        public int FrameCount { get; set; }

        /// <summary>每帧持续多少游戏帧。</summary>
        public float FramesPerStep { get; set; } = 2f;

        public bool Loop { get; set; } = true;

        /// <summary>ARGB 染色。</summary>
        public uint TintArgb { get; set; } = 0xFFFFFFFFu;

        /// <summary>一个纹理像素画成多少地图单位。</summary>
        public float PixelScale { get; set; } = 1f;

        /// <summary>相对所属对象原点的偏移（地图单位）。</summary>
        public float OffsetX { get; set; }

        public float OffsetY { get; set; }

        internal int EffectiveFrameCount
        {
            get
            {
                int grid = Math.Max(1, Columns) * Math.Max(1, Rows);
                return FrameCount > 0 ? Math.Min(FrameCount, grid) : grid;
            }
        }
    }

    /// <summary>
    /// 特效规格注册表：模组在自己的启动代码里登记，魔法用 <c>magicObject.AttachEffect(id)</c> 取用。
    /// 只在注册期写、施法期读，注册发生在组件 Awake/Start 阶段的主线程上，之后表不再变动，
    /// 因此施法路径不需要加锁。
    /// </summary>
    public static class MagicEffects
    {
        private static readonly Dictionary<string, MagicEffectSpec> Specs =
            new Dictionary<string, MagicEffectSpec>(StringComparer.Ordinal);

        /// <summary>登记（或覆盖）一个特效规格。</summary>
        public static void Register(string effectId, MagicEffectSpec spec)
        {
            if (string.IsNullOrEmpty(effectId))
            {
                throw new ArgumentException("The effect id cannot be empty.", nameof(effectId));
            }

            Specs[effectId] = spec ?? throw new ArgumentNullException(nameof(spec));
        }

        public static bool TryGet(string effectId, out MagicEffectSpec spec)
        {
            if (effectId == null)
            {
                spec = null;
                return false;
            }

            return Specs.TryGetValue(effectId, out spec);
        }

        internal static MagicEffectSpec Require(string effectId)
        {
            if (TryGet(effectId, out MagicEffectSpec spec))
            {
                return spec;
            }

            throw new MagicResourceException(
                "No magic effect is registered under '" + effectId + "'; call MagicEffects.Register during startup.");
        }
    }
}
