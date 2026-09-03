using System;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using ClassEasy.ViewModels;

namespace ClassEasy.Views
{
    /// <summary>
    /// 主悬浮课表窗口（顶部顶栏模式）。
    /// 固定在屏幕上方正中央，宽度自适应屏幕宽度的 92%，
    /// 透明度通过 ViewModel 绑定，支持实时预览。
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainViewModel ViewModel { get; }

        public MainWindow()
        {
            InitializeComponent();
            ViewModel = new MainViewModel();
            DataContext = ViewModel;

            // 系统设置变化（分辨率、DPI缩放、任务栏位置）时，重新计算窗口位置和大小。
            SystemEvents.DisplaySettingsChanged += (sender, args) => RepositionToTop();
        }

        /// <summary>
        /// 当 WPF 完成窗口句柄创建后触发，此时才可以准确获取屏幕尺寸（尊重 DPI）。
        /// </summary>
        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            RepositionToTop();
        }

        /// <summary>
        /// 把窗口放到屏幕顶部正中央，宽度取屏幕工作区的 92%，留 4% 两侧边距。
        /// 大屏（1920+）给的视觉宽度更舒展，小屏（1366）也不会左右裁切。
        /// </summary>
        private void RepositionToTop()
        {
            // PrimaryScreen.WorkingArea 会自动扣除任务栏高度，并按系统 DPI 换算为与 WPF 一致的 1/96" 单位。
            var workArea = SystemParameters.WorkArea;

            const double widthRatio = 0.92;
            const double minWidth = 960;       // 下限：信息区不挤
            const double maxWidth = 1800;      // 上限：超大屏不过宽

            double targetWidth = workArea.Width * widthRatio;
            targetWidth = Math.Max(minWidth, Math.Min(maxWidth, targetWidth));

            Width = targetWidth;
            Left = workArea.Left + (workArea.Width - targetWidth) * 0.5;
            Top = workArea.Top + 4;
        }

        protected override void OnClosed(EventArgs e)
        {
            ViewModel?.DisposeTimer();
            base.OnClosed(e);
        }

        /// <summary>鼠标拖动顶栏时移动窗口（保留之前的拖拽能力，避免顶栏挡视线时也能挪走）。</summary>
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 允许拖动，但拖完一次用户可能希望位置固定，这里保留自由拖动。
            if (e.ChangedButton == MouseButton.Left)
            {
                try { DragMove(); }
                catch { /* 忽略用户松手过快导致的异常 */ }
            }
        }

        /// <summary>
        /// 按需求将窗口放回屏幕顶部中央（若用户拖歪了，未来可在设置里一键复位，或保存位置）。
        /// 此方法留作外部调用入口。
        /// </summary>
        public void ResetPosition() => RepositionToTop();
    }
}