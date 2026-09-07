using System;
using System.Runtime.InteropServices;

namespace ClassEasy;

/// <summary>
/// 集中管理 Win32 P/Invoke 调用。
/// 注意：DllImport 声明放在独立类中，避免 Avalonia XAML 编译器
/// 扫描 MainWindow 代码隐藏时因 P/Invoke 触发内部 NullReferenceException。
/// </summary>
public static class Win32Helper
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_APPWINDOW = 0x00040000;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_LAYERED = 0x00080000;
    private const uint LWA_COLORKEY = 0x00000001;
    private const uint LWA_ALPHA    = 0x00000002;

    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    private static int GetWindowLong(IntPtr hWnd, int nIndex)
        => IntPtr.Size == 8 ? (int)GetWindowLongPtr64(hWnd, nIndex) : GetWindowLong32(hWnd, nIndex);

    private static void SetWindowLong(IntPtr hWnd, int nIndex, int value)
    {
        if (IntPtr.Size == 8)
            SetWindowLongPtr64(hWnd, nIndex, new IntPtr(value));
        else
            SetWindowLong32(hWnd, nIndex, value);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

    /// <summary>
    /// 弹出提示框（MB_ICONINFORMATION）。返回按钮 id，0 表示调用失败。
    /// </summary>
    public static void ShowMessageBox(IntPtr hwnd, string text, string caption)
        => MessageBox(hwnd, text, caption, 0x00000040 /* MB_ICONINFORMATION */);

    /// <summary>
    /// 从 Alt+Tab 切换列表中隐藏窗口：添加 WS_EX_TOOLWINDOW，移除 WS_EX_APPWINDOW。
    /// </summary>
    public static void HideFromAltTab(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return;

        var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        exStyle = (exStyle | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW;
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
    }

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

    /// <summary>
    /// 启用 WS_EX_LAYERED（ColorKey / Alpha 生效的前提）。窗口创建后调用一次即可。
    /// </summary>
    public static void EnableLayered(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;
        var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        if ((exStyle & WS_EX_LAYERED) == 0)
        {
            exStyle |= WS_EX_LAYERED;
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
            RefreshWindowStyle(hwnd);
        }
    }

    /// <summary>
    /// hover 进入：纯黑背景(ColorKey)变透明 + 点击穿透。
    /// 要求窗口 Background 已设为 Black，这样 ColorKey 只透明黑底，保留文字。
    /// </summary>
    public static void SetWindowTransparent(IntPtr hwnd, bool transparent)
    {
        if (hwnd == IntPtr.Zero) return;

        var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        if (transparent)
        {
            exStyle |= WS_EX_TRANSPARENT;
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
            // 纯黑(0x000000)变完全透明；alpha=0 配合 COLORKEY 使用
            SetLayeredWindowAttributes(hwnd, 0x000000, 0, LWA_COLORKEY);
        }
        else
        {
            exStyle &= ~WS_EX_TRANSPARENT;
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
            // 关闭 ColorKey，恢复 alpha=255 完全不透明
            SetLayeredWindowAttributes(hwnd, 0, 255, LWA_ALPHA);
        }
        RefreshWindowStyle(hwnd);
    }

    /// <summary>
    /// 定时器：若 TRANSPARENT 被 Avalonia 重置则补回。
    /// </summary>
    public static void ReassertWindowTransparent(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;
        var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        if ((exStyle & WS_EX_TRANSPARENT) == 0)
        {
            exStyle |= WS_EX_TRANSPARENT;
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
            SetLayeredWindowAttributes(hwnd, 0x000000, 0, LWA_COLORKEY);
            RefreshWindowStyle(hwnd);
        }
    }

    private static void RefreshWindowStyle(IntPtr hwnd)
    {
        SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
            0x0020 /* SWP_FRAMECHANGED */ | 0x0004 /* SWP_NOMOVE */ |
            0x0001 /* SWP_NOSIZE */ | 0x0002 /* SWP_NOZORDER */ |
            0x0008 /* SWP_NOACTIVATE */ | 0x0010 /* SWP_NOOWNERZORDER */);
    }
}