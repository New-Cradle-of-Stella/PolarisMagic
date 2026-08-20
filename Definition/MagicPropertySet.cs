using System;
using System.Collections;
using System.Collections.Generic;
using Polaris.Magic.Authoring;

namespace Polaris.Magic.Definitions
{
    /// <summary>一条自定义静态属性的运行时快照。值在注册期就已定型，施法期只读。</summary>
    public readonly struct MagicPropertyEntry
    {
        internal MagicPropertyEntry(string name, MagicPropertyType type, object value)
        {
            Name = name;
            Type = type;
            Value = value;
        }

        public string Name { get; }

        public MagicPropertyType Type { get; }

        /// <summary>装箱后的值：<see cref="int"/>、<see cref="float"/>、<see cref="bool"/> 或 <see cref="string"/>。</summary>
        public object Value { get; }
    }

    /// <summary>
    /// 自定义静态属性表。生成代码同时会为每条属性发射一个强类型 <c>public static</c> 只读属性，
    /// 作者平时直接用那些属性；这张表是给"按名字看一眼有哪些参数"的诊断和调试面板用的。
    /// </summary>
    public sealed class MagicPropertySet : IReadOnlyList<MagicPropertyEntry>
    {
        /// <summary>空表单例。没有自定义属性的魔法不必各自分配一份。</summary>
        public static readonly MagicPropertySet Empty = new MagicPropertySet(new List<MagicPropertyEntry>());

        private readonly List<MagicPropertyEntry> entries;
        private readonly Dictionary<string, int> index;

        internal MagicPropertySet(List<MagicPropertyEntry> entries)
        {
            this.entries = entries;
            index = new Dictionary<string, int>(entries.Count, StringComparer.Ordinal);
            for (int i = 0; i < entries.Count; i++)
            {
                index[entries[i].Name] = i;
            }
        }

        public int Count => entries.Count;

        /// <summary>按声明顺序取；顺序与 <c>.pmagic</c> 里的行顺序一致。</summary>
        public MagicPropertyEntry this[int i] => entries[i];

        public bool TryGet(string name, out MagicPropertyEntry entry)
        {
            if (name != null && index.TryGetValue(name, out int i))
            {
                entry = entries[i];
                return true;
            }

            entry = default;
            return false;
        }

        public IEnumerator<MagicPropertyEntry> GetEnumerator() => entries.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
