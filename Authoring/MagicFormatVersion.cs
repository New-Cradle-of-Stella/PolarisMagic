namespace Polaris.Magic.Authoring
{
    /// <summary>
    /// <c>.pmagic</c> 作者文件的格式版本。工具侧与游戏侧链接同一份文件，因此不会出现
    /// "编辑器按版本 2 写、运行时按版本 1 读"的偏移。
    /// </summary>
    public static class MagicFormatVersion
    {
        /// <summary>当前写出的版本号。</summary>
        public const int Current = 1;

        /// <summary>能够读取的最低版本号。</summary>
        public const int Minimum = 1;

        /// <summary>版本号是否在可读范围内。超出范围时编辑器只读打开，工具不会自动升级或降级格式。</summary>
        public static bool IsReadable(int version) => version >= Minimum && version <= Current;
    }
}
