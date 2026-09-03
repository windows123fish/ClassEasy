using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using ClassEasy.Models;
using ClassEasy.Services;
using ClassEasy.ViewModels;

namespace ClassEasy.Views
{
    /// <summary>
    /// 主悬浮课表窗口（顶部顶栏模式）。
    /// 特性：
    ///   1. 固定在屏幕上方正中央，宽度自适应屏幕宽度的 92%；
    ///   2. 鼠标悬停自动淡化：鼠标移到顶栏 120ms 不动 → 自动"悬停穿透"，点击命中后面窗口；
    ///      鼠标离开顶栏立即取消穿透（恢复可拖动、可点内容）；
    ///   3. 手动穿透模式（Ctrl+Alt+T / 托盘勾选）：持久化穿透；
    ///   4. 仅点击最外层空白区域才 DragMove，不吞子元素点击；
    ///   5. 分辨率/DPI 变化自动重新定位。
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainViewModel ViewModel { get; }

        /// <summary>悬停检测轮询：约每 100ms 检查一次鼠标是否压在顶栏窗口内。
        /// 用 Mouse.GetPosition(this) 而非 WPF 的 MouseEnter/Leave，
        /// 因为 WS_EX_TRANSPARENT 穿透态下 WPF 收不到 Mouse 路由事件，必须自己轮询。</summary>
        private readonly DispatcherTimer _hoverPollingTimer;

        public MainWindow()
        {
            InitializeComponent();
            ViewModel = new MainViewModel();
            DataContext = ViewModel;

            // 快捷键 Ctrl+Alt+T → 切换手动穿透模式并持久化
            ToggleThroughBinding.Command = new RelayCommand(_ => ToggleClickThroughAndPersist());

            // 穿透模式状态变化（手动穿透 / 悬停穿透任一变化）→ 真正改写 Win32 窗口扩展样式
            ViewModel.ClickThroughChanged += (sender, args) =>
            {
                bool shouldThrough = ViewModel.IsClickThrough || ViewModel.HoverClickThrough;
                Win32WindowHelper.SetClickThrough(this, shouldThrough);
            };

            // 轮询循环：判断鼠标是否压在顶栏矩形内，交给 ViewModel 做 debounce + 穿透判定
            // —— 必须用 Win32 GetCursorPos + PointFromScreen 读取全局屏幕坐标后换算，
            //    因为 WS_EX_TRANSPARENT 穿透态下 Mouse.GetPosition(this) 的结果会基于命中的下层窗口换算，造成假阴性，
            //    结果就是「进穿透→下一轮判鼠标在外→关穿透→判鼠标在内→又开穿透」的死循环闪烁。
            _hoverPollingTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(90)
            };
            _hoverPollingTimer.Tick += (sender, args) =>
            {
                var p = Win32WindowHelper.GetCursorLogicalPosition(this);
                if (p == null) return;
                Point pos = p.Value;

                bool inRect = pos.X >= -1 && pos.X <= ActualWidth + 1
                              && pos.Y >= -1 && pos.Y <= ActualHeight + 1;
                ViewModel.SetHoverDetected(inRect);
            };
            _hoverPollingTimer.Start();

            // 系统设置变化（分辨率、DPI缩放、任务栏位置）时重新计算窗口位置和大小。
            SystemEvents.DisplaySettingsChanged += (sender, args) => RepositionToTop();
        }

        /// <summary>WPF 窗口句柄就绪后：首次定位 + 同步初始穿透样式。</summary>
        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            RepositionToTop();
            bool shouldThrough = ViewModel.IsClickThrough || ViewModel.HoverClickThrough;
            Win32WindowHelper.SetClickThrough(this, shouldThrough);
        }

        /// <summary>把窗口放到屏幕顶部正中央。</summary>
        private void RepositionToTop()
        {
            var workArea = SystemParameters.WorkArea;

            const double widthRatio = 0.92;
            const double minWidth = 960;
            const double maxWidth = 1800;

            double targetWidth = workArea.Width * widthRatio;
            targetWidth = Math.Max(minWidth, Math.Min(maxWidth, targetWidth));

            Width = targetWidth;
            Left = workArea.Left + (workArea.Width - targetWidth) * 0.5;
            Top = workArea.Top + 4;
        }

        /// <summary>切换手动穿透模式并把新值写进 config.xml。</summary>
        public void ToggleClickThroughAndPersist()
        {
            bool next = !ViewModel.IsClickThrough;
            SetClickThrough(next, persist: true);
        }

        /// <summary>设置穿透模式。persist=true 时立刻写入配置文件。</summary>
        public void SetClickThrough(bool enable, bool persist)
        {
            ViewModel.IsClickThrough = enable;

            if (persist)
            {
                var cfg = ConfigService.Load<AppConfig>(Paths.ConfigFile);
                cfg.ClickThroughMode = enable;
                ConfigService.Save(Paths.ConfigFile, cfg);
            }
        }

        /// <summary>鼠标进入顶栏 → 变清晰（仅在非穿透模式下；悬停穿透的淡化效果由 EffectiveOpacity 自行处理）。</summary>
        private void Window_MouseEnter(object sender, MouseEventArgs e)
        {
            if (ViewModel != null && !ViewModel.IsClickThrough && !ViewModel.HoverClickThrough)
            {
                ViewModel.IsHovering = true;
            }
        }

        /// <summary>鼠标离开顶栏 → 淡化（非穿透模式）。</summary>
        private void Window_MouseLeave(object sender, MouseEventArgs e)
        {
            if (ViewModel != null) ViewModel.IsHovering = false;
        }

        protected override void OnClosed(EventArgs e)
        {
            _hoverPollingTimer?.Stop();
            ViewModel?.DisposeTimer();
            base.OnClosed(e);
        }

        /// <summary>
        /// 拖动窗口：只有点击最外层 RootBorder 的空白区域时才允许 DragMove；
        /// 点击内部卡片/文本/进度条/按钮，不触发 DragMove，保留给子元素处理自己的点击。
        /// </summary>
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 穿透态下 WPF 收不到该事件（Win32 已直接把消息传给后一层），保险快速返回
            if (ViewModel != null && (ViewModel.IsClickThrough || ViewModel.HoverClickThrough)) return;

            if (e.ChangedButton != MouseButton.Left) return;

            var hit = e.OriginalSource as DependencyObject;
            if (hit == null) return;

            bool hitOnRootBorder = false;
            var current = hit;
            while (current != null && !(current is Window))
            {
                if (ReferenceEquals(current, RootBorder))
                {
                    hitOnRootBorder = true;
                    break;
                }
                current = VisualTreeHelper.GetParent(current);
            }

            if (hitOnRootBorder)
            {
                try { DragMove(); }
                catch { /* 忽略用户松手过快导致的异常 */ }
            }
        }

        /// <summary>外部调用：把窗口恢复到屏幕顶部正中央。</summary>
        public void ResetPosition() => RepositionToTop();
    }
}