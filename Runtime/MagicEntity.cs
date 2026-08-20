using nel;
using Polaris.API;
using Polaris.Drawing;

namespace Polaris.Magic.Runtime
{
    /// <summary>
    /// 当前施法的魔法实体，是原版 <c>MagicItem</c> 的中间层包装。
    ///
    /// 作者拿不到 <c>MagicItem</c>、<c>phase</c>、<c>t</c> 或 holder：那些是对象池化的、会被下一代施法复用的
    /// 可变状态，直接暴露出去等于把"魔法结束后还拿着旧引用"这个坑留给每一个模组。这里的每个访问器都
    /// 先验一遍身份（同一个对象且 <c>id</c> 没变且还没被 kill），实体已经回池时读操作返回最后已知值、
    /// 写操作静默丢弃。
    /// </summary>
    public sealed class MagicEntity : IMapDrawTarget
    {
        private readonly MagicItem item;
        private readonly int itemId;

        private GameVector2 lastPosition;
        private GameVector2 lastVelocity;

        internal MagicEntity(MagicItem item)
        {
            this.item = item;
            itemId = item.id;
            lastPosition = new GameVector2(item.sx, item.sy);
            lastVelocity = new GameVector2(item.dx, item.dy);
        }

        /// <summary>原版实体是否还是"我这次施法"的那一个。回池、换代或已 kill 后为 false。</summary>
        public bool IsAlive => item != null && item.id == itemId && !item.killed;

        /// <summary>地图坐标。</summary>
        public GameVector2 Position
        {
            get
            {
                if (IsAlive)
                {
                    lastPosition = new GameVector2(item.sx, item.sy);
                }

                return lastPosition;
            }
            set
            {
                lastPosition = value;
                if (IsAlive)
                {
                    item.sx = value.X;
                    item.sy = value.Y;
                }
            }
        }

        /// <summary>每帧位移，单位与 <see cref="MagicClock.DeltaFrames"/> 配套。</summary>
        public GameVector2 Velocity
        {
            get
            {
                if (IsAlive)
                {
                    lastVelocity = new GameVector2(item.dx, item.dy);
                }

                return lastVelocity;
            }
            set
            {
                lastVelocity = value;
                if (IsAlive)
                {
                    item.dx = value.X;
                    item.dy = value.Y;
                }
            }
        }

        /// <summary>施法朝向。</summary>
        public bool IsFacingRight => IsAlive && item.is_right;

        /// <summary>原版为这次施法算出的瞄准点（地图坐标）。</summary>
        public GameVector2 AimPosition => IsAlive ? (GameVector2)item.PosA : lastPosition;

        // ── Drawing 跟随协议 ────────────────

        /// <summary>
        /// <see cref="IMapDrawTarget"/> 的实现：让地图 Drawing Surface 跟着原版魔法实体本体跑。
        /// 显式实现，不进 <see cref="MagicEntity"/> 自己的公共表面；作者读坐标继续用
        /// <see cref="Position"/>。
        ///
        /// 实体回池、换代或已 kill（<see cref="IsAlive"/> 为 false）时返回 <c>false</c>，
        /// 由跟随方按 <see cref="MapTargetLostBehavior"/> 处置。这里刻意不退回
        /// <see cref="Position"/> 的"最后已知值"语义：读属性给旧值只是让作者的算术不炸，
        /// 而跟随方需要的是"目标没了"这个信号，否则绘制会永远钉在实体死掉的那一点上。
        /// </summary>
        bool IMapDrawTarget.TryGetMapPosition(out DrawPoint position)
        {
            if (!IsAlive)
            {
                position = default;
                return false;
            }

            position = new DrawPoint(item.sx, item.sy);
            return true;
        }

        /// <summary>把实体按当前速度推进一个 Tick。等价于 <c>Position += Velocity * clock.DeltaFrames</c>。</summary>
        public void Integrate(MagicClock clock)
        {
            if (clock == null)
            {
                return;
            }

            Position = Position + Velocity * clock.DeltaFrames;
        }
    }
}
