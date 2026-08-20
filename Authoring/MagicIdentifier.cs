using System;
using System.Text.RegularExpressions;

namespace Polaris.Magic.Authoring
{
    /// <summary>
    /// 名字规则的唯一出处：类名、命名空间段、自定义属性名与字符串魔法 Id。
    /// 编辑器、生成器、游戏侧注册期共用同一批正则，不允许各自手写。
    /// </summary>
    public static class MagicIdentifier
    {
        private static readonly Regex CSharpIdentifier =
            new Regex(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant);

        /// <summary>字符串 Id 至少两段，用点分隔；每段本身是合法 C# 标识符。</summary>
        private static readonly Regex MagicId =
            new Regex(@"^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)+$", RegexOptions.CultureInvariant);

        /// <summary>C# 关键字与上下文关键字；类名和命名空间段撞上就必须拒绝，否则生成的文件编译不过。</summary>
        private static readonly string[] ReservedWords =
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
            "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
            "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
            "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
            "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
            "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed",
            "short", "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw",
            "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using",
            "virtual", "void", "volatile", "while",
        };

        public static bool IsReservedWord(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            foreach (string reserved in ReservedWords)
            {
                if (string.Equals(reserved, value, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>合法 C# 标识符，且不是关键字。用于类名、自定义属性名。</summary>
        public static bool IsValidName(string value) =>
            !string.IsNullOrEmpty(value) && CSharpIdentifier.IsMatch(value) && !IsReservedWord(value);

        /// <summary>点分命名空间，每段都要过 <see cref="IsValidName"/>。</summary>
        public static bool IsValidNamespace(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            foreach (string segment in value.Split('.'))
            {
                if (!IsValidName(segment))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>字符串魔法 Id：至少两段的点分名，例如 <c>local.magic</c>。</summary>
        public static bool IsValidMagicId(string value) =>
            !string.IsNullOrEmpty(value) && MagicId.IsMatch(value);
    }
}
