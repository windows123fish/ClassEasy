using System;
using System.Threading;
using System.Windows;
using ClassEasy.Models;
using ClassEasy.Services;

namespace ClassEasy
{
    /// <summary>
    /// 应用程序入口。负责单实例控制、系统托盘与主窗口生命周期。
    /// </summary>
    public partial class App : Application
    {
        private Mutex _mutex;
        private bool _ownsMutex;
        private TrayIconService _trayIcon;
        private Views.MainWindow _mainWindow;
        private Views.SettingsWindow _settingsWindow;
        private Views.ScheduleEditorWindow _scheduleEditorWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
            // 注册全局异常处理，记录日志，避免未处理异常导致程序静默崩溃。
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            // 单实例控制：避免重复启动多个悬浮窗。
            const string mutexName = "ClassEasy_SingleInstance";
            _mutex = new Mutex(true, mutexName, out bool createdNew);
            _ownsMutex = createdNew;
            if (!createdNew)
            {
                MessageBox.Show("ClassEasy 已经在运行中。", "ClassEasy",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }

            // 加载配置并应用主题/字体。
            var config = ConfigService.Load<AppConfig>(Paths.ConfigFile);
            ThemeService.Apply(config);

            base.OnStartup(e);

            _mainWindow = new Views.MainWindow();
            _mainWindow.Show();

            _trayIcon = new TrayIconService(
                showMainWindow: ShowMainWindow,
                openSettings: OpenSettings,
                openScheduleEditor: OpenScheduleEditor,
                setClickThrough: enable => _mainWindow.SetClickThrough(enable, persist: true),
                getClickThrough: () => _mainWindow?.ViewModel?.IsClickThrough ?? false,
                exitApplication: ExitApplication);
        }

        private void OnDispatcherUnhandledException(object sender,
            System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            // 记录日志后标记为已处理，避免进程立即崩溃；
            // 但记录下异常，便于诊断而非静默吞掉。
            LogService.Error("未处理的 UI 线程异常", e.Exception);
            e.Handled = true;
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                LogService.Error("未处理的应用程序异常（进程即将退出）",
                    e.ExceptionObject as Exception);
            }
            catch
            {
                // 日志写入失败时静默忽略。
            }
        }

        private void ShowMainWindow()
        {
            _mainWindow.Show();
            _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.Activate();
        }

        private void OpenSettings()
        {
            if (_settingsWindow == null || !_settingsWindow.IsLoaded)
            {
                _settingsWindow = new Views.SettingsWindow { Owner = _mainWindow };
                _settingsWindow.Saved += (sender, args) => _mainWindow.ViewModel.RefreshWeather();
            }

            _settingsWindow.Show();
            _settingsWindow.Activate();
        }

        private void OpenScheduleEditor()
        {
            if (_scheduleEditorWindow == null || !_scheduleEditorWindow.IsLoaded)
            {
                _scheduleEditorWindow = new Views.ScheduleEditorWindow { Owner = _mainWindow };
                _scheduleEditorWindow.Saved += (sender, args) => _mainWindow.ViewModel.ReloadSchedule();
            }

            _scheduleEditorWindow.Show();
            _scheduleEditorWindow.Activate();
        }

        private void ExitApplication()
        {
            _trayIcon?.Dispose();
            _trayIcon = null;
            Shutdown();
        }

        /// <summary>
        /// 重启当前进程：先释放单例 Mutex 让新进程能拿到所有权，再启动新进程，最后 Shutdown 当前进程。
        /// 用于"开发者模式"等需要重启才生效的设置。
        /// </summary>
        public void Restart()
        {
            try
            {
                // 1) 主动释放 Mutex（必须在启动新进程之前），否则新进程会因拿不到 mutex 而退出
                if (_mutex != null && _ownsMutex)
                {
                    try { _mutex.ReleaseMutex(); }
                    catch (Exception ex) { LogService.Warn("重启时释放 Mutex 失败：" + ex.Message); }
                    _ownsMutex = false;
                }

                // 2) 启动新进程（用当前 exe 路径；Environment.ProcessPath 兼容 .NET 4.8 通过 MainModule 取）
                var exe = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
                if (!string.IsNullOrEmpty(exe))
                {
                    System.Diagnostics.Process.Start(exe);
                }
            }
            catch (Exception ex)
            {
                LogService.Error("自动重启失败，请手动重启 ClassEasy", ex);
                MessageBox.Show("自动重启失败：" + ex.Message + "\n请手动退出后重新启动。",
                    "ClassEasy", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 3) 关闭当前进程
            Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _trayIcon?.Dispose();
            if (_mutex != null)
            {
                // 只有当前线程拥有所有权（createdNew=true）时才允许释放，
                // 否则 ReleaseMutex 会抛 ApplicationException。
                if (_ownsMutex)
                {
                    try
                    {
                        _mutex.ReleaseMutex();
                    }
                    catch (Exception ex)
                    {
                        LogService.Error("释放 Mutex 失败", ex);
                    }
                }
                _mutex.Dispose();
                _mutex = null;
            }
            base.OnExit(e);
        }
    }
}