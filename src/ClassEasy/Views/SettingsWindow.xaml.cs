using System;
using System.Linq;
using System.Windows;
using ClassEasy.Models;
using ClassEasy.Services;

namespace ClassEasy.Views
{
    /// <summary>
    /// 设置窗口：调整主题、字体与时间字号并保存。
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private readonly AppConfig _config;
        private readonly ViewModels.MainViewModel _mainVm;
        private bool _devModeToggledByUser; // 用户本次会话中是否切换过 dev 模式

        /// <summary>保存成功后触发，用于通知主窗口刷新（如天气城市变更）。</summary>
        public event EventHandler Saved;

        public SettingsWindow()
        {
            InitializeComponent();

            _config = ConfigService.Load<AppConfig>(Paths.ConfigFile);

            // 从应用主窗口拿到 VM，用来实时预览/取消回滚透明度。
            if (Application.Current.MainWindow is MainWindow mw)
            {
                _mainVm = mw.ViewModel;
            }

            ThemeCombo.ItemsSource = ThemeService.Themes;
            ThemeCombo.DisplayMemberPath = "Name";
            FontCombo.ItemsSource = ThemeService.FontFamilies;
            WeatherSourceCombo.ItemsSource = new[] { "uapis", "openmeteo" };

            CityBox.Text = _config.City;
            WeatherSourceCombo.SelectedItem = string.IsNullOrEmpty(_config.WeatherSource) ? "uapis" : _config.WeatherSource;
            ThemeCombo.SelectedItem = ThemeService.FindTheme(_config.Theme);
            FontCombo.SelectedItem = _config.FontFamily;
            FontSizeSlider.Value = _config.TimeFontSize;
            BackdropSlider.Value = _config.BackdropOpacity;
            NtpIntervalSlider.Value = Math.Max(1, Math.Min(120, _config.NtpSyncIntervalMinutes));

            UpdateFontSizeText();
            UpdateBackdropText();
            UpdateNtpIntervalText();
            FontSizeSlider.ValueChanged += (sender, args) => UpdateFontSizeText();
            NtpIntervalSlider.ValueChanged += (sender, args) => UpdateNtpIntervalText();
            BackdropSlider.ValueChanged += BackdropSlider_ValueChanged;

            // 高级设置面板：仅在 config 已开启 dev 模式时显示
            AdvancedSection.Visibility = _config.DevMode ? Visibility.Visible : Visibility.Collapsed;
            CustomNtpBox.Text = _config.CustomNtpServers ?? "";
            LogLevelCombo.ItemsSource = new[] { "Info", "Warning", "Error" };
            LogLevelCombo.SelectedItem = string.IsNullOrEmpty(_config.LogLevel) ? "Info" : _config.LogLevel;

            // dev 模式开关初始化（注意：放在 AdvancedSection 设置之后，避免 Checked 事件被误触发）
            DevModeCheck.IsChecked = _config.DevMode;

            // 普通功能开关
            ShowHolidayCheck.IsChecked = _config.ShowHoliday;
            AutoStartCheck.IsChecked = AutoStartService.IsEnabled();

            // 展示当前授时来源和最后成功的服务器（只读信息）
            UpdateNtpStatus();
        }

        private void UpdateNtpIntervalText()
        {
            NtpIntervalValue.Text = Math.Round(NtpIntervalSlider.Value, 0) + " 分钟";
        }

        private void UpdateNtpStatus()
        {
            string src = NtpTimeService.ClockSourceText;
            if (NtpTimeService.IsSynchronized)
            {
                string cat = string.IsNullOrEmpty(NtpTimeService.LastCategory)
                    ? "" : "「" + NtpTimeService.LastCategory + "」";
                string server = string.IsNullOrEmpty(NtpTimeService.LastServer)
                    ? "" : " — " + NtpTimeService.LastServer;
                NtpStatusText.Text = "当前：" + src + "  " + cat + server;
            }
            else
            {
                NtpStatusText.Text = "当前：" + src + "  （启动后首次同步可能需要几秒钟）";
            }
        }

        private void UpdateFontSizeText()
        {
            FontSizeValue.Text = Math.Round(FontSizeSlider.Value) + " px";
        }

        private void UpdateBackdropText()
        {
            if (BackdropValue == null) return;
            BackdropValue.Text = Math.Round(BackdropSlider.Value * 100, 0) + " %";
        }

        /// <summary>拖动磨砂背景透明度滑块：实时预览主窗口磨砂效果。</summary>
        private void BackdropSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (BackdropValue == null || BackdropSlider == null) return;
            UpdateBackdropText();
            if (_mainVm != null) _mainVm.BackdropOpacity = BackdropSlider.Value;
        }

        /// <summary>用户切换"开发者模式"开关：立即写入 config，弹窗询问是否立即重启。
        /// 重启后 dev 模式才会真正生效。</summary>
        private void DevModeCheck_Changed(object sender, RoutedEventArgs e)
        {
            bool isChecked = DevModeCheck.IsChecked == true;
            bool changed = (_config.DevMode != isChecked);
            _devModeToggledByUser = changed;

            if (changed)
            {
                // 先保存，再问是否立即重启（保证新进程启动时配置已落盘）
                _config.DevMode = isChecked;
                try
                {
                    ConfigService.Save(Paths.ConfigFile, _config);
                }
                catch (Exception ex)
                {
                    LogService.Error("保存开发者模式开关失败", ex);
                }

                string action = isChecked ? "开启" : "关闭";
                var result = MessageBox.Show(
                    $"开发者模式已{action}，需要重启 ClassEasy 才会生效。\n是否立即重启？",
                    "ClassEasy",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // 启动新进程 + Shutdown 自己（SettingsWindow 随 App 一起退出，无需手动 Close）
                    var app = Application.Current as App;
                    app?.Restart();
                    return;
                }

                // 用户拒绝立即重启：显示提示，让用户自己挑时机重启
                DevModeWarning.Visibility = Visibility.Visible;
            }
            else
            {
                DevModeWarning.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>强制刷新天气（开发者模式专用按钮）。</summary>
        private void ForceRefreshWeather_Click(object sender, RoutedEventArgs e)
        {
            _mainVm?.RefreshWeather();
            UpdateNtpStatus();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var theme = (ThemeDefinition)ThemeCombo.SelectedItem;
                _config.City = string.IsNullOrWhiteSpace(CityBox.Text) ? "北京" : CityBox.Text.Trim();
                _config.WeatherSource = (WeatherSourceCombo.SelectedItem as string) ?? "uapis";
                _config.Theme = theme?.Name ?? "蓝色";
                _config.FontFamily = FontCombo.SelectedItem?.ToString() ?? "Microsoft YaHei UI";
                _config.TimeFontSize = FontSizeSlider.Value;
                _config.BackdropOpacity = BackdropSlider.Value;
                _config.NtpSyncIntervalMinutes = (int)Math.Round(NtpIntervalSlider.Value, 0);
                _config.ShowHoliday = ShowHolidayCheck.IsChecked == true;
                // 开机启动：配置项与注册表同步
                bool autoStart = AutoStartCheck.IsChecked == true;
                _config.AutoStart = autoStart;
                AutoStartService.SetEnabled(autoStart);

                // 高级设置（dev 模式专属），即使面板不可见也安全写入
                _config.CustomNtpServers = CustomNtpBox?.Text ?? "";
                _config.LogLevel = (LogLevelCombo?.SelectedItem as string) ?? "Info";

                ConfigService.Save(Paths.ConfigFile, _config);
                ThemeService.Apply(_config);

                Saved?.Invoke(this, EventArgs.Empty);
                ShowToast("设置更改成功");
            }
            catch (Exception ex)
            {
                LogService.Error("保存设置失败", ex);
                MessageBox.Show("保存设置时发生错误，详情请查看日志。", "ClassEasy",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // 设置窗口保持打开，方便用户继续调整；点击右上角 X 或「取消」再关闭。
            }
        }

        private System.Windows.Threading.DispatcherTimer _toastTimer;

        /// <summary>在设置窗口顶部显示提示条，2 秒后自动隐藏。</summary>
        private void ShowToast(string message)
        {
            ToastBar.Visibility = Visibility.Visible;
            _toastTimer?.Stop();
            _toastTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _toastTimer.Tick += (s, args) =>
            {
                _toastTimer.Stop();
                ToastBar.Visibility = Visibility.Collapsed;
            };
            _toastTimer.Start();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            // 用户取消：磨砂背景透明度预览回滚到上次保存值。
            if (_mainVm != null) _mainVm.BackdropOpacity = _config.BackdropOpacity;
            Close();
        }
    }
}
