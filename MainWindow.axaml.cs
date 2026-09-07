using System;
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
    private bool _userMovedWindow; // 用户是否手动拖拽过窗口
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

        // 屏幕正上方顶端（仅在用户未手动移动过窗口时才贴齐顶端）
        if (!_userMovedWindow)
        {
            var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
            if (screen is not null)
            {
                var pos = Position;
                Position = new PixelPoint(pos.X, screen.WorkingArea.Y);
            }
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

        // 主动释放 VM（避免依赖 OnClosed 时的隐式释放顺序）
        if (DataContext is MainWindowViewModel vm)
            vm.Dispose();

        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.Dispose();
        base.OnClosed(e);
    }

    /// <summary>
    /// 鼠标进入：隐藏亚克力背景层 + 高光条，内容变淡到 15%，
    /// 同时开启 Win32 点击穿透。触摸输入不触发此效果。
    /// </summary>
    private void RootBorder_PointerEntered(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        if (e.Pointer.Type != PointerType.Mouse)
            return;

        var hwnd = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (hwnd == IntPtr.Zero) return;

        AcrylicBackground.IsVisible = false;
        TopHighlight.IsVisible = false;
        ContentPanel.Opacity = 0.15;
        Win32Helper.SetWindowTransparent(hwnd, true);
        _hoverTimer.Start();
    }

    /// <summary>
    /// 定时器：检测鼠标是否离开窗口 bounds，离开则恢复背景层、内容不透明、取消穿透。
    /// </summary>
    private void OnHoverTimerTick(object? sender, ElapsedEventArgs e)
    {
        var hwnd = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (hwnd == IntPtr.Zero) return;

        Win32Helper.GetCursorPos(out var pt);
        Win32Helper.GetWindowRect(hwnd, out var rc);

        if (pt.X < rc.Left + 2 || pt.X > rc.Right - 2 || pt.Y < rc.Top + 2 || pt.Y > rc.Bottom - 2)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                AcrylicBackground.IsVisible = true;
                TopHighlight.IsVisible = true;
                ContentPanel.Opacity = 1.0;
                Win32Helper.SetWindowTransparent(hwnd, false);
            });
            _hoverTimer.Stop();
        }
        else
        {
            Win32Helper.ReassertWindowTransparent(hwnd);
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

        // 显式 Stop+Start，避免前一次未正常结束导致 Timer 处于 Stopped 后被忽略
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
            _userMovedWindow = true; // 标记用户已移动过，OnOpened 不再强制贴齐顶端
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
        _isTouchDragging = false;
        _longPressTriggered = false;
    }

    /// <summary>
    /// 触摸捕获丢失（如窗口外滑出）：同样清理状态，避免残留触发菜单。
    /// </summary>
    private void RootBorder_PointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (e.Pointer.Type != PointerType.Touch)
            return;

        _longPressTimer.Stop();
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
            // 退出由 ApplicationLifetime.Shutdown() 统一处理，
            // TrayIcon 清理与窗口释放下沉到 App.OnExit/OnFrameworkShutdownCompleted
            ForceClose();
            (Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.Shutdown();
        };
        menu.Items.Add(exitItem);

        menu.Placement = PlacementMode.Pointer;
        menu.Open(RootBorder);
    }
}
