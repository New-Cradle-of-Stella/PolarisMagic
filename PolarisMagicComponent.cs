using Polaris.Components;

namespace Polaris.Magic
{
    /// <summary>自定义魔法模块入口；具体魔法能力在后续实现中挂接到该组件生命周期。</summary>
    public sealed class PolarisMagicComponent : PolarisComponent
    {
        public override string Id => "PolarisMagic";
        public override int Order => 500;
    }
}
