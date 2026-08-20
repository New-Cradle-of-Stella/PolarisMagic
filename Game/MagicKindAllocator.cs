using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using nel;
using Polaris.Magic.Runtime;

namespace Polaris.Magic.Game
{
    /// <summary>
    /// 字符串 Id ↔ 数字 <c>MGKIND</c> 的分配与持久化。
    ///
    /// 为什么必须持久化：玩家存档里存的是数字 <c>ushort</c>（<c>MagicSelector.writeBinaryTo</c>），
    /// 不是字符串 Id。如果每次启动按程序集加载顺序重新编号，玩家上次学会的魔法下次进游戏就会变成
    /// 另一种魔法，或者干脆对不上而被静默丢弃。因此映射一旦分配就写进磁盘，之后只增不改。
    ///
    /// 登记区固定 30000–39999：原版枚举 14011–50000 之间是空档（见魔法系统技术文档 §21.4），
    /// 而 ID 又必须 ≤ 65535 才能进存档。启动时仍然逐个核对原版枚举与 MKind 字典，不假定空档一直空着。
    /// </summary>
    internal sealed class MagicKindAllocator
    {
        internal const int RangeStart = 30000;
        internal const int RangeEndExclusive = 40000;

        private const string FileName = "magic-ids.txt";

        private readonly Dictionary<string, int> assigned = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<int, string> reverse = new Dictionary<int, string>();
        private readonly string filePath;

        private bool dirty;

        internal MagicKindAllocator(string stateDirectory)
        {
            filePath = Path.Combine(stateDirectory, FileName);
        }

        internal IReadOnlyDictionary<string, int> Assignments => assigned;

        /// <summary>读入已有映射。文件不存在是正常的首次启动；文件坏了只跳过坏行，不清空整张表。</summary>
        internal void Load()
        {
            if (!File.Exists(filePath))
            {
                return;
            }

            foreach (string raw in File.ReadAllLines(filePath, Encoding.UTF8))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#')
                {
                    continue;
                }

                int separator = line.IndexOf('=');
                if (separator <= 0)
                {
                    MagicLog.Warn("Skipping a malformed line in " + FileName + ": " + line);
                    continue;
                }

                string id = line.Substring(0, separator).Trim();
                string number = line.Substring(separator + 1).Trim();

                if (!int.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out int kind)
                    || kind < RangeStart || kind >= RangeEndExclusive)
                {
                    MagicLog.Warn("Skipping the out-of-range mapping " + line + " in " + FileName + ".");
                    continue;
                }

                if (assigned.TryGetValue(id, out int previousKind))
                {
                    if (previousKind != kind)
                    {
                        MagicLog.Warn(
                            "Magic id '" + id + "' is mapped to both " + previousKind + " and " + kind +
                            " in " + FileName + "; keeping " + previousKind + ".");
                    }

                    continue;
                }

                if (reverse.TryGetValue(kind, out string owner))
                {
                    MagicLog.Warn(
                        "Numeric id " + kind + " is claimed by both '" + owner + "' and '" + id +
                        "' in " + FileName + "; keeping '" + owner + "'.");
                    continue;
                }

                assigned[id] = kind;
                reverse[kind] = id;
            }
        }

        /// <summary>取已有映射，没有就分配一个新的。返回的数字保证不与原版枚举或已有映射冲突。</summary>
        internal MGKIND Resolve(string magicId)
        {
            if (assigned.TryGetValue(magicId, out int existing))
            {
                return (MGKIND)existing;
            }

            for (int candidate = RangeStart; candidate < RangeEndExclusive; candidate++)
            {
                if (reverse.ContainsKey(candidate) || IsTakenByVanilla(candidate))
                {
                    continue;
                }

                assigned[magicId] = candidate;
                reverse[candidate] = magicId;
                dirty = true;
                MagicLog.Info("Assigned numeric id " + candidate + " to magic '" + magicId + "'.");
                return (MGKIND)candidate;
            }

            throw new InvalidOperationException(
                "The PolarisMagic id range " + RangeStart + "-" + (RangeEndExclusive - 1) + " is exhausted.");
        }

        /// <summary>写回映射。只在真的分配了新 Id 时写文件。</summary>
        internal void Save()
        {
            if (!dirty)
            {
                return;
            }

            var text = new StringBuilder();
            text.Append("# PolarisMagic string id -> numeric MGKIND.\r\n");
            text.Append("# Save files store the numeric id, so these mappings are append-only:\r\n");
            text.Append("# editing or reordering them silently rewrites what players already learned.\r\n");

            var ordered = new List<KeyValuePair<string, int>>(assigned);
            ordered.Sort((left, right) => left.Value.CompareTo(right.Value));
            foreach (KeyValuePair<string, int> entry in ordered)
            {
                text.Append(entry.Key).Append('=')
                    .Append(entry.Value.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            File.WriteAllText(filePath, text.ToString(), new UTF8Encoding(false));
            dirty = false;
        }

        /// <summary>原版枚举已有这个数值，或原版/其它模组已经往 MKind 字典里放过它。</summary>
        private static bool IsTakenByVanilla(int candidate)
        {
            if (Enum.IsDefined(typeof(MGKIND), candidate))
            {
                return true;
            }

            // 用返回引用的重载：out 版本在 MKind 表还没加载时会直接对 null 字典解引用。
            return MKind.Get((MGKIND)candidate) != null;
        }
    }
}
