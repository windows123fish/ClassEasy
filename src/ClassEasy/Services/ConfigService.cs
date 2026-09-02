using System;
using System.IO;
using System.Xml.Serialization;

namespace ClassEasy.Services
{
    /// <summary>
    /// 配置与课表数据的加载/保存（使用内置 XML 序列化，零第三方依赖）。
    /// </summary>
    public static class ConfigService
    {
        public static T Load<T>(string path) where T : new()
        {
            try
            {
                if (!File.Exists(path))
                {
                    return new T();
                }

                var serializer = new XmlSerializer(typeof(T));
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    return (T)serializer.Deserialize(stream);
                }
            }
            catch (Exception ex)
            {
                // 数据损坏或格式不兼容时回退到默认实例，避免程序崩溃。
                LogService.Error("加载配置失败：" + path, ex);
                return new T();
            }
        }

        public static void Save<T>(string path, T obj)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var serializer = new XmlSerializer(typeof(T));

            // 先写入临时文件，再原子替换，避免写入过程中断导致配置文件损坏。
            string tempPath = path + ".tmp";
            using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
            {
                serializer.Serialize(stream, obj);
                stream.Flush();
            }

            if (File.Exists(path))
            {
                File.Replace(tempPath, path, null);
            }
            else
            {
                File.Move(tempPath, path);
            }
        }
    }
}