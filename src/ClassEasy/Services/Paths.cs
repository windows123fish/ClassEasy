using System;
using System.IO;

namespace ClassEasy.Services
{
    /// <summary>
    /// 应用数据与配置文件路径的集中定义。
    /// </summary>
    public static class Paths
    {
        private static string? _dataDir;

        /// <summary>
        /// 数据根目录。通过"实际尝试创建目录"来探测第一个可写的位置，
        /// 依次尝试 ApplicationData、LocalApplicationData、Temp、程序目录，
        /// 避免在只读/被保护目录下创建文件导致 UnauthorizedAccessException。
        /// </summary>
        public static string DataDir
        {
            get
            {
                if (_dataDir == null)
                {
                    _dataDir = ResolveDataDir();
                }
                return _dataDir;
            }
        }

        private static string ResolveDataDir()
        {
            string[] candidates =
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Path.GetTempPath()
            };

            foreach (var baseDir in candidates)
            {
                if (string.IsNullOrWhiteSpace(baseDir))
                {
                    continue;
                }

                var dir = Path.Combine(baseDir, "ClassEasy");
                if (TryEnsureDirectory(dir))
                {
                    return dir;
                }
            }

            // 所有系统目录都不可写时，回退到程序所在目录，保证程序仍能运行。
            var exeDir = Path.GetDirectoryName(typeof(Paths).Assembly.Location);
            var fallback = Path.Combine(string.IsNullOrEmpty(exeDir) ? "." : exeDir, "ClassEasy");
            TryEnsureDirectory(fallback);
            return fallback;
        }

        /// <summary>尝试创建目录，成功返回 true，失败（权限/IO 异常）返回 false。</summary>
        private static bool TryEnsureDirectory(string dir)
        {
            try
            {
                Directory.CreateDirectory(dir);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>课表数据文件路径。</summary>
        public static string ScheduleFile => Path.Combine(DataDir, "schedule.json");

        /// <summary>应用配置文件路径。</summary>
        public static string ConfigFile => Path.Combine(DataDir, "config.json");
    }
}
