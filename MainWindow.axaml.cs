using System;
using System.Collections.Generic;
using System.Timers;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using ClassEasy.ViewModels;

namespace ClassEasy;

public partial class MainWindow : Window
{
    private bool _forceClose;
    private readonly System.Timers.Timer _hoverTimer;

    // 触摸手势状态
    private readonly System.Timers.Timer _longPressTimer;
    private Point _touchStartPos;
    private PixelPoint _windowStartPos;
    private bool _isTouchDragging;
    private bool _longPressTriggered;
    private const int DragThreshold = 10; // 拖拽判定阈值（像素）
    private const int LongPressMs = 500;  // 长按判定时间（毫秒）

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();

        // 100ms 检测一次鼠标是否离开窗口
        _hoverTimer = new System.Timers.Timer(100) { AutoReset = true };
        _hoverTimer.Elapsed += OnHoverTimerTick;

        // 长按定时器（触摸手势）
        _longPressTimer = new System.Timers.Timer(LongPressMs) { AutoReset = false };
        _longPressTimer.Elapsed += OnLongPressTimerTick;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        ShowInTaskbar = false;

        // 屏幕正上方顶端
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        if (screen is not null)
        {
            var pos = Position;
            Position = new PixelPoint(pos.X, screen.WorkingArea.Y);
        }

        Win32Helper.HideFromAltTab(TryGetPlatformHandle()?.Handle ?? IntPtr.Zero);
        (Application.Current as App)?.EnsureTray();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (!_forceClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        base.OnClosing(e);
    }

    public void ForceClose()
    {
        _forceClose = true;
        _hoverTimer?.Stop();
        _hoverTimer?.Dispose();
        _longPressTimer?.Stop();
        _longPressTimer?.Dispose();
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.Dispose();
        base.OnClosed(e);
    }

    /// <summary>
    /// 鼠标进入：淡化 + 点击穿透。
    /// 设 WS_EX_TRANSPARENT 后 Avalonia 收不到 PointerExited，改用定时器检测。
    /// 触摸输入不触发此效果（触屏没有 hover 概念）。
    /// </summary>
    private void RootBorder_PointerEntered(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        // 触摸/笔输入不启用 hover 效果
        if (e.Pointer.Type != PointerType.Mouse)
            return;

        var hwnd = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (hwnd == IntPtr.Zero) return;

        RootBorder.Opacity = 0.35;
        Win32Helper.SetWindowTransparent(hwnd, true);
        _hoverTimer.Start();
    }

    /// <summary>
    /// 定时器：检测鼠标是否离开窗口 bounds，离开则取消穿透。
    /// 鼠标仍在窗口内时，每 100ms 重新应用穿透样式，防止 Avalonia 渲染循环重置 GWL_EXSTYLE。
    /// </summary>
    private void OnHoverTimerTick(object? sender, ElapsedEventArgs e)
    {
        var hwnd = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (hwnd == IntPtr.Zero) return;

        Win32Helper.GetCursorPos(out var pt);
        Win32Helper.GetWindowRect(hwnd, out var rc);

        // 留 2px 容差，避免边缘抖动
        if (pt.X < rc.Left + 2 || pt.X > rc.Right - 2 || pt.Y < rc.Top + 2 || pt.Y > rc.Bottom - 2)
        {
            // 鼠标已离开窗口
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                RootBorder.Opacity = 1.0;
                Win32Helper.SetWindowTransparent(hwnd, false);
            });
            _hoverTimer.Stop();
        }
        else
        {
            // 鼠标仍在窗口内：重新应用穿透样式，对抗 Avalonia 渲染循环对 GWL_EXSTYLE 的重置
            Win32Helper.SetWindowTransparent(hwnd, true);
        }
    }

    // ==================== 触屏手势支持 ====================

    /// <summary>
    /// 触摸按下：记录起始位置，启动长按检测。
    /// </summary>
    private void RootBorder_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Pointer.Type != PointerType.Touch)
            return;

        _touchStartPos = e.GetPosition(RootBorder);
        _windowStartPos = Position;
        _isTouchDragging = false;
        _longPressTriggered = false;

        _longPressTimer.Stop();
        _longPressTimer.Start();
    }

    /// <summary>
    /// 触摸移动：若移动距离超过阈值则判定为拖拽，移动窗口位置。
    /// </summary>
    private void RootBorder_PointerMoved(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        if (e.Pointer.Type != PointerType.Touch)
            return;
        if (!_longPressTimer.Enabled && !_longPressTriggered)
            return; // 没有按下

        var currentPos = e.GetPosition(RootBorder);
        var deltaX = currentPos.X - _touchStartPos.X;
        var deltaY = currentPos.Y - _touchStartPos.Y;

        if (Math.Abs(deltaX) > DragThreshold || Math.Abs(deltaY) > DragThreshold)
        {
            _isTouchDragging = true;
            _longPressTimer.Stop(); // 移动中取消长按

            // 移动窗口（保持贴顶栏可手动拖回）
            Position = new PixelPoint(
                _windowStartPos.X + (int)deltaX,
                _windowStartPos.Y + (int)deltaY);
        }
    }

    /// <summary>
    /// 触摸释放：若未拖拽且未触发长按，则为轻触（可扩展功能）。
    /// </summary>
    private void RootBorder_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.Pointer.Type != PointerType.Touch)
            return;

        _longPressTimer.Stop();

        if (!_isTouchDragging && !_longPressTriggered)
        {
            // 轻触：暂不做额外操作（避免与可能的后台点击冲突）
        }

        _isTouchDragging = false;
        _longPressTriggered = false;
    }

    /// <summary>
    /// 长按定时器触发：弹出触摸菜单。
    /// </summary>
    private void OnLongPressTimerTick(object? sender, ElapsedEventArgs e)
    {
        _longPressTriggered = true;
        Dispatcher.UIThread.InvokeAsync(ShowTouchMenu);
    }

    /// <summary>
    /// 在窗口中心弹出触摸右键菜单。
    /// </summary>
    private void ShowTouchMenu()
    {
        var menu = new ContextMenu();

        var showItem = new MenuItem { Header = "显示主窗口" };
        showItem.Click += (_, _) => Show();
        menu.Items.Add(showItem);

        var hideItem = new MenuItem { Header = "隐藏到托盘" };
        hideItem.Click += (_, _) => Hide();
        menu.Items.Add(hideItem);

        menu.Items.Add(new Separator());

        var topmostItem = new MenuItem { Header = $"置顶窗口 {(Topmost ? "✓" : "")}" };
        topmostItem.Click += (_, _) =>
        {
            Topmost = !Topmost;
            topmostItem.Header = $"置顶窗口 {(Topmost ? "✓" : "")}";
        };
        menu.Items.Add(topmostItem);

        menu.Items.Add(new Separator());

        var exitItem = new MenuItem { Header = "退出" };
        exitItem.Click += (_, _) =>
        {
            try
            {
                var icons = TrayIcon.GetIcons(Application.Current!);
                if (icons is not null)
                {
                    var list = new List<TrayIcon>();
                    foreach (var icon in icons)
                        list.Add(icon);
                    foreach (var icon in list)
                        icons.Remove(icon);
                }
            }
            catch { }
            ForceClose();
            (Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.Shutdown();
        };
        menu.Items.Add(exitItem);

        menu.Placement = PlacementMode.Pointer;
        menu.Open(RootBorder);
    }
}
