using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace ClassEasy.Services
{
    /// <summary>
    /// 配置与课表数据的加载/保存（System.Text.Json，零第三方依赖）。
    /// </summary>
    public static class ConfigService
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public static T Load<T>(string path) where T : new()
        {
            try
            {
                if (!File.Exists(path))
                {
                    return new T();
                }

                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<T>(json, Options) ?? new T();
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
            try
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // 先写入临时文件，再原子替换，避免写入过程中断导致配置文件损坏。
                var tempPath = path + ".tmp";
                File.WriteAllText(tempPath, JsonSerializer.Serialize(obj, Options));

                if (File.Exists(path))
                {
                    File.Replace(tempPath, path, null);
                }
                else
                {
                    File.Move(tempPath, path);
                }
            }
            catch (Exception ex)
            {
                LogService.Error("保存配置失败：" + path, ex);
            }
        }
    }
}
