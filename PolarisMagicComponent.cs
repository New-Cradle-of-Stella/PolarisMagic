using System.Collections.Generic;
using System.Reflection;
using Polaris.Components;
using Polaris.Magic.Game;
using Polaris.Magic.Runtime;

namespace Polaris.Magic
{
    /// <summary>
    /// 自定义魔法模块入口。
    ///
    /// 顺序有硬约束：注册（扫描提供器、分配数字 Id、登记名称）必须在 <see cref="Awake"/> 完成，
    /// 因为 <c>MKind</c> 注入和读档保护两条补丁都可能在 <see cref="Start"/> 之前就被触发，
    /// 而它们只有在注册表就绪时才有东西可注入。
    /// </summary>
    public sealed class PolarisMagicComponent : PolarisComponent
    {
        public override string Id => "PolarisMagic";

        public override int Order => 500;

        public override void Awake()
        {
            MagicRuntimeHost.Initialize();
        }

        public override void Start()
        {
            PolarisAPI.Errors.Guard(
                () => MagicRegistry.Discover(PolarisAPI.Paths.StateDir, CandidateAssemblies()),
                "discovering custom magic definitions");

            // MKind 表通常在这之前就加载过一次了；补丁的 Postfix 只在真正重新加载时才触发。
            PolarisAPI.Errors.Guard(MagicKindInjector.Inject, "injecting custom MKind entries");
        }

        public override void Update()
        {
            MagicRuntimeHost.Update();
        }

        public override void Shutdown()
        {
            MagicRuntimeHost.Shutdown();
        }

        /// <summary>
        /// 提供器可能住在两种程序集里：把魔法做成 BepInEx 插件的模组（PluginAssemblies），
        /// 或者做成 Polaris 组件 DLL 的模组（ComponentAssemblies）。两边都扫，去重靠字典。
        /// </summary>
        private static IEnumerable<Assembly> CandidateAssemblies()
        {
            var seen = new HashSet<Assembly>();

            foreach (Assembly assembly in PolarisAPI.Modules.ComponentAssemblies)
            {
                if (seen.Add(assembly))
                {
                    yield return assembly;
                }
            }

            foreach (Assembly assembly in PolarisAPI.Modules.PluginAssemblies)
            {
                if (seen.Add(assembly))
                {
                    yield return assembly;
                }
            }
        }
    }
}
