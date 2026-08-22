using System;
using System.Threading;
using System.Threading.Tasks;
using Polaris.API;
using Polaris.Magic.Definitions;

namespace Polaris.Magic.Runtime
{
    /// <summary>
    /// 作者能看见的全部运行期入口，所有成员都是中间层包装：这里没有 <c>MagicItem</c>、
    /// <c>MGContainer</c>、holder、<c>phase</c> 或 <c>t</c>，也没有拿到原版对象的逃生口。
    /// 每次施法一个 Context 实例，与 Token 和 code-behind 实例一一对应，并发施法之间互相隔离。
    /// </summary>
    public sealed class MagicRuntimeContext
    {
        private readonly MagicRuntimeInstance instance;

        internal MagicRuntimeContext(
            MagicRuntimeInstance instance,
            MagicDefinition definition,
            int instanceId,
            MagicClock clock,
            MagicEntity self,
            GameCharacter caster,
            MagicWorldServices world,
            MagicApi magic)
        {
            this.instance = instance;
            Definition = definition;
            InstanceId = instanceId;
            Clock = clock;
            Self = self;
            Caster = caster;
            World = world;
            Magic = magic;
        }

        /// <summary>本次施法用的静态定义，含 <c>.pmagic</c> 里的全部参数。</summary>
        public MagicDefinition Definition { get; }

        /// <summary>本次施法在本进程内唯一的序号，用于日志对号。</summary>
        public int InstanceId { get; }

        public MagicClock Clock { get; }

        /// <summary>魔法实体本身。</summary>
        public MagicEntity Self { get; }

        /// <summary>施法者；施法者已离场时为 <c>null</c>。</summary>
        public GameCharacter Caster { get; }

        /// <summary>当前玩家；不在场时为 <c>null</c>。</summary>
        public GamePlayer Player => PolarisAPI.Game.World.CurrentPlayer;

        /// <summary>当前地图；没有加载地图时为 <c>null</c>。</summary>
        public GameMap Map => PolarisAPI.Game.World.CurrentMap;

        public MagicWorldServices World { get; }

        /// <summary>创建魔法对象、挂载图片与特效。</summary>
        public MagicApi Magic { get; }

        /// <summary>等到下一个 Tick。续体在游戏主线程执行。</summary>
        public ValueTask NextTickAsync(CancellationToken cancellationToken = default) =>
            instance.Wait(MagicWaitKind.NextTick, 0f, null, cancellationToken);

        /// <summary>
        /// 等待若干帧。单位与 <see cref="MagicClock.DeltaFrames"/> 一致，因此时停/慢放时会自然拉长；
        /// <paramref name="frames"/> 不为正时等价于 <see cref="NextTickAsync"/>。
        /// </summary>
        public ValueTask DelayFramesAsync(float frames, CancellationToken cancellationToken = default)
        {
            if (!(frames > 0f))
            {
                return NextTickAsync(cancellationToken);
            }

            return instance.Wait(MagicWaitKind.Frames, Clock.ElapsedFrames + frames, null, cancellationToken);
        }

        /// <summary>
        /// 等到谓词为真。谓词每 Tick 在游戏主线程求值一次，因此可以直接读游戏状态；
        /// 但不要在里面做重活或产生副作用。
        /// </summary>
        public ValueTask WaitUntilAsync(Func<bool> predicate, CancellationToken cancellationToken = default)
        {
            if (predicate == null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }

            return instance.Wait(MagicWaitKind.Predicate, 0f, predicate, cancellationToken);
        }
    }
}
