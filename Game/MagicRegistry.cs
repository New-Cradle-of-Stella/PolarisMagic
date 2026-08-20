using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using nel;
using Polaris.Magic.Definitions;
using Polaris.Magic.Runtime;

namespace Polaris.Magic.Game
{
    /// <summary>
    /// 自定义魔法的注册表。启动时扫描一次，之后只读。
    ///
    /// 映射文件无法读取时拒绝整批注册；单个提供器的定义有误时只跳过该定义，
    /// 不影响其它彼此独立的魔法。
    /// </summary>
    internal static class MagicRegistry
    {
        private static readonly Dictionary<MGKIND, MagicRegistration> ByKind =
            new Dictionary<MGKIND, MagicRegistration>();

        private static readonly Dictionary<string, MagicRegistration> ById =
            new Dictionary<string, MagicRegistration>(StringComparer.Ordinal);

        private static MagicKindAllocator allocator;

        internal static bool IsReady { get; private set; }

        internal static IReadOnlyCollection<MagicRegistration> All => ByKind.Values;

        internal static bool TryGet(MGKIND kind, out MagicRegistration registration) =>
            ByKind.TryGetValue(kind, out registration);

        internal static bool TryGet(string magicId, out MagicRegistration registration) =>
            ById.TryGetValue(magicId, out registration);

        internal static bool IsCustom(MGKIND kind) => ByKind.ContainsKey(kind);

        /// <summary>
        /// 扫描全部已加载程序集里带 <see cref="MagicDefinitionProviderAttribute"/> 的提供器，
        /// 建立定义、分配数字 Id 并登记名称。
        ///
        /// 一个提供器坏掉只丢掉它自己那一条，其余魔法照常注册；映射文件整体无法读取时才放弃本轮注册。
        /// </summary>
        internal static void Discover(string stateDirectory, IEnumerable<Assembly> assemblies)
        {
            if (IsReady)
            {
                return;
            }

            allocator = new MagicKindAllocator(stateDirectory);
            try
            {
                allocator.Load();
            }
            catch (Exception ex)
            {
                MagicLog.Error("Failed to read the magic id map; refusing to register anything: " + ex.Message);
                return;
            }

            foreach (Assembly assembly in assemblies)
            {
                foreach (Type type in SafeTypes(assembly))
                {
                    if (!HasProviderAttribute(type))
                    {
                        continue;
                    }

                    try
                    {
                        RegisterProvider(type);
                    }
                    catch (Exception ex)
                    {
                        PolarisAPI.Errors.Report(ex, "registering the magic provider " + type.FullName, assembly);
                        MagicLog.Error(
                            "The magic declared by " + type.FullName + " is unavailable this session: " + ex.Message);
                    }
                }
            }

            try
            {
                allocator.Save();
            }
            catch (Exception ex)
            {
                MagicLog.Error("Failed to persist the magic id map: " + ex.Message);
            }

            IsReady = true;
            MagicLog.Info("Registered " + ByKind.Count + " custom magic definition(s).");
        }

        private static void RegisterProvider(Type providerType)
        {
            MethodInfo factory = providerType.GetMethod(
                MagicDefinitionProviderAttribute.FactoryMethodName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            if (factory == null || factory.GetParameters().Length != 0
                || !typeof(MagicDefinition).IsAssignableFrom(factory.ReturnType))
            {
                throw new MagicDefinitionException(
                    providerType.FullName + " must declare a parameterless static " +
                    MagicDefinitionProviderAttribute.FactoryMethodName + "() returning a MagicDefinition.");
            }

            var definition = (MagicDefinition)factory.Invoke(null, null);
            if (definition == null)
            {
                throw new MagicDefinitionException(
                    providerType.FullName + "." + MagicDefinitionProviderAttribute.FactoryMethodName + "() returned null.");
            }

            if (ById.TryGetValue(definition.Id, out MagicRegistration existing))
            {
                throw new MagicDefinitionException(
                    "Magic id '" + definition.Id + "' is declared twice: by " +
                    existing.Definition.ProviderAssembly.GetName().Name + " and by " +
                    definition.ProviderAssembly.GetName().Name + ".");
            }

            string enumName = ToEnumName(definition.Id);
            if (Enum.IsDefined(typeof(MGKIND), enumName))
            {
                throw new MagicDefinitionException(
                    "Magic id '" + definition.Id + "' maps to the existing vanilla enum name '" + enumName + "'.");
            }

            foreach (MagicRegistration registered in ByKind.Values)
            {
                if (string.Equals(registered.EnumName, enumName, StringComparison.Ordinal))
                {
                    throw new MagicDefinitionException(
                        "Magic ids '" + registered.Definition.Id + "' and '" + definition.Id +
                        "' both map to the enum name '" + enumName + "'.");
                }
            }

            MGKIND kind = allocator.Resolve(definition.Id);
            if (ByKind.ContainsKey(kind))
            {
                throw new MagicDefinitionException(
                    "Numeric id " + (int)kind + " is already in use; the magic id map is inconsistent.");
            }

            var registration = new MagicRegistration(definition, kind, enumName);
            ByKind.Add(kind, registration);
            ById.Add(definition.Id, registration);
            MagicNameBinding.Register(registration);
        }

        /// <summary>
        /// <c>mymod.fire_ball</c> → <c>MYMOD_FIRE_BALL</c>。原版的事件命令与菜单都用大写下划线名，
        /// 点号在那些解析器里没有意义。
        /// </summary>
        private static string ToEnumName(string magicId) =>
            magicId.Replace('.', '_').ToUpper(CultureInfo.InvariantCulture);

        private static bool HasProviderAttribute(Type type)
        {
            try
            {
                return type.IsClass
                    && type.GetCustomAttribute<MagicDefinitionProviderAttribute>(false) != null;
            }
            catch (Exception)
            {
                // 特性所在程序集缺失时读取特性会抛；这种类型直接跳过。
                return false;
            }
        }

        private static IEnumerable<Type> SafeTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                var loaded = new List<Type>();
                foreach (Type type in ex.Types)
                {
                    if (type != null)
                    {
                        loaded.Add(type);
                    }
                }

                return loaded;
            }
            catch (Exception)
            {
                return Array.Empty<Type>();
            }
        }
    }
}
