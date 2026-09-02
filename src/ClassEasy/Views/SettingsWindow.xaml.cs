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

        /// <summary>保存成功后触发，用于通知主窗口刷新（如天气城市变更）。</summary>
        public event EventHandler Saved;

        public SettingsWindow()
        {
            InitializeComponent();

            _config = ConfigService.Load<AppConfig>(Paths.ConfigFile);

            ThemeCombo.ItemsSource = ThemeService.Themes;
            ThemeCombo.DisplayMemberPath = "Name";
            FontCombo.ItemsSource = ThemeService.FontFamilies;

            CityBox.Text = _config.City;
            ThemeCombo.SelectedItem = ThemeService.FindTheme(_config.Theme);
            FontCombo.SelectedItem = _config.FontFamily;
            FontSizeSlider.Value = _config.TimeFontSize;

            UpdateFontSizeText();
            FontSizeSlider.ValueChanged += (sender, args) => UpdateFontSizeText();
        }

        private void UpdateFontSizeText()
        {
            FontSizeValue.Text = Math.Round(FontSizeSlider.Value) + " px";
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
            Close();
        }
    }
}