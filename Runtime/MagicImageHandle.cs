using Polaris.Drawing;
using Polaris.Res;
using UnityEngine;
using XX;

namespace Polaris.Magic.Runtime
{
    /// <summary>
    /// 挂在魔法对象上的一张静态图片。纹理由 PolarisRes 解析并持有租约，句柄释放时归还租约——
    /// 同一张图被多个施法同时用到时，底层只解码一次。
    /// </summary>
    public sealed class MagicImageHandle : MagicVisualHandle
    {
        private readonly IResourceLease<Texture2D> lease;
        private readonly DrawImage image;

        private uint tintArgb = 0xFFFFFFFFu;
        private float pixelScale = 1f;
        private bool flipX;

        internal MagicImageHandle(MagicObject owner, IResourceLease<Texture2D> lease)
            : base(owner)
        {
            this.lease = lease;
            image = new DrawImage(lease.Value);
        }

        internal MagicImageHandle(MagicObject owner, MImage source)
            : base(owner)
        {
            image = new DrawImage((Texture2D)source.Tx);
        }

        /// <summary>ARGB 染色，默认不染。</summary>
        public uint TintArgb
        {
            get => tintArgb;
            set
            {
                tintArgb = value;
                Node?.Invalidate();
            }
        }

        /// <summary>一个纹理像素画成多少地图单位。</summary>
        public float PixelScale
        {
            get => pixelScale;
            set
            {
                pixelScale = value;
                Node?.Invalidate();
            }
        }

        /// <summary>水平翻转。做左右两个朝向时不必准备两张图。</summary>
        public bool FlipX
        {
            get => flipX;
            set
            {
                flipX = value;
                ApplyTransform();
            }
        }

        internal override float LocalScaleX => flipX ? -1f : 1f;

        public int PixelWidth => image.PixelWidth;

        public int PixelHeight => image.PixelHeight;

        /// <summary>绘制回调：以对象原点为中心。节点变换由基类按对象旋转/缩放写入。</summary>
        internal void Draw(DrawContext context)
        {
            float width = image.PixelWidth * pixelScale;
            float height = image.PixelHeight * pixelScale;

            context.DrawImage(
                image,
                new DrawRect(-width * 0.5f, -height * 0.5f, width, height),
                new DrawImageStyle { TintArgb = tintArgb });
        }

        internal override void ReleaseResources() => lease?.Dispose();
    }
}
