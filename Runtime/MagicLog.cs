namespace Polaris.Magic.Runtime
{
    /// <summary>
    /// PolarisMagic 的日志出口。文案一律英文：日志会被贴到 issue 里，中文在部分玩家的控制台编码下
    /// 会变成乱码，反而丢掉排错信息。游戏内给玩家看的文案不走这里。
    /// </summary>
    internal static class MagicLog
    {
        private const string Prefix = "[PolarisMagic] ";

        internal static void Info(string message) => Plugin.Logger.LogMessage(Prefix + message);

        internal static void Debug(string message) => Plugin.Logger.LogInfo(Prefix + message);

        internal static void Warn(string message) => Plugin.Logger.LogWarning(Prefix + message);

        internal static void Error(string message) => Plugin.Logger.LogError(Prefix + message);
    }
}
