using System;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ClassEasy.Services;
using ClassEasy.ViewModels;

namespace ClassEasy.Views
{
    /// <summary>
    /// 灵动岛主窗口：顶部悬浮、亚克力毛玻璃、可展开课表面板、
    /// 顶栏组件拖拽排序、Ctrl+Alt+T 鼠标穿透。
    /// </summary>
    public partial class MainWindow : Window
    {
        private const double CollapsedHeight = 68;
        private const double ExpandedHeight = 560;

        private DispatcherTimer? _hotkeyTimer;
        private bool _hotkeyWasDown;
        private bool _allowClose;

        // 顶栏胶囊拖拽状态
        private Border? _dragBorder;
        private string? _dragKind;
        private bool _dragging;
        private Point _dragStart;

        private MainViewModel? _subscribedVm;

        public MainWindow()
        {
            InitializeComponent();
            Opened += OnWinOpened;
            Closing += OnWinClosing;
            DataContextChanged += OnDataContextChanged;
        }

        public MainViewModel? ViewModel => DataContext as MainViewModel;

        // ------------------------------------------------------------------
        // 生命周期
        // ------------------------------------------------------------------

        private void OnDataContextChanged(object? sender, EventArgs e)
        {
            if (_subscribedVm != null)
            {
                _subscribedVm.PropertyChanged -= Vm_PropertyChanged;
                _subscribedVm.ClickThroughChanged -= Vm_ClickThroughChanged;
                _subscribedVm.ConfigChanged -= Vm_ConfigChanged;
            }

            _subscribedVm = ViewModel;
            if (_subscribedVm != null)
            {
                _subscribedVm.PropertyChanged += Vm_PropertyChanged;
                _subscribedVm.ClickThroughChanged += Vm_ClickThroughChanged;
                _subscribedVm.ConfigChanged += Vm_ConfigChanged;

                Height = _subscribedVm.IsExpanded ? ExpandedHeight : CollapsedHeight;
            }

            ApplyBackdrop();
            ApplyTopmost();
        }

        private void OnWinOpened(object? sender, EventArgs e)
        {
            var handle = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            if (handle != IntPtr.Zero)
            {
                // 从 Alt+Tab 列表隐藏；按配置应用穿透
                Win32WindowHelper.HideFromAltTab(handle);
                Win32WindowHelper.SetClickThrough(handle, ViewModel?.IsClickThrough ?? false);
            }

            PositionAtTopCenter();
            ApplyBackdrop();
            ApplyTopmost();

            // Ctrl+Alt+T 全局快捷键轮询（150ms，按下沿触发一次，需松开后才可再次触发）
            _hotkeyTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            _hotkeyTimer.Tick += (s, _) =>
            {
                bool down = Win32WindowHelper.IsHotkeyPressed();
                if (down && !_hotkeyWasDown && ViewModel != null)
                {
                    ViewModel.SetClickThrough(!ViewModel.IsClickThrough);
                }
                _hotkeyWasDown = down;
            };
            _hotkeyTimer.Start();
        }

        private void OnWinClosing(object? sender, WindowClosingEventArgs e)
        {
            if (!_allowClose)
            {
                e.Cancel = true;
                Hide(); // 关闭 = 隐藏到托盘
            }
        }

        /// <summary>托盘"退出"调用：真正关闭窗口并停止后台刷新。</summary>
        public void ForceClose()
        {
            _allowClose = true;
            ViewModel?.Stop();
            _hotkeyTimer?.Stop();
            Close();
        }

        /// <summary>停靠到屏幕上方居中。</summary>
        private void PositionAtTopCenter()
        {
            try
            {
                var screen = Screens.Primary;
                if (screen == null) return;

                double scale = RenderScaling;
                var wa = screen.WorkingArea;
                int w = (int)(Width * scale);
                Position = new PixelPoint(
                    wa.X + Math.Max(0, (wa.Width - w) / 2),
                    wa.Y + (int)(12 * scale));
            }
            catch (Exception ex)
            {
                LogService.Warn("定位窗口失败: " + ex.Message);
            }
        }

        // ------------------------------------------------------------------
        // VM 联动
        // ------------------------------------------------------------------

        private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.IsExpanded))
            {
                Height = ViewModel?.IsExpanded == true ? ExpandedHeight : CollapsedHeight;
            }
        }

        private void Vm_ClickThroughChanged(object? sender, bool enabled)
        {
            var handle = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            if (handle != IntPtr.Zero)
            {
                Win32WindowHelper.SetClickThrough(handle, enabled);
            }
            (Application.Current as App)?.SyncTrayClickThrough(enabled);
        }

        private void Vm_ConfigChanged(object? sender, EventArgs e)
        {
            ApplyBackdrop();
            ApplyTopmost();
        }

        private void ApplyTopmost()
        {
            Topmost = ViewModel?.Config.Topmost ?? true;
        }

        /// <summary>托盘菜单/外部调用：设置穿透（同步 VM 与 Win32）。</summary>
        public void SetClickThrough(bool enabled)
        {
            ViewModel?.SetClickThrough(enabled);
        }

        /// <summary>按配置刷新亚克力材质（深浅色自适应 + 强度可调）。</summary>
        public void ApplyBackdrop()
        {
            try
            {
                var strength = ViewModel?.Config.BackdropStrength ?? 0.55;
                if (Acrylic.Material is ExperimentalAcrylicMaterial mat)
                {
                    bool dark = ActualThemeVariant == ThemeVariant.Dark;
                    mat.TintColor = dark ? Color.Parse("#FF1F2229") : Color.Parse("#FFF6F8FB");
                    mat.TintOpacity = strength;
                    mat.MaterialOpacity = dark ? Math.Min(1.0, strength + 0.05) : strength;
                }
            }
            catch (Exception ex)
            {
                LogService.Warn("应用亚克力失败: " + ex.Message);
            }
        }

        // ------------------------------------------------------------------
        // 顶栏胶囊拖拽排序
        // ------------------------------------------------------------------

        private void Capsule_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is not Border b || b.DataContext is not CapsuleViewModel cap) return;
            var point = e.GetCurrentPoint(this);
            if (!point.Properties.IsLeftButtonPressed) return;

            _dragBorder = b;
            _dragKind = cap.Kind;
            _dragging = false;
            _dragStart = e.GetPosition(this);
            e.Pointer.Capture(b);
        }

        private void Capsule_PointerMoved(object? sender, PointerEventArgs e)
        {
            if (_dragBorder == null || _dragKind == null) return;

            var pos = e.GetPosition(this);
            if (!_dragging)
            {
                // 超过阈值才进入拖拽，避免普通点击误触
                if (Math.Abs(pos.X - _dragStart.X) > 5 || Math.Abs(pos.Y - _dragStart.Y) > 5)
                {
                    _dragging = true;
                    _dragBorder.Opacity = 0.65;
                }
                return;
            }
        }

        private void Capsule_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_dragging && _dragKind != null)
            {
                var target = FindDropTarget(e.GetPosition(this), _dragKind);
                EndDrag();
                if (target.HasValue)
                {
                    ViewModel?.MoveCapsuleRelative(_dragKind, target.Value.kind, target.Value.before);
                }
            }
            else
            {
                EndDrag();
            }
        }

        private void Capsule_PointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
        {
            EndDrag();
        }

        private void EndDrag()
        {
            if (_dragBorder != null)
            {
                _dragBorder.Opacity = 1;
            }
            _dragBorder = null;
            _dragKind = null;
            _dragging = false;
        }

        /// <summary>根据落点找目标胶囊：返回 (目标 Kind, 是否插入到目标之前)。</summary>
        private (string kind, bool before)? FindDropTarget(Point pos, string dragKind)
        {
            foreach (var b in RootBorder.GetVisualDescendants().OfType<Border>()
                         .Where(x => x.Classes.Contains("capsule")))
            {
                if (b.DataContext is CapsuleViewModel cap && cap.Kind != dragKind)
                {
                    var topLeft = b.TranslatePoint(new Point(0, 0), this);
                    if (topLeft.HasValue)
                    {
                        var rect = new Rect(topLeft.Value, b.Bounds.Size);
                        if (rect.Contains(pos))
                        {
                            return (cap.Kind, pos.X < rect.Center.X);
                        }
                    }
                }
            }
            return null;
        }
    }
}
