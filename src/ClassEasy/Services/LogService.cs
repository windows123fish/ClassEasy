using System;
using System.IO;

namespace ClassEasy.Services
{
    /// <summary>
    /// 简单的文件日志服务，将错误与关键信息写入用户目录，便于排查问题。
    /// </summary>
    public static class LogService
    {
        private static readonly object SyncRoot = new object();

        /// <summary>当前日志文件路径（按天轮转）。</summary>
        public static string LogFile => Path.Combine(
            Paths.DataDir, "logs", DateTime.Now.ToString("yyyyMMdd") + ".log");

        /// <summary>当前最低输出级别：Info（全部） / Warning（警告+错误） / Error（仅错误）。
        /// 默认 Info；开发者可在配置里调高减少噪音。</summary>
        public static string MinLevel { get; set; } = "Info";

        /// <summary>把字符串级别映射为数值（数字越大越严格）。</summary>
        private static int LevelValue(string level)
        {
            switch ((level ?? "Info").Trim().ToLowerInvariant())
            {
                case "error": return 2;
                case "warning":
                case "warn": return 1;
                default: return 0; // Info
            }
        }

        private static bool ShouldWrite(string level)
        {
            return LevelValue(level) >= LevelValue(MinLevel);
        }

        public static void Info(string message)
        {
            if (ShouldWrite("Info")) Write("INFO", message, null);
        }

        public static void Error(string message, Exception ex = null)
        {
            if (ShouldWrite("Error")) Write("ERROR", message, ex);
        }

        /// <summary>记录非致命告警（例如 NTP 授时失败降级本地时间）。</summary>
        public static void Warn(string message)
        {
            if (ShouldWrite("Warning")) Write("WARN", message, null);
        }

        private static void Write(string level, string message, Exception ex)
        {
            try
            {
                lock (SyncRoot)
                {
                    var directory = Path.GetDirectoryName(LogFile);
                    if (!string.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    var line = string.Format("{0:yyyy-MM-dd HH:mm:ss.fff} [{1}] {2}",
                        DateTime.Now, level, message);
                    if (ex != null)
                    {
                        line += Environment.NewLine + ex;
                    }

                    File.AppendAllText(LogFile, line + Environment.NewLine);
                }
            }
            catch
            {
                // 日志写入失败时静默忽略，避免二次异常。
            }
        }
    }
}