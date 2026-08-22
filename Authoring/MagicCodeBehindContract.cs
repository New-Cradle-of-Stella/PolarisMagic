using System.Text;

namespace Polaris.Magic.Authoring
{
    /// <summary>
    /// <c>.pmagic.cs</c> 里作者回调的签名契约，以及首次创建时写入的骨架文本；生成的
    /// <c>.pmagic.g.cs</c> 与工具侧都靠同一份签名判断方法是否有效（PMAG1302），两边必须字字一致。
    /// 同样只依赖 BCL，因为这个文件会被 PolarisTools 链接进 VS 扩展编译。
    /// </summary>
    public static class MagicCodeBehindContract
    {
        /// <summary>运行时命名空间；骨架和生成文件都 using 它。</summary>
        public const string RuntimeNamespace = "Polaris.Magic.Runtime";

        /// <summary>一次施法只调用一次的作者回调。</summary>
        public const string RunMethodName = "RunAsync";

        public const string ReturnTypeName = "Task";

        public const string ContextTypeName = "MagicRuntimeContext";

        public const string ContextParameterName = "context";

        public const string CancellationTypeName = "CancellationToken";

        public const string CancellationParameterName = "cancellationToken";

        /// <summary>作者回调的可见性。生成的 partial 另一半在同一个类里，因此 private 就够。</summary>
        public const string Accessibility = "private";

        /// <summary>无命名空间时生成代码落到的兜底命名空间。</summary>
        public const string FallbackNamespace = "Polaris.Generated";

        /// <summary>
        /// 人可读的签名，用于诊断文本。不写 <c>async</c>：那是实现细节，签名检查也不看它——
        /// 作者开始 <c>await</c> 之后自然会加上，那不算签名变了。
        /// </summary>
        public static string SignatureText =>
            Accessibility + " " + ReturnTypeName + " " + RunMethodName + "(" +
            ContextTypeName + " " + ContextParameterName + ", " +
            CancellationTypeName + " " + CancellationParameterName + ")";

        /// <summary>
        /// 首次创建 <c>.pmagic.cs</c> 时写入的骨架。只在文件不存在时写一次，之后绝不整体覆盖。
        /// 换行固定 CRLF，与生成器输出保持一致。
        /// </summary>
        public static string BuildSkeleton(string namespaceName, string className)
        {
            var text = new StringBuilder();
            text.Append("using System.Threading;\r\n");
            text.Append("using System.Threading.Tasks;\r\n");
            text.Append("using ").Append(RuntimeNamespace).Append(";\r\n");
            text.Append("\r\n");
            text.Append("namespace ").Append(namespaceName).Append("\r\n");
            text.Append("{\r\n");
            text.Append("    public sealed partial class ").Append(className).Append("\r\n");
            text.Append("    {\r\n");
            text.Append(BuildRunMethod("        "));
            text.Append("    }\r\n");
            text.Append("}\r\n");
            return text.ToString();
        }

        /// <summary>
        /// 作者回调的骨架：只有一个空白 Task，不写示例逻辑，避免作者需要先读懂再删掉。
        /// 不写 <c>async</c>，因为空方法体会报 CS1998；作者写下第一个 <c>await</c> 时编译器会提示补上。
        /// </summary>
        public static string BuildRunMethod(string indent)
        {
            var text = new StringBuilder();
            text.Append(indent).Append("/// <summary>\r\n");
            text.Append(indent).Append("/// 一次施法只调用一次。这个 Task 存活期间魔法就在运行；它不管以什么方式退出\r\n");
            text.Append(indent).Append("/// （正常完成、取消、抛异常）都代表魔法立即结束，收尾由 PolarisMagic 负责。\r\n");
            text.Append(indent).Append("/// </summary>\r\n");
            text.Append(indent).Append(Accessibility).Append(" ").Append(ReturnTypeName).Append(" ")
                .Append(RunMethodName).Append("(\r\n");
            text.Append(indent).Append("    ").Append(ContextTypeName).Append(" ").Append(ContextParameterName).Append(",\r\n");
            text.Append(indent).Append("    ").Append(CancellationTypeName).Append(" ").Append(CancellationParameterName).Append(")\r\n");
            text.Append(indent).Append("{\r\n");
            text.Append(indent).Append("    return ").Append(ReturnTypeName).Append(".CompletedTask;\r\n");
            text.Append(indent).Append("}\r\n");
            return text.ToString();
        }
    }
}
