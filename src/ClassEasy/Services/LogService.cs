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

        public static void Info(string message)
        {
            Write("INFO", message, null);
        }

        public static void Error(string message, Exception ex = null)
        {
            Write("ERROR", message, ex);
        }

        /// <summary>记录非致命告警（例如 NTP 授时失败降级本地时间）。</summary>
        public static void Warn(string message)
        {
            Write("WARN", message, null);
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