using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace ClassEasy.Views
{
    /// <summary>
    /// 轻量提示窗口：淡入显示后自动关闭，用于"设置更改成功"等短暂通知。
    /// </summary>
    public partial class ToastWindow : Window
    {
        public ToastWindow(string message)
        {
            InitializeComponent();
            ToastText.Text = message;
            Loaded += ToastWindow_Loaded;
        }

        private void ToastWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 2 秒后关闭
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            timer.Tick += (s, args) =>
            {
                timer.Stop();
                Close();
            };
            timer.Start();
        }
    }
}
