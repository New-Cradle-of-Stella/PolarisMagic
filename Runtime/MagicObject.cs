using System;
using System.Collections.Generic;
using System.Reflection;
using Polaris.API;
using Polaris.Drawing;
using Polaris.Res;
using UnityEngine;
using XX;

namespace Polaris.Magic.Runtime
{
    /// <summary>
    /// 中间层的场景对象：一个地图坐标下的位置/朝向/缩放，外加若干挂载的图片与特效，归当前施法所有。
    /// Task 结束（完成、取消或异常）时统一清理，作者也可以提前 <see cref="Dispose"/>；绘制走
    /// PolarisCore 的 Drawing，生命周期设为 Map，即便中间层自己漏了一次清理，切图时也不会留下
    /// 绘制残留。
    /// </summary>
    public sealed class MagicObject : IDisposable, IMapDrawTarget
    {
        private readonly Assembly owner;
        private readonly List<MagicVisualHandle> attachments = new List<MagicVisualHandle>();
        private readonly string debugName;

        private DrawingSurface surface;
        private GameVector2 position;
        private float rotation;
        private float scale = 1f;
        private bool active = true;
        private bool disposed;

        internal MagicObject(Assembly owner, string debugName)
        {
            this.owner = owner;
            this.debugName = debugName;
        }

        public bool IsDisposed => disposed;

        /// <summary>地图坐标。</summary>
        public GameVector2 Position
        {
            get => position;
            set
            {
                position = value;
                if (surface != null)
                {
                    surface.Position = new DrawPoint(value.X, value.Y);
                }
            }
        }

        /// <summary>
        /// 每帧位移。中间层不会自动应用它——原版魔法的运动曲线千差万别，自动积分只会碍事。
        /// 想要最普通的匀速直线运动时调 <see cref="Advance"/>。
        /// </summary>
        public GameVector2 Velocity { get; set; }

        /// <summary>弧度。</summary>
        public float Rotation
        {
            get => rotation;
            set
            {
                rotation = value;
                ApplyTransforms();
            }
        }

        /// <summary>等比缩放。</summary>
        public float Scale
        {
            get => scale;
            set
            {
                scale = value;
                ApplyTransforms();
            }
        }

        /// <summary>关掉后整个对象不再绘制，但挂载项与资源租约都保留。</summary>
        public bool Active
        {
            get => active;
            set
            {
                active = value;
                if (surface != null)
                {
                    surface.Visible = value;
                }
            }
        }

        /// <summary>按当前速度推进一个 Tick 的位移。</summary>
        public void Advance(MagicClock clock)
        {
            if (clock != null)
            {
                Position = Position + Velocity * clock.DeltaFrames;
            }
        }

        /// <summary>挂一张静态图片。<paramref name="resourceId"/> 由 PolarisRes 解析，见 <see cref="MagicResourceId"/>。</summary>
        public MagicImageHandle AttachImage(string resourceId)
        {
            ThrowIfDisposed();

            IResourceLease<Texture2D> lease = MagicResourceId.LoadTexture(resourceId, owner);
            MagicImageHandle handle;
            try
            {
                handle = new MagicImageHandle(this, lease);
            }
            catch
            {
                lease.Dispose();
                throw;
            }

            handle.Bind(EnsureSurface().Add(handle.Draw));
            attachments.Add(handle);
            return handle;
        }

        /// <summary>挂一张由 PolarisRes 自动绑定的图片。图片生命周期仍归对应的资源字段所有。</summary>
        public MagicImageHandle AttachImage(MImage image)
        {
            ThrowIfDisposed();

            var handle = new MagicImageHandle(this, image);
            handle.Bind(EnsureSurface().Add(handle.Draw));
            attachments.Add(handle);
            return handle;
        }

        /// <summary>挂一个已登记的特效，见 <see cref="MagicEffects.Register"/>。</summary>
        public MagicEffectHandle AttachEffect(string effectId)
        {
            ThrowIfDisposed();

            MagicEffectSpec spec = MagicEffects.Require(effectId);
            IResourceLease<Texture2D> lease = MagicResourceId.LoadTexture(spec.TextureResourceId, owner);
            MagicEffectHandle handle;
            try
            {
                handle = new MagicEffectHandle(this, spec, lease);
            }
            catch
            {
                lease.Dispose();
                throw;
            }

            handle.Bind(EnsureSurface().Add(handle.Draw));
            attachments.Add(handle);
            return handle;
        }

        // ── Drawing 跟随协议 ────────────────

        /// <summary>
        /// <see cref="IMapDrawTarget"/> 的实现：让别人的地图 Drawing Surface 跟着这个魔法对象跑，
        /// 显式实现以免进入 <see cref="MagicObject"/> 自己的公共表面，作者读坐标继续用
        /// <see cref="Position"/>。已 <see cref="Dispose"/> 后返回 <c>false</c> 而不是汇报最后坐标，
        /// 因为跟随方往往比魔法对象活得久，需要明确的失效信号；<see cref="Active"/> 关掉只影响绘制，
        /// 不影响坐标有效性。
        /// </summary>
        bool IMapDrawTarget.TryGetMapPosition(out DrawPoint position)
        {
            if (disposed)
            {
                position = default;
                return false;
            }

            position = new DrawPoint(this.position.X, this.position.Y);
            return true;
        }

        internal void Tick(MagicClock clock)
        {
            // 倒序遍历：特效播完时可能在 Tick 里自释放，正序会把后面的挂载项跳过去。
            for (int i = attachments.Count - 1; i >= 0; i--)
            {
                attachments[i].Tick(clock);
            }
        }

        internal void Detach(MagicVisualHandle handle) => attachments.Remove(handle);

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;

            // 句柄的 Dispose 会回调 Detach，所以先拷一份再逐个释放。
            MagicVisualHandle[] snapshot = attachments.ToArray();
            attachments.Clear();
            foreach (MagicVisualHandle handle in snapshot)
            {
                handle.Dispose();
            }

            surface?.Dispose();
            surface = null;
        }

        private DrawingSurface EnsureSurface()
        {
            if (surface != null)
            {
                return surface;
            }

            surface = DrawingAPI.CreateSurface(new DrawingSurfaceOptions
            {
                Space = DrawSpace.Map,
                Plane = DrawPlane.WorldActors,
                Lifetime = DrawLifetime.Map,
                DebugName = debugName,
            });
            surface.Position = new DrawPoint(position.X, position.Y);
            surface.Visible = active;
            return surface;
        }

        private void ApplyTransforms()
        {
            foreach (MagicVisualHandle handle in attachments)
            {
                handle.ApplyTransform();
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(MagicObject));
            }
        }
    }
}
