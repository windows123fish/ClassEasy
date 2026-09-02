using System;
using System.Windows;
using System.Windows.Input;
using ClassEasy.Services;

namespace ClassEasy.Views
{
    /// <summary>
    /// 悬浮课表主窗口。
    /// </summary>
    public partial class MainWindow : Window
    {
        public ViewModels.MainViewModel ViewModel { get; private set; }

        public MainWindow()
        {
            InitializeComponent();

            ViewModel = new ViewModels.MainViewModel();
            DataContext = ViewModel;

            ApplyInitialPosition();
        }

        /// <summary>
        /// 无边框窗口使用左键拖拽移动。
        /// </summary>
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void ApplyInitialPosition()
        {
            var config = ConfigService.Load<Models.AppConfig>(Paths.ConfigFile);

            var workArea = SystemParameters.WorkArea;
            Left = Math.Max(workArea.Left, Math.Min(config.WindowLeft, workArea.Right - Width));
            Top = Math.Max(workArea.Top, Math.Min(config.WindowTop, workArea.Bottom - Height));
            Topmost = config.Topmost;
        }
    }
}