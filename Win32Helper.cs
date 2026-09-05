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
}