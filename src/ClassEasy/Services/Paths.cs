using System;
using System.IO;

namespace ClassEasy.Services
{
    /// <summary>
    /// 应用数据与配置文件路径的集中定义。
    /// </summary>
    public static class Paths
    {
        private static string _dataDir;

        /// <summary>
        /// 数据根目录。优先使用用户 AppDataRoaming，若系统返回空则依次回退到
        /// LocalAppData、临时目录，避免因环境变量缺失导致在只读目录创建文件。
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
            var baseDir =
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            if (string.IsNullOrWhiteSpace(baseDir))
            {
                baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            }

            if (string.IsNullOrWhiteSpace(baseDir))
            {
                baseDir = Path.GetTempPath();
            }

            return Path.Combine(baseDir, "ClassEasy");
        }

        /// <summary>课表数据文件路径。</summary>
        public static string ScheduleFile => Path.Combine(DataDir, "schedule.xml");

        /// <summary>应用配置文件路径。</summary>
        public static string ConfigFile => Path.Combine(DataDir, "config.xml");
    }
}