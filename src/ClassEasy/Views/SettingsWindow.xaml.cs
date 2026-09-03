using System;
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

            CityBox.Text = _config.City;
            ThemeCombo.SelectedItem = ThemeService.FindTheme(_config.Theme);
            FontCombo.SelectedItem = _config.FontFamily;
            FontSizeSlider.Value = _config.TimeFontSize;
            OpacitySlider.Value = _config.WindowOpacity;
            NtpIntervalSlider.Value = Math.Max(5, Math.Min(120, _config.NtpSyncIntervalMinutes));

            UpdateFontSizeText();
            UpdateOpacityText();
            UpdateNtpIntervalText();
            FontSizeSlider.ValueChanged += (sender, args) => UpdateFontSizeText();
            NtpIntervalSlider.ValueChanged += (sender, args) => UpdateNtpIntervalText();
            OpacitySlider.ValueChanged += OpacitySlider_ValueChanged;

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
        }

        private void UpdateFontSizeText()
        {
            FontSizeValue.Text = Math.Round(FontSizeSlider.Value) + " px";
        }

        private void UpdateOpacityText()
        {
            // 对象存在性保护：XAML 加载/卸载期间任一未就绪就安全跳过，不抢跑。
            if (OpacityValue == null) return;
            OpacityValue.Text = Math.Round(OpacitySlider.Value * 100, 0) + " %";
        }

        /// <summary>拖动透明度滑块：实时预览主窗口透明度。</summary>
        private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // 双保险：InitializeComponent 过程中理论上已不会走到这里（事件在后面订阅），
            // 仍保留空判以防未来有人把事件移回 XAML。
            if (OpacityValue == null || OpacitySlider == null) return;
            UpdateOpacityText();
            _mainVm?.PreviewOpacity(OpacitySlider.Value);
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var theme = (ThemeDefinition)ThemeCombo.SelectedItem;
                _config.City = string.IsNullOrWhiteSpace(CityBox.Text) ? "北京" : CityBox.Text.Trim();
                _config.Theme = theme?.Name ?? "蓝色";
                _config.FontFamily = FontCombo.SelectedItem?.ToString() ?? "Microsoft YaHei UI";
                _config.TimeFontSize = FontSizeSlider.Value;
                _config.WindowOpacity = OpacitySlider.Value;
                _config.NtpSyncIntervalMinutes = (int)Math.Round(NtpIntervalSlider.Value, 0);

                _mainVm?.CommitOpacityToConfig();

                ConfigService.Save(Paths.ConfigFile, _config);
                ThemeService.Apply(_config);

                Saved?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                LogService.Error("保存设置失败", ex);
                MessageBox.Show("保存设置时发生错误，详情请查看日志。", "ClassEasy",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // 设置窗口通过 Show() 非模态打开，必须用 Close 关闭，
                // 不能设置 DialogResult（会抛 InvalidOperationException 导致崩溃）。
                Close();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            // 用户取消：透明度预览回滚到上次保存值，其他主题/字体/城市未保存过本来就不会生效。
            _mainVm?.RestoreOpacityFromConfig();
            Close();
        }
    }
}