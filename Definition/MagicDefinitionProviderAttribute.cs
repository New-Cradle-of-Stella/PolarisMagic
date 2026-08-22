using System;

namespace Polaris.Magic.Definitions
{
    /// <summary>
    /// 打在生成的提供器类型上；PolarisMagic 启动时扫描各模组程序集里带这个特性的类型，
    /// 调用它的 <c>BuildDefinition</c> 拿到 <see cref="MagicDefinition"/>。
    /// 用特性而不是让模组自己在 Awake 里调注册函数，是因为注册必须早于
    /// <c>MagicSelector.readBinaryFrom</c>，不受模组代码运行时机影响。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class MagicDefinitionProviderAttribute : Attribute
    {
        /// <summary>提供器类型上无参、返回 <see cref="MagicDefinition"/> 的静态方法名。</summary>
        public const string FactoryMethodName = "BuildDefinition";
    }
}
