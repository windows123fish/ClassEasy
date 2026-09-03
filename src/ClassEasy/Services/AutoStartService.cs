using System;
using System.Reflection;
using Microsoft.Win32;

namespace ClassEasy.Services
{
    /// <summary>
    /// 开机自动启动服务：通过写入 / 删除注册表
    /// HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run
    /// 实现当前用户级别的开机启动。不需要管理员权限。
    /// </summary>
    public static class AutoStartService
    {
        private const string RegistryKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
        private const string AppName = "ClassEasy";

        /// <summary>当前程序的可执行文件路径（含 .exe）。</summary>
        private static string ExePath
        {
            get
            {
                // .NET Framework 4.8 可用 Assembly.GetExecutingAssembly().Location，
                // 但 ClickOnce/阴影复制时可能不准；这里优先用 Process.MainModule。
                try
                {
                    return System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
                }
                catch
                {
                    return Assembly.GetExecutingAssembly().Location;
                }
            }
        }

        /// <summary>查询当前是否已设置开机启动。</summary>
        public static bool IsEnabled()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RegistryKey, false))
                {
                    var value = key?.GetValue(AppName) as string;
                    return !string.IsNullOrEmpty(value);
                }
            }
            catch (Exception ex)
            {
                LogService.Error("读取开机启动注册表失败", ex);
                return false;
            }
        }

        /// <summary>设置或取消开机启动。</summary>
        /// <param name="enable">true=写入注册表；false=删除注册表项。</param>
        public static void SetEnabled(bool enable)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RegistryKey, true))
                {
                    if (key == null)
                    {
                        LogService.Error("无法打开开机启动注册表项", null);
                        return;
                    }

                    if (enable)
                    {
                        // 路径带空格时加引号
                        string path = "\"" + ExePath + "\"";
                        key.SetValue(AppName, path, RegistryValueKind.String);
                    }
                    else
                    {
                        if (key.GetValue(AppName) != null)
                        {
                            key.DeleteValue(AppName, false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.Error("设置开机启动失败", ex);
            }
        }
    }
}
