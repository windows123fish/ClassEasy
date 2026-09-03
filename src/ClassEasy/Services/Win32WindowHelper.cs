using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ClassEasy.Services
{
    /// <summary>
    /// 桌面悬浮层 Win32 互操作：切换窗口的"鼠标穿透"状态，
    /// 以及读取全局光标位置（穿透态下 WPF Mouse.GetPosition 不准确）。
    /// </summary>
    public static class Win32WindowHelper
    {
        // ---- user32.dll 常量与签名 ----
        private const int GWL_EXSTYLE = -20;

        private const uint WS_EX_LAYERED      = 0x00080000; // 分层窗口（透明/亚克力/异形必备）
        private const uint WS_EX_TRANSPARENT  = 0x00000020; // 点击穿透
        private const uint WS_EX_TOOLWINDOW   = 0x00000080; // Alt+Tab 不显示（工具窗口）
        private const uint WS_EX_APPWINDOW    = 0x00040000; // Alt+Tab 强制显示（与此相反）

        [Flags]
        private enum SetWindowPosFlags : uint
        {
            SWP_NOSIZE         = 0x0001,
            SWP_NOMOVE         = 0x0002,
            SWP_NOZORDER       = 0x0004,
            SWP_NOACTIVATE     = 0x0010,
            SWP_FRAMECHANGED   = 0x0020,
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Win32Point
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
        private static extern uint GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
        private static extern uint SetWindowLong32(IntPtr hWnd, int nIndex, uint dwNewLong);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, SetWindowPosFlags uFlags);

        /// <summary>全局光标位置（屏幕像素坐标，含 DPI 缩放后的真实物理像素）。</summary>
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out Win32Point lpPoint);

        /// <summary>把全局屏幕坐标（物理像素）换算成指定窗口的 96dpi WPF 逻辑坐标。
        /// 需要 DwmGetWindowAttribute 或 Visual.PointFromScreen；这里用 TransformFromDevice 更简洁。
        /// </summary>
        public static Point? GetCursorLogicalPosition(Window window)
        {
            if (window == null) return null;
            if (!GetCursorPos(out var p)) return null;

            // GetCursorPos 返回物理像素，WPF 的 PointFromScreen 会自动按 DPI 转成逻辑单位（即使窗口有 WS_EX_TRANSPARENT 也可用）
            try
            {
                return window.PointFromScreen(new Point(p.X, p.Y));
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 兼容性包装：x86 走 32 位 API，x64 走 Ptr 版 API。
        /// .NET Framework 4.8 可能是 AnyCPU，必须按 IntPtr.Size 选择正确函数签名。
        /// </summary>
        private static uint GetWindowExStyle(IntPtr hWnd)
        {
            if (IntPtr.Size == 4)
            {
                return GetWindowLong32(hWnd, GWL_EXSTYLE);
            }
            return unchecked((uint)GetWindowLongPtr64(hWnd, GWL_EXSTYLE).ToInt64());
        }

        private static void SetWindowExStyle(IntPtr hWnd, uint value)
        {
            if (IntPtr.Size == 4)
            {
                SetWindowLong32(hWnd, GWL_EXSTYLE, value);
            }
            else
            {
                SetWindowLongPtr64(hWnd, GWL_EXSTYLE, new IntPtr(unchecked((long)value)));
            }
        }

        /// <summary>
        /// 开启或关闭窗口的"点击穿透"。
        /// 开启后：任何鼠标点击（左键、右键、移动）都会穿到本窗口后面的程序；
        ///         窗口对系统来说只是一个"纯视觉装饰"，不再参与命中测试。
        /// 关闭后：恢复正常窗口行为（可点、可拖）。
        /// </summary>
        /// <param name="window">要操作的 WPF Window。</param>
        /// <param name="enable">true=穿透；false=恢复正常。</param>
        public static void SetClickThrough(Window window, bool enable)
        {
            if (window == null) throw new ArgumentNullException(nameof(window));

            // 拿到 HWND：若句柄未创建，先保证生成。
            var helper = new WindowInteropHelper(window);
            if (helper.Handle == IntPtr.Zero)
            {
                helper.EnsureHandle();
            }
            IntPtr hWnd = helper.Handle;

            uint style = GetWindowExStyle(hWnd);
            uint newStyle;
            if (enable)
            {
                // WS_EX_LAYERED + WS_EX_TRANSPARENT 组合 = 视觉可见 + 鼠标消息穿透
                // （AllowsTransparency=True 的 WPF 窗口已带 WS_EX_LAYERED；这里保险再 OR 一次）
                newStyle = style | WS_EX_LAYERED | WS_EX_TRANSPARENT;
            }
            else
            {
                // 去掉 WS_EX_TRANSPARENT（保留 WS_EX_LAYERED，否则 AllowsTransparency 的窗口会黑屏）
                newStyle = (style | WS_EX_LAYERED) & ~WS_EX_TRANSPARENT;
            }

            if (newStyle != style)
            {
                SetWindowExStyle(hWnd, newStyle);
            }

            // 关键：通知系统重算窗口样式（否则 WS_EX 改变可能要等到重绘才生效）
            SetWindowPos(hWnd, IntPtr.Zero, 0, 0, 0, 0,
                SetWindowPosFlags.SWP_NOMOVE
                | SetWindowPosFlags.SWP_NOSIZE
                | SetWindowPosFlags.SWP_NOZORDER
                | SetWindowPosFlags.SWP_NOACTIVATE
                | SetWindowPosFlags.SWP_FRAMECHANGED);
        }

        /// <summary>
        /// 把窗口从 Alt+Tab 切换列表里剔除，任务栏也不出现按钮，
        /// 变成「桌面悬浮小工具」的样式。
        /// 机制：GWL_EXSTYLE 中去掉 WS_EX_APPWINDOW、加上 WS_EX_TOOLWINDOW。
        /// 注意：必须在窗口 SourceInitialized 之后（句柄已创建）调用才生效。
        /// </summary>
        public static void HideFromAltTab(Window window)
        {
            if (window == null) throw new ArgumentNullException(nameof(window));

            var helper = new WindowInteropHelper(window);
            if (helper.Handle == IntPtr.Zero) helper.EnsureHandle();
            IntPtr hWnd = helper.Handle;

            uint style = GetWindowExStyle(hWnd);
            uint newStyle = (style | WS_EX_TOOLWINDOW | WS_EX_LAYERED) & ~WS_EX_APPWINDOW;

            if (newStyle != style)
            {
                SetWindowExStyle(hWnd, newStyle);
            }

            // 通知系统重新计算非客户区 / Alt+Tab 枚举缓存
            SetWindowPos(hWnd, IntPtr.Zero, 0, 0, 0, 0,
                SetWindowPosFlags.SWP_NOMOVE
                | SetWindowPosFlags.SWP_NOSIZE
                | SetWindowPosFlags.SWP_NOZORDER
                | SetWindowPosFlags.SWP_NOACTIVATE
                | SetWindowPosFlags.SWP_FRAMECHANGED);
        }

        // ---- DWM 磨砂背景（Win11 build 22000+） ----

        [StructLayout(LayoutKind.Sequential)]
        private struct MARGINS
        {
            public int cxLeftWidth;
            public int cxRightWidth;
            public int cyTopHeight;
            public int cyBottomHeight;
        }

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hWnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);

        // DWMWA_SYSTEMBACKDROP_TYPE = 38（Win11 22000+ 引入）
        private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
        private const int DWMSBT_AUTO              = 0;
        private const int DWMSBT_NONE              = 1;
        private const int DWMSBT_MAINWINDOW        = 2;  // Mica
        private const int DWMSBT_TRANSIENTWINDOW   = 3;  // Acrylic（最接近"磨砂玻璃"）
        private const int DWMSBT_TABBEDWINDOW      = 4;

        /// <summary>
        /// 开启 Windows 11 系统的"磨砂玻璃 / Acrylic"窗口背景。
        /// 机制：调用 dwmapi!DwmSetWindowAttribute 设置 DWMWA_SYSTEMBACKDROP_TYPE=Acrylic，
        ///       并 DwmExtendFrameIntoClientArea 把 DWM 框架扩展到整个客户区，让系统特效透出来。
        /// 兼容性：仅 Win11 22000+ 有效；旧系统调用会失败但不会崩溃，自动回退到 XAML 里的渐变。
        /// 注意：必须在窗口 SourceInitialized 之后（句柄已创建）调用才生效。
        /// </summary>
        public static void EnableAcrylicBackdrop(Window window)
        {
            if (window == null) return;
            var helper = new WindowInteropHelper(window);
            if (helper.Handle == IntPtr.Zero) helper.EnsureHandle();
            IntPtr hWnd = helper.Handle;

            // 1) 把 DWM 框架扩展到整个客户区（-1 = 全占）
            var margins = new MARGINS
            {
                cxLeftWidth = -1,
                cxRightWidth = -1,
                cyTopHeight = -1,
                cyBottomHeight = -1
            };
            DwmExtendFrameIntoClientArea(hWnd, ref margins);

            // 2) 背景类型 = Acrylic（磨砂）
            int useAcrylic = DWMSBT_TRANSIENTWINDOW;
            DwmSetWindowAttribute(hWnd, DWMWA_SYSTEMBACKDROP_TYPE, ref useAcrylic, sizeof(int));
        }
    }
}