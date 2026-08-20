using System;
using System.Collections.Generic;
using System.Reflection;
using Polaris.Magic.Authoring;

namespace Polaris.Magic.Definitions
{
    /// <summary>
    /// 一条魔法定义不合法时抛出的启动期错误。
    /// </summary>
    public sealed class MagicDefinitionException : Exception
    {
        public MagicDefinitionException(string message) : base(message) { }
    }

    /// <summary>
    /// 生成代码唯一的组装入口。生成器只调这几个方法，因此"生成文本长什么样"和"运行时怎么建对象"
    /// 之间只有这一处耦合；以后加字段只要同时改这里和发射器，不必改已生成的旧文件之外的东西。
    /// </summary>
    public sealed class MagicDefinitionBuilder
    {
        private readonly string id;
        private readonly List<MagicPropertyEntry> properties = new List<MagicPropertyEntry>();
        private readonly HashSet<string> propertyNames = new HashSet<string>(StringComparer.Ordinal);

        private int mpCost;
        private float castTime;
        private float mpCrystalizeRatio;
        private float neutralCrystalizeRatio;
        private float prepareTime = MagicDefinitionDocument.DefaultPrepareTime;
        private float manaDrainLock = MagicDefinitionDocument.DefaultManaDrainLock;
        private int projectilePower = MagicDefinitionDocument.DefaultProjectilePower;
        private float shotgunRatio = MagicDefinitionDocument.DefaultShotgunRatio;
        private float superArmorTiredTime = MagicDefinitionDocument.DefaultSuperArmorTiredTime;
        private Assembly providerAssembly;
        private Func<MagicTaskCallback> behaviorFactory;

        public MagicDefinitionBuilder(string id)
        {
            this.id = id;
        }

        public MagicDefinitionBuilder SetProviderAssembly(Assembly assembly)
        {
            providerAssembly = assembly;
            return this;
        }

        /// <summary>四个必需的基本数值。生成器按 <c>.pmagic</c> 的字段顺序传入。</summary>
        public MagicDefinitionBuilder SetCost(
            int mpCost,
            float castTime,
            float mpCrystalizeRatio,
            float neutralCrystalizeRatio)
        {
            this.mpCost = mpCost;
            this.castTime = castTime;
            this.mpCrystalizeRatio = mpCrystalizeRatio;
            this.neutralCrystalizeRatio = neutralCrystalizeRatio;
            return this;
        }

        /// <summary>五个可选的基本数值；生成文件总是写出，因此这里不再区分"没写"和"写了默认值"。</summary>
        public MagicDefinitionBuilder SetTuning(
            float prepareTime,
            float manaDrainLock,
            int projectilePower,
            float shotgunRatio,
            float superArmorTiredTime)
        {
            this.prepareTime = prepareTime;
            this.manaDrainLock = manaDrainLock;
            this.projectilePower = projectilePower;
            this.shotgunRatio = shotgunRatio;
            this.superArmorTiredTime = superArmorTiredTime;
            return this;
        }

        public MagicDefinitionBuilder AddProperty(string name, MagicPropertyType type, object value)
        {
            if (!MagicIdentifier.IsValidName(name))
            {
                throw new MagicDefinitionException(
                    "Magic '" + id + "' declares the custom property '" + name + "', which is not a valid C# name.");
            }

            if (!IsPropertyValue(type, value))
            {
                throw new MagicDefinitionException(
                    "Magic '" + id + "' gives the custom property '" + name +
                    "' a value that does not match " + type + ".");
            }

            if (!propertyNames.Add(name))
            {
                throw new MagicDefinitionException(
                    "Magic '" + id + "' declares the custom property '" + name + "' more than once.");
            }

            properties.Add(new MagicPropertyEntry(name, type, value));
            return this;
        }

        /// <summary>每次施法造一个新的 code-behind 实例，并交出它那一个回调。</summary>
        public MagicDefinitionBuilder SetBehaviorFactory(Func<MagicTaskCallback> factory)
        {
            behaviorFactory = factory;
            return this;
        }

        public MagicDefinition Build()
        {
            if (!MagicIdentifier.IsValidMagicId(id))
            {
                throw new MagicDefinitionException(
                    "'" + id + "' is not a valid magic id; use at least two dot-separated identifier segments.");
            }

            if (behaviorFactory == null)
            {
                throw new MagicDefinitionException("Magic '" + id + "' has no behaviour factory.");
            }

            if (providerAssembly == null)
            {
                throw new MagicDefinitionException("Magic '" + id + "' has no provider assembly.");
            }

            if (mpCost < 0 || !IsFiniteNonNegative(castTime) || !IsFiniteNonNegative(prepareTime)
                || !IsFiniteNonNegative(manaDrainLock) || projectilePower < 0
                || !IsFiniteNonNegative(shotgunRatio) || !IsFiniteNonNegative(superArmorTiredTime))
            {
                throw new MagicDefinitionException("Magic '" + id + "' has an invalid base value.");
            }

            if (!IsRatio(mpCrystalizeRatio) || !IsRatio(neutralCrystalizeRatio))
            {
                throw new MagicDefinitionException("Magic '" + id + "' has a crystalize ratio outside 0..1.");
            }

            return new MagicDefinition(
                id,
                mpCost,
                castTime,
                mpCrystalizeRatio,
                neutralCrystalizeRatio,
                prepareTime,
                manaDrainLock,
                projectilePower,
                shotgunRatio,
                superArmorTiredTime,
                properties.Count == 0 ? MagicPropertySet.Empty : new MagicPropertySet(properties),
                providerAssembly,
                behaviorFactory);
        }

        private static bool IsFiniteNonNegative(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;

        private static bool IsRatio(float value) =>
            !float.IsNaN(value) && value >= 0f && value <= 1f;

        private static bool IsPropertyValue(MagicPropertyType type, object value)
        {
            switch (type)
            {
                case MagicPropertyType.Int:
                    return value is int;
                case MagicPropertyType.Float:
                    return value is float number && !float.IsNaN(number) && !float.IsInfinity(number);
                case MagicPropertyType.Bool:
                    return value is bool;
                case MagicPropertyType.String:
                    return value is string;
                default:
                    return false;
            }
        }
    }
}
