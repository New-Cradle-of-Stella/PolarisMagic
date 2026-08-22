using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Polaris.Magic.Authoring
{
    /// <summary>
    /// <c>.pmagic</c> 读不下去时抛这个：XML 坏了、不是 <c>Magic</c> 根、有认不出来的元素/属性、
    /// 数值不合法，或格式版本过新。只有这一个异常，没有错误码体系，因为 <c>.pmagic</c>
    /// 只是一张十来个字段的属性表，不是编程语言。
    /// </summary>
    public sealed class MagicFormatException : Exception
    {
        public MagicFormatException(string message) : base(message) { }

        public MagicFormatException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>一条自定义静态属性。行顺序即生成的属性顺序。</summary>
    public sealed class MagicCustomProperty
    {
        public string Name { get; set; } = string.Empty;

        public MagicPropertyType Type { get; set; } = MagicPropertyType.Int;

        public string Value { get; set; } = "0";

        public MagicCustomProperty Clone() => new MagicCustomProperty
        {
            Name = Name,
            Type = Type,
            Value = Value,
        };
    }

    /// <summary>
    /// <c>.pmagic</c> 的内存模型与规范 XML 读写；因为被 PolarisTools 链接进 VS 扩展编译，只能依赖
    /// BCL，不引用 Unity、BepInEx、Harmony、原版程序集或 Visual Studio、WPF。
    /// 编辑器存盘与生成器读取共用同一套解析和写法，缺属性时取默认值而非报错，只有真正读不下去时
    /// 才抛 <see cref="MagicFormatException"/>。
    /// </summary>
    public sealed class MagicDefinitionDocument
    {
        public const string RootElementName = "Magic";
        public const string BaseElementName = "Base";
        public const string PropertiesElementName = "Properties";
        public const string PropertyElementName = "Property";

        /// <summary>可选属性未写出时的取值，与原版 <c>MKind</c> 的字段默认值一致。</summary>
        public const float DefaultPrepareTime = 14f;
        public const float DefaultManaDrainLock = 5f;
        public const int DefaultProjectilePower = 100;
        public const float DefaultShotgunRatio = 1.5f;
        public const float DefaultSuperArmorTiredTime = 0f;

        /// <summary>模板新建时写入的占位 Id；作者应在定义编辑器里改成模组自己的稳定 Id。</summary>
        public const string TemplateId = "local.magic";

        public int Version { get; set; } = MagicFormatVersion.Current;

        public string Id { get; set; } = string.Empty;

        // ── 必需的基本属性：原版 MDAT 每次初始化都会重置它们，没有“沿用上一代”的默认值可依赖，
        //    所以编辑器给它们标星号提示作者填。

        public int MpCost { get; set; }

        public float CastTime { get; set; }

        public float MpCrystalizeRatio { get; set; }

        public float NeutralCrystalizeRatio { get; set; }

        // ── 可选的基本属性：缺省时取原版 MKind 的字段默认值，但规范写法总是写出。

        public float PrepareTime { get; set; } = DefaultPrepareTime;

        public float ManaDrainLock { get; set; } = DefaultManaDrainLock;

        public int ProjectilePower { get; set; } = DefaultProjectilePower;

        public float ShotgunRatio { get; set; } = DefaultShotgunRatio;

        public float SuperArmorTiredTime { get; set; } = DefaultSuperArmorTiredTime;

        public List<MagicCustomProperty> Properties { get; } = new List<MagicCustomProperty>();

        /// <summary>模板/新建文档：必需属性是零值，可选属性是默认值。</summary>
        public static MagicDefinitionDocument CreateTemplate() => new MagicDefinitionDocument
        {
            Version = MagicFormatVersion.Current,
            Id = TemplateId,
        };

        // ==================== 读 ====================

        /// <summary>解析 <c>.pmagic</c>。读不下去时抛 <see cref="MagicFormatException"/>。</summary>
        public static MagicDefinitionDocument Parse(string xml)
        {
            XDocument xdoc;
            try
            {
                var settings = new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    CloseInput = true,
                };

                using (var reader = XmlReader.Create(new StringReader(xml ?? string.Empty), settings))
                {
                    xdoc = XDocument.Load(reader);
                }
            }
            catch (Exception ex)
            {
                throw new MagicFormatException("This is not well-formed XML: " + ex.Message, ex);
            }

            XElement root = xdoc.Root;
            if (root == null || root.Name.LocalName != RootElementName || root.Name.NamespaceName.Length != 0)
            {
                throw new MagicFormatException("The root element must be <" + RootElementName + ">.");
            }

            var document = new MagicDefinitionDocument();
            document.ReadRoot(root);
            document.ReadBase(root);
            document.ReadProperties(root);
            document.RejectUnknownElements(root);
            return document;
        }

        private void ReadRoot(XElement root)
        {
            foreach (XAttribute attribute in root.Attributes())
            {
                switch (attribute.Name.LocalName)
                {
                    case "Version":
                        Version = ReadInt(attribute);
                        break;
                    case "Id":
                        Id = attribute.Value;
                        break;
                    default:
                        throw Unknown(attribute.Name.LocalName, RootElementName);
                }
            }

            if (Version > MagicFormatVersion.Current)
            {
                throw new MagicFormatException(
                    "Format version " + Version + " is newer than this tooling supports (" +
                    MagicFormatVersion.Current + ").");
            }

            if (Version < MagicFormatVersion.Minimum)
            {
                throw new MagicFormatException(
                    "Format version " + Version + " is older than the minimum supported version (" +
                    MagicFormatVersion.Minimum + ").");
            }
        }

        private void ReadBase(XElement root)
        {
            XElement element = root.Element(BaseElementName);
            if (element == null)
            {
                return;
            }

            foreach (XAttribute attribute in element.Attributes())
            {
                switch (attribute.Name.LocalName)
                {
                    case "MpCost": MpCost = ReadInt(attribute); break;
                    case "ProjectilePower": ProjectilePower = ReadInt(attribute); break;
                    case "CastTime": CastTime = ReadFloat(attribute); break;
                    case "MpCrystalizeRatio": MpCrystalizeRatio = ReadFloat(attribute); break;
                    case "NeutralCrystalizeRatio": NeutralCrystalizeRatio = ReadFloat(attribute); break;
                    case "PrepareTime": PrepareTime = ReadFloat(attribute); break;
                    case "ManaDrainLock": ManaDrainLock = ReadFloat(attribute); break;
                    case "ShotgunRatio": ShotgunRatio = ReadFloat(attribute); break;
                    case "SuperArmorTiredTime": SuperArmorTiredTime = ReadFloat(attribute); break;
                    default: throw Unknown(attribute.Name.LocalName, BaseElementName);
                }
            }
        }

        private void ReadProperties(XElement root)
        {
            XElement container = root.Element(PropertiesElementName);
            if (container == null)
            {
                return;
            }

            foreach (XAttribute attribute in container.Attributes())
            {
                throw Unknown(attribute.Name.LocalName, PropertiesElementName);
            }

            foreach (XElement element in container.Elements())
            {
                if (element.Name.LocalName != PropertyElementName)
                {
                    throw new MagicFormatException(
                        "Unknown element <" + element.Name.LocalName + "> inside <" + PropertiesElementName + ">.");
                }

                var property = new MagicCustomProperty();
                foreach (XAttribute attribute in element.Attributes())
                {
                    switch (attribute.Name.LocalName)
                    {
                        case "Name":
                            property.Name = attribute.Value;
                            break;
                        case "Value":
                            property.Value = attribute.Value;
                            break;
                        case "Type":
                            if (!MagicPropertyValue.TryParseType(attribute.Value, out MagicPropertyType type))
                            {
                                throw new MagicFormatException(
                                    "Unknown property type '" + attribute.Value +
                                    "'; expected Int, Float, Bool or String.");
                            }

                            property.Type = type;
                            break;
                        default:
                            throw Unknown(attribute.Name.LocalName, PropertyElementName);
                    }
                }

                Properties.Add(property);
            }
        }

        private void RejectUnknownElements(XElement root)
        {
            foreach (XElement element in root.Elements())
            {
                string name = element.Name.LocalName;
                if (name != BaseElementName && name != PropertiesElementName)
                {
                    throw new MagicFormatException(
                        "Unknown element <" + name + "> inside <" + RootElementName + ">.");
                }
            }
        }

        private static int ReadInt(XAttribute attribute)
        {
            if (MagicPropertyValue.TryParseInt(attribute.Value, out int value))
            {
                return value;
            }

            throw new MagicFormatException(
                attribute.Name.LocalName + " must be a decimal integer, but it is '" + attribute.Value + "'.");
        }

        private static float ReadFloat(XAttribute attribute)
        {
            if (MagicPropertyValue.TryParseFloat(attribute.Value, out float value))
            {
                return value;
            }

            throw new MagicFormatException(
                attribute.Name.LocalName + " must be a number, but it is '" + attribute.Value + "'.");
        }

        private static MagicFormatException Unknown(string attributeName, string elementName) =>
            new MagicFormatException("Unknown attribute '" + attributeName + "' on <" + elementName + ">.");

        // ==================== 写 ====================

        /// <summary>
        /// 规范 XML。顺序固定为根属性、<c>Base</c> 属性、<c>Properties</c>；缩进两空格、换行 CRLF。
        /// Version 与全部可选属性始终写出，避免“默认值改了，旧文件行为跟着变”。
        /// </summary>
        public string ToXml()
        {
            var text = new StringBuilder();
            text.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n");
            text.Append("<").Append(RootElementName)
                .Append(" Version=\"").Append(MagicPropertyValue.FormatInt(Version)).Append("\"")
                .Append(" Id=\"").Append(Escape(Id)).Append("\">\r\n");

            text.Append("  <").Append(BaseElementName)
                .Append(" MpCost=\"").Append(MagicPropertyValue.FormatInt(MpCost)).Append("\"")
                .Append(" CastTime=\"").Append(MagicPropertyValue.FormatFloat(CastTime)).Append("\"")
                .Append(" MpCrystalizeRatio=\"").Append(MagicPropertyValue.FormatFloat(MpCrystalizeRatio)).Append("\"")
                .Append(" NeutralCrystalizeRatio=\"").Append(MagicPropertyValue.FormatFloat(NeutralCrystalizeRatio)).Append("\"")
                .Append(" PrepareTime=\"").Append(MagicPropertyValue.FormatFloat(PrepareTime)).Append("\"")
                .Append(" ManaDrainLock=\"").Append(MagicPropertyValue.FormatFloat(ManaDrainLock)).Append("\"")
                .Append(" ProjectilePower=\"").Append(MagicPropertyValue.FormatInt(ProjectilePower)).Append("\"")
                .Append(" ShotgunRatio=\"").Append(MagicPropertyValue.FormatFloat(ShotgunRatio)).Append("\"")
                .Append(" SuperArmorTiredTime=\"").Append(MagicPropertyValue.FormatFloat(SuperArmorTiredTime)).Append("\"")
                .Append(" />\r\n");

            if (Properties.Count == 0)
            {
                text.Append("  <").Append(PropertiesElementName).Append(" />\r\n");
            }
            else
            {
                text.Append("  <").Append(PropertiesElementName).Append(">\r\n");
                foreach (MagicCustomProperty property in Properties)
                {
                    text.Append("    <").Append(PropertyElementName)
                        .Append(" Name=\"").Append(Escape(property.Name)).Append("\"")
                        .Append(" Type=\"").Append(MagicPropertyValue.TypeToText(property.Type)).Append("\"")
                        .Append(" Value=\"").Append(Escape(property.Value)).Append("\"")
                        .Append(" />\r\n");
                }

                text.Append("  </").Append(PropertiesElementName).Append(">\r\n");
            }

            text.Append("</").Append(RootElementName).Append(">\r\n");
            return text.ToString();
        }

        /// <summary>XML 属性值转义。只转义会破坏属性语法或读回后不等价的字符。</summary>
        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var text = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                switch (c)
                {
                    case '&': text.Append("&amp;"); break;
                    case '<': text.Append("&lt;"); break;
                    case '>': text.Append("&gt;"); break;
                    case '"': text.Append("&quot;"); break;
                    case '\r': text.Append("&#xD;"); break;
                    case '\n': text.Append("&#xA;"); break;
                    case '\t': text.Append("&#x9;"); break;
                    default: text.Append(c); break;
                }
            }

            return text.ToString();
        }
    }
}
