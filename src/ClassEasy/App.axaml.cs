using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ClassEasy.Models;
using ClassEasy.Services;
using ClassEasy.ViewModels;
using ClassEasy.Views;

namespace ClassEasy
{
    public class App : Application
    {
        private SettingsWindow? _settingsWindow;
        private NativeMenuItem? _trayClickThroughItem;
        private TrayIcon? _trayIcon;

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            // 按配置应用主题模式与强调色
            var config = ConfigService.Load<AppConfig>(Paths.ConfigFile);
            ThemeService.Apply(config.AccentColor, config.ThemeMode);

            // 托盘图标（用代码创建，避免 Avalonia 11 中 XAML 声明 <TrayIcon.Icons> 导致 XamlIlPopulate NRE）
            SetupTrayIcon();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainVm = new MainViewModel();
                var w = new MainWindow { DataContext = mainVm };
                desktop.MainWindow = w;
                w.Show();

                // 主窗口关闭 = 隐藏到托盘；只有"退出"才真正结束进程。
                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            }

            base.OnFrameworkInitializationCompleted();
        }

        /// <summary>
        /// 在代码中创建托盘图标 + 菜单。
        /// 说明：Avalonia 11 中直接在 XAML 里声明 TrayIcon.Icons 会导致 XamlIlPopulate 阶段 NullReferenceException，
        /// 因此改为在 OnFrameworkInitializationCompleted 中用代码构建。
        /// </summary>
        private void SetupTrayIcon()
        {
            try
            {
                // 加载嵌入的 .ico 资源（失败时 fallback 为无图标，不影响主窗口）
                WindowIcon? icon = null;
                try
                {
                    var uri = new Uri("avares://ClassEasy/Assets/appicon.ico", UriKind.Absolute);
                    using var stream = AssetLoader.Open(uri);
                    if (stream != null)
                    {
                        icon = new WindowIcon(stream);
                    }
                }
                catch (Exception ex)
                {
                    LogService.Warn("加载托盘图标失败: " + ex.Message);
                }

                var toggle = new NativeMenuItem
                {
                    Header = "鼠标穿透（Ctrl+Alt+T）",
                    ToggleType = NativeMenuItemToggleType.CheckBox
                };
                toggle.Click += OnClickThrough;
                _trayClickThroughItem = toggle;

                var menu = new NativeMenu();
                var miToggleMain = new NativeMenuItem { Header = "显示 / 隐藏课表" };
                miToggleMain.Click += OnToggleMain;
                menu.Items.Add(miToggleMain);
                menu.Items.Add(new NativeMenuItemSeparator());
                menu.Items.Add(toggle);
                menu.Items.Add(new NativeMenuItemSeparator());
                var miSettings = new NativeMenuItem { Header = "设置…" };
                miSettings.Click += OnOpenSettings;
                menu.Items.Add(miSettings);
                menu.Items.Add(new NativeMenuItemSeparator());
                var miExit = new NativeMenuItem { Header = "退出" };
                miExit.Click += OnExitApp;
                menu.Items.Add(miExit);

                _trayIcon = new TrayIcon
                {
                    ToolTipText = "ClassEasy 课表",
                    Menu = menu,
                    Icon = icon
                };

                var icons = TrayIcon.GetIcons(this);
                if (icons == null)
                {
                    icons = new TrayIcons();
                    SetValue(TrayIcon.IconsProperty, icons);
                }
                icons.Add(_trayIcon);
            }
            catch (Exception ex)
            {
                LogService.Error("创建托盘图标失败", ex);
            }
        }

        private static MainWindow? Main =>
            (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow
                as MainWindow;

        private void OnToggleMain(object? sender, EventArgs e)
        {
            var w = Main;
            if (w == null) return;
            if (w.IsVisible)
            {
                w.Hide();
            }
            else
            {
                w.Show();
                w.Activate();
            }
        }

        private void OnClickThrough(object? sender, EventArgs e)
        {
            if (sender is NativeMenuItem item)
            {
                _trayClickThroughItem = item;
                Main?.SetClickThrough(item.IsChecked);
            }
        }

        private void OnOpenSettings(object? sender, EventArgs e)
        {
            if (_settingsWindow != null)
            {
                _settingsWindow.Activate();
                return;
            }

            var owner = Main;
            if (owner == null) return;

            var vm = new SettingsViewModel(owner.ViewModel);
            _settingsWindow = new SettingsWindow { DataContext = vm };
            _settingsWindow.Closed += (s, _) => _settingsWindow = null;
            _settingsWindow.Show(owner);
        }

        private void OnExitApp(object? sender, EventArgs e)
        {
            if (Main != null)
            {
                Main.ForceClose();
            }
            (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
        }

        /// <summary>快捷键切换穿透后同步托盘菜单勾选状态。</summary>
        public void SyncTrayClickThrough(bool enabled)
        {
            if (_trayClickThroughItem != null)
            {
                _trayClickThroughItem.IsChecked = enabled;
            }
        }
    }
}
