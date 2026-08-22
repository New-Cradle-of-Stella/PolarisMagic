using System;
using Polaris.API;
using Polaris.Drawing;

namespace Polaris.Magic.Runtime
{
    /// <summary>
    /// 挂在 <see cref="MagicObject"/> 上的一个可视件的公共部分，句柄归当前施法所有：Task 结束时
    /// 统一清理，也可以提前 <see cref="Dispose"/>。<see cref="Dispose"/> 是幂等的，取消后仍然
    /// 允许调用，<c>finally</c> 里的清理不该因为"已经取消了"而失败。
    /// </summary>
    public abstract class MagicVisualHandle : IDisposable
    {
        private MagicObject owner;
        private DrawNode node;
        private GameVector2 offset;
        private float opacity = 1f;
        private bool visible = true;
        private int order;

        internal MagicVisualHandle(MagicObject owner)
        {
            this.owner = owner;
        }

        public bool IsDisposed => owner == null;

        /// <summary>相对所属对象原点的偏移（地图单位）。</summary>
        public GameVector2 Offset
        {
            get => offset;
            set
            {
                offset = value;
                ApplyTransform();
            }
        }

        public float Opacity
        {
            get => opacity;
            set
            {
                opacity = value;
                if (node != null)
                {
                    node.Opacity = value;
                }
            }
        }

        public bool Visible
        {
            get => visible;
            set
            {
                visible = value;
                if (node != null)
                {
                    node.Visible = value;
                }
            }
        }

        /// <summary>同一对象内的绘制顺序，数值大的画在上面。</summary>
        public int Order
        {
            get => order;
            set
            {
                order = value;
                if (node != null)
                {
                    node.Order = value;
                }
            }
        }

        internal DrawNode Node => node;

        /// <summary>本件自己在 X 方向的缩放因子，与所属对象的缩放相乘。图片用它做水平翻转。</summary>
        internal virtual float LocalScaleX => 1f;

        internal void Bind(DrawNode created)
        {
            node = created;
            node.Opacity = opacity;
            node.Visible = visible;
            node.Order = order;
            ApplyTransform();
        }

        /// <summary>把所属对象的旋转和缩放，连同本件自己的偏移，一起写进节点变换。</summary>
        internal void ApplyTransform()
        {
            if (node == null || owner == null)
            {
                return;
            }

            node.Transform = new DrawTransform(
                new DrawPoint(offset.X, offset.Y),
                owner.Rotation,
                owner.Scale * LocalScaleX,
                owner.Scale);
        }

        /// <summary>每 Tick 推进一次；静态图片什么都不做，特效在这里走帧。</summary>
        internal virtual void Tick(MagicClock clock) { }

        /// <summary>释放非绘制资源（纹理租约等）。</summary>
        internal virtual void ReleaseResources() { }

        public void Dispose()
        {
            if (owner == null)
            {
                return;
            }

            MagicObject detachFrom = owner;
            owner = null;

            node?.Dispose();
            node = null;

            ReleaseResources();
            detachFrom.Detach(this);
        }
    }
}
