using System;
using System.Reflection;
using Polaris.Res;
using UnityEngine;

namespace Polaris.Magic.Runtime
{
    /// <summary>资源 Id 解析不出东西时抛这个；只结束当前施法实例，不影响别的魔法。</summary>
    public sealed class MagicResourceException : Exception
    {
        public MagicResourceException(string message, Exception inner = null) : base(message, inner) { }
    }

    /// <summary>
    /// <c>AttachImage</c>/特效规格里那个 <c>resourceId</c> 的解析规则。
    ///
    /// 两种写法：
    /// <list type="bullet">
    ///   <item><c>"effects/fireball.png"</c>——挂在声明这条魔法的模组名下（程序集简名即 modId）。</item>
    ///   <item><c>"othermod:shared/spark.png"</c>——显式指定 modId，用于借用别的模组已挂载的资源。</item>
    /// </list>
    /// 真正的挂载点、导入设置与缓存都归 PolarisRes；这里只做拆分和错误信息。
    /// </summary>
    internal static class MagicResourceId
    {
        internal static IResourceLease<Texture2D> LoadTexture(string resourceId, Assembly owner)
        {
            if (string.IsNullOrEmpty(resourceId))
            {
                throw new MagicResourceException("The resource id is empty.");
            }

            Split(resourceId, owner, out string modId, out string path);

            try
            {
                return ResAPI.For(modId).Texture(path);
            }
            catch (Exception ex)
            {
                throw new MagicResourceException(
                    "Failed to load the magic texture '" + path + "' of mod '" + modId + "'.", ex);
            }
        }

        internal static void Split(string resourceId, Assembly owner, out string modId, out string path)
        {
            int separator = resourceId.IndexOf(':');
            if (separator < 0)
            {
                modId = owner?.GetName().Name
                    ?? throw new MagicResourceException(
                        "The resource id '" + resourceId + "' has no mod prefix and the owning assembly is unknown.");
                path = resourceId;
                return;
            }

            modId = resourceId.Substring(0, separator);
            path = resourceId.Substring(separator + 1);

            if (modId.Length == 0 || path.Length == 0)
            {
                throw new MagicResourceException(
                    "The resource id '" + resourceId + "' must be written as 'modId:path' or just 'path'.");
            }
        }
    }
}
