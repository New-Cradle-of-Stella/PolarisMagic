using System;
using System.Reflection;

namespace Polaris.Magic.Definitions
{
    /// <summary>
    /// 一种自定义魔法的完整静态描述：字符串 Id、原版需要的基本数值、自定义静态属性，
    /// 以及"每次施法造一个 code-behind 实例并取它那一个回调"的工厂。
    ///
    /// 这里没有图、没有阶段、没有逐 Tick 回调；运行期行为全部由 <see cref="CreateCallback"/>
    /// 返回的那一个 Task 决定。
    /// </summary>
    public sealed class MagicDefinition
    {
        private readonly Func<MagicTaskCallback> callbackFactory;

        internal MagicDefinition(
            string id,
            int mpCost,
            float castTime,
            float mpCrystalizeRatio,
            float neutralCrystalizeRatio,
            float prepareTime,
            float manaDrainLock,
            int projectilePower,
            float shotgunRatio,
            float superArmorTiredTime,
            MagicPropertySet properties,
            Assembly providerAssembly,
            Func<MagicTaskCallback> callbackFactory)
        {
            Id = id;
            MpCost = mpCost;
            CastTime = castTime;
            MpCrystalizeRatio = mpCrystalizeRatio;
            NeutralCrystalizeRatio = neutralCrystalizeRatio;
            PrepareTime = prepareTime;
            ManaDrainLock = manaDrainLock;
            ProjectilePower = projectilePower;
            ShotgunRatio = shotgunRatio;
            SuperArmorTiredTime = superArmorTiredTime;
            Properties = properties;
            ProviderAssembly = providerAssembly;
            this.callbackFactory = callbackFactory;
        }

        /// <summary>稳定字符串 Id，例如 <c>mymod.fireball</c>。数字 MGKIND 由 PolarisMagic 分配并持久化。</summary>
        public string Id { get; }

        public int MpCost { get; }

        public float CastTime { get; }

        public float MpCrystalizeRatio { get; }

        public float NeutralCrystalizeRatio { get; }

        public float PrepareTime { get; }

        public float ManaDrainLock { get; }

        public int ProjectilePower { get; }

        public float ShotgunRatio { get; }

        public float SuperArmorTiredTime { get; }

        public MagicPropertySet Properties { get; }

        /// <summary>声明这条定义的模组程序集。资源解析与错误归因都按它算账。</summary>
        public Assembly ProviderAssembly { get; }

        /// <summary>
        /// 造一个新的 code-behind 实例并取它的回调。每次正式施法各调一次，
        /// 并发施法之间的实例字段因此天然隔离。
        /// </summary>
        public MagicTaskCallback CreateCallback()
        {
            MagicTaskCallback callback = callbackFactory();
            if (callback == null)
            {
                throw new InvalidOperationException(
                    "The behaviour factory of magic '" + Id + "' returned no callback.");
            }

            return callback;
        }

        public override string ToString() => "MagicDefinition(" + Id + ")";
    }
}
