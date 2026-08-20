using System;
using System.Globalization;

namespace Polaris.Magic.Authoring
{
    /// <summary>自定义静态属性支持的类型。生成器按此决定 C# 属性类型与字面量写法。</summary>
    public enum MagicPropertyType
    {
        Int,
        Float,
        Bool,
        String,
    }

    /// <summary>
    /// 自定义静态属性的文本值 ↔ 类型化值的唯一转换规则。
    /// 全部使用 invariant culture：编辑器所在机器的区域设置不能影响文件内容。
    /// </summary>
    public static class MagicPropertyValue
    {
        /// <summary>该类型的零值文本，改型时用它重置 Value。</summary>
        public static string DefaultText(MagicPropertyType type)
        {
            switch (type)
            {
                case MagicPropertyType.Int: return "0";
                case MagicPropertyType.Float: return "0";
                case MagicPropertyType.Bool: return "false";
                case MagicPropertyType.String: return string.Empty;
                default: throw new ArgumentOutOfRangeException(nameof(type));
            }
        }

        public static bool TryParseType(string text, out MagicPropertyType type)
        {
            switch (text)
            {
                case "Int": type = MagicPropertyType.Int; return true;
                case "Float": type = MagicPropertyType.Float; return true;
                case "Bool": type = MagicPropertyType.Bool; return true;
                case "String": type = MagicPropertyType.String; return true;
                default: type = MagicPropertyType.Int; return false;
            }
        }

        public static string TypeToText(MagicPropertyType type) => type.ToString();

        /// <summary>该文本对该类型是否合法。String 恒合法（含空串）。</summary>
        public static bool IsValid(MagicPropertyType type, string text)
        {
            switch (type)
            {
                case MagicPropertyType.Int:
                    return TryParseInt(text, out _);
                case MagicPropertyType.Float:
                    return TryParseFloat(text, out _);
                case MagicPropertyType.Bool:
                    return TryParseBool(text, out _);
                case MagicPropertyType.String:
                    return text != null;
                default:
                    return false;
            }
        }

        public static bool TryParseInt(string text, out int value) =>
            int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);

        public static bool TryParseFloat(string text, out float value) =>
            float.TryParse(
                text,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent,
                CultureInfo.InvariantCulture,
                out value);

        /// <summary>只认小写 <c>true</c>/<c>false</c>：文件里不允许出现 <c>True</c> 这种第二种写法。</summary>
        public static bool TryParseBool(string text, out bool value)
        {
            if (string.Equals(text, "true", StringComparison.Ordinal))
            {
                value = true;
                return true;
            }

            if (string.Equals(text, "false", StringComparison.Ordinal))
            {
                value = false;
                return true;
            }

            value = false;
            return false;
        }

        public static string FormatInt(int value) => value.ToString(CultureInfo.InvariantCulture);

        /// <summary>R 格式：往返写出的浮点文本再读回来一定是同一个 float。</summary>
        public static string FormatFloat(float value) => value.ToString("R", CultureInfo.InvariantCulture);

        public static string FormatBool(bool value) => value ? "true" : "false";
    }
}
