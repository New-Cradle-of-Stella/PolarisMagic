using System;
using Polaris.API;
using Polaris.Drawing;
using Polaris.Res;
using UnityEngine;

namespace Polaris.Magic.Runtime
{
    /// <summary>
    /// 挂在魔法对象上的一个精灵表特效。帧推进由中间层在每个 Tick 完成，用的是这次施法自己的
    /// <see cref="MagicClock"/>——时停/慢放时特效跟着慢下来，而不是按真实时间自顾自播。
    /// </summary>
    public sealed class MagicEffectHandle : MagicVisualHandle
    {
        private readonly MagicEffectSpec spec;
        private readonly IResourceLease<Texture2D> lease;
        private readonly DrawImage image;
        private readonly int frameCount;
        private readonly int columns;
        private readonly int frameWidth;
        private readonly int frameHeight;

        private float elapsedFrames;
        private int currentFrame = -1;

        internal MagicEffectHandle(MagicObject owner, MagicEffectSpec spec, IResourceLease<Texture2D> lease)
            : base(owner)
        {
            this.spec = spec;
            this.lease = lease;
            image = new DrawImage(lease.Value);

            columns = Math.Max(1, spec.Columns);
            int rows = Math.Max(1, spec.Rows);
            frameCount = spec.EffectiveFrameCount;
            frameWidth = Math.Max(1, image.PixelWidth / columns);
            frameHeight = Math.Max(1, image.PixelHeight / rows);

            Loop = spec.Loop;
            TintArgb = spec.TintArgb;
            Offset = new GameVector2(spec.OffsetX, spec.OffsetY);
        }

        /// <summary>是否继续推进帧。暂停时停在当前帧上，不隐藏。</summary>
        public bool Playing { get; set; } = true;

        public bool Loop { get; set; }

        public uint TintArgb { get; set; }

        /// <summary>非循环特效是否已经播完最后一帧。</summary>
        public bool IsFinished => !Loop && elapsedFrames >= frameCount * Math.Max(0.0001f, spec.FramesPerStep);

        /// <summary>回到第一帧重新播。</summary>
        public void Restart()
        {
            elapsedFrames = 0f;
            Playing = true;
            UpdateFrame();
        }

        internal override void Tick(MagicClock clock)
        {
            if (!Playing || clock == null)
            {
                return;
            }

            elapsedFrames += clock.DeltaFrames;
            UpdateFrame();
        }

        private void UpdateFrame()
        {
            float step = Math.Max(0.0001f, spec.FramesPerStep);
            int frame = (int)(elapsedFrames / step);

            if (frame >= frameCount)
            {
                if (Loop)
                {
                    frame = frameCount == 0 ? 0 : frame % frameCount;
                }
                else
                {
                    frame = frameCount - 1;
                    Playing = false;
                }
            }

            if (frame == currentFrame)
            {
                return;
            }

            currentFrame = frame;
            Node?.Invalidate();
        }

        internal void Draw(DrawContext context)
        {
            int frame = Math.Max(0, currentFrame);
            int column = frame % columns;
            int row = frame / columns;

            float width = frameWidth * spec.PixelScale;
            float height = frameHeight * spec.PixelScale;

            context.DrawImage(
                image,
                new DrawRect(-width * 0.5f, -height * 0.5f, width, height),
                new DrawImageStyle
                {
                    TintArgb = TintArgb,
                    SourcePixelRect = new DrawRect(column * frameWidth, row * frameHeight, frameWidth, frameHeight),
                });
        }

        internal override void ReleaseResources() => lease?.Dispose();
    }
}
