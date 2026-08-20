using System;
using Polaris.API;

namespace Polaris.Magic.Runtime
{
    /// <summary>
    /// 与具体实体无关的世界查询。全部走 PolarisCore 的能力层，不直连原版静态类——原版那些静态
    /// 在标题画面/地图切换期可能还没就绪，能力层已经处理过这些空窗。
    /// </summary>
    public sealed class MagicWorldServices
    {
        private readonly Random random;

        internal MagicWorldServices(int seed)
        {
            random = new Random(seed);
        }

        /// <summary>原版全局帧计数。</summary>
        public int FrameCount => PolarisAPI.Game.Loop.FrameCount;

        /// <summary>当前是否夜晚。</summary>
        public bool IsNight => PolarisAPI.Game.World.IsNight();

        /// <summary>当前危险度。</summary>
        public float DangerLevel => PolarisAPI.Game.World.DangerLevel;

        /// <summary>
        /// 每个施法实例独立的随机源，种子来自实例序号：并发施法各自独立，
        /// 也不会去动原版自己的随机状态（那会影响原版逻辑的可复现性）。
        /// </summary>
        public float NextFloat() => (float)random.NextDouble();

        public float NextFloat(float min, float max) => min + (float)random.NextDouble() * (max - min);

        public int NextInt(int minInclusive, int maxExclusive) => random.Next(minInclusive, maxExclusive);

        /// <summary>该矩形是否在摄像机内。用于"飞出画面就结束"这类判定。</summary>
        public bool IsInCamera(GameVector2 center, float width, float height, float marginPixels = 0f)
        {
            GameMap map = PolarisAPI.Game.World.CurrentMap;
            return map != null && map.IsInCamera(center.X, center.Y, width, height, marginPixels);
        }
    }
}
