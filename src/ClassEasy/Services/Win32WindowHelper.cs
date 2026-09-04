using System;
using System.Runtime.InteropServices;

namespace ClassEasy.Services
{
    /// <summary>
    /// Win32 窗口辅助：Alt+Tab 隐藏（WS_EX_TOOLWINDOW）、鼠标穿透（WS_EX_TRANSPARENT）、
    /// Ctrl+Alt+T 全局快捷键轮询。仅 Windows 平台可用。
    /// </summary>
    public static class Win32WindowHelper
    {
        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_APPWINDOW = 0x00040000;
        private const int WS_EX_NOACTIVATE = 0x08000000;

        private const int VK_CONTROL = 0x11;
        private const int VK_MENU = 0x12;
        private const int VK_T = 0x54;

        /// <summary>从 Alt+Tab 任务切换列表中隐藏窗口，且点击不抢焦点。</summary>
        public static void HideFromAltTab(IntPtr hWnd)
        {
            try
            {
                int style = GetWindowLong(hWnd, GWL_EXSTYLE);
                SetWindowLong(hWnd, GWL_EXSTYLE,
                    (style | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE) & ~WS_EX_APPWINDOW);
            }
            catch (Exception ex)
            {
                LogService.Warn("HideFromAltTab 失败: " + ex.Message);
            }
        }

        /// <summary>切换鼠标点击穿透。</summary>
        public static void SetClickThrough(IntPtr hWnd, bool enable)
        {
            try
            {
                int style = GetWindowLong(hWnd, GWL_EXSTYLE);
                if (enable)
                {
                    SetWindowLong(hWnd, GWL_EXSTYLE, style | WS_EX_TRANSPARENT);
                }
                else
                {
                    SetWindowLong(hWnd, GWL_EXSTYLE, style & ~WS_EX_TRANSPARENT);
                }
            }
            catch (Exception ex)
            {
                LogService.Warn("SetClickThrough 失败: " + ex.Message);
            }
        }

        /// <summary>轮询：Ctrl+Alt+T 是否当前被按下。</summary>
        public static bool IsHotkeyPressed()
        {
            return (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0
                && (GetAsyncKeyState(VK_MENU) & 0x8000) != 0
                && (GetAsyncKeyState(VK_T) & 0x8000) != 0;
        }
    }
}
