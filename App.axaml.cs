using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ClassEasy.ViewModels;
using ClassEasy.Views;

namespace ClassEasy;

public partial class App : Application
{
    private TrayIcon? _trayIcon;
    private MainWindow? _mainWindow;
    private NativeMenuItem? _toggleTopmostItem;
    private IClassicDesktopStyleApplicationLifetime? _desktop;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _desktop = desktop;
            _mainWindow = new MainWindow();
            desktop.MainWindow = _mainWindow;

            // 进程退出时显式释放托盘图标，避免 Windows shell 残留 ghost 托盘图标
            desktop.Exit += OnDesktopExit;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnDesktopExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        DisposeTrayIcon();
    }

    private void DisposeTrayIcon()
    {
        if (_trayIcon is null) return;
        try
        {
            var icons = TrayIcon.GetIcons(this);
            if (icons is not null) icons.Remove(_trayIcon);
            _trayIcon.Dispose();
        }
        catch { /* 进程退出阶段静默 */ }
        _trayIcon = null;
    }

    /// <summary>
    /// 延迟到主窗口 OnOpened（消息循环已运行）后再创建托盘图标。
    /// 若在 OnFrameworkInitializationCompleted 中创建，RenderTargetBitmap 在
    /// 软件渲染模式下会因渲染线程未就绪而死锁，导致窗口永远不显示。
    /// </summary>
    public void EnsureTray()
    {
        if (_trayIcon is not null || _desktop is null)
            return;

        BuildTrayIcon(_desktop);
    }

    private void BuildTrayIcon(IClassicDesktopStyleApplicationLifetime desktop)
    {
        _trayIcon = new TrayIcon
        {
            ToolTipText = "ClassEasy",
            Icon = new WindowIcon(LoadTrayIcon()),
            Menu = BuildTrayMenu(desktop)
        };

        // 左键：切换窗口显示/隐藏
        _trayIcon.Clicked += (_, _) =>
        {
            if (_mainWindow is null) return;
            if (_mainWindow.IsVisible)
                _mainWindow.Hide();
            else
                _mainWindow.Show();
        };

        // Avalonia 11.0.0：GetIcons 附加属性集合不会自动初始化，需判空后创建
        var icons = TrayIcon.GetIcons(this);
        if (icons is null)
        {
            icons = new TrayIcons();
            TrayIcon.SetIcons(this, icons);
        }
        icons.Add(_trayIcon);
    }

    private NativeMenu BuildTrayMenu(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var menu = new NativeMenu();

        // 显示主窗口
        var showItem = new NativeMenuItem { Header = "显示主窗口" };
        showItem.Click += (_, _) => _mainWindow?.Show();
        menu.Add(showItem);

        // 隐藏到托盘
        var hideItem = new NativeMenuItem { Header = "隐藏到托盘" };
        hideItem.Click += (_, _) => _mainWindow?.Hide();
        menu.Add(hideItem);

        // 置顶窗口（可勾选）
        _toggleTopmostItem = new NativeMenuItem
        {
            Header = "置顶窗口",
            ToggleType = NativeMenuItemToggleType.CheckBox,
            IsChecked = true
        };
        _toggleTopmostItem.Click += (_, _) =>
        {
            if (_mainWindow is not null)
                _mainWindow.Topmost = _toggleTopmostItem.IsChecked;
        };
        menu.Add(_toggleTopmostItem);

        menu.Add(new NativeMenuItemSeparator());

        // 课表子菜单：管理 + 周历 + 导入
        var scheduleMenu = new NativeMenuItem { Header = "课表", Menu = new NativeMenu() };

        var manageItem = new NativeMenuItem { Header = "课表管理..." };
        manageItem.Click += (_, _) =>
        {
            try
            {
                var w = new ScheduleManagerWindow();
                if (_mainWindow is not null)
                    w.Show(_mainWindow);
                else
                    w.Show();
            }
            catch (Exception ex)
            {
                var hwnd = _mainWindow?.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
                Win32Helper.ShowMessageBox(hwnd, "打开失败：" + ex.Message, "错误");
            }
        };
        scheduleMenu.Menu.Add(manageItem);

        var weekItem = new NativeMenuItem { Header = "周历视图..." };
        weekItem.Click += (_, _) =>
        {
            try
            {
                var w = new WeekCalendarView();
                if (_mainWindow is not null)
                    w.Show(_mainWindow);
                else
                    w.Show();
            }
            catch (Exception ex)
            {
                var hwnd = _mainWindow?.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
                Win32Helper.ShowMessageBox(hwnd, "打开失败：" + ex.Message, "错误");
            }
        };
        scheduleMenu.Menu.Add(weekItem);

        var importItem = new NativeMenuItem { Header = "从Excel导入..." };
        importItem.Click += async (_, _) => await ImportScheduleAsync();
        scheduleMenu.Menu.Add(importItem);

        menu.Add(scheduleMenu);

        // 自定义内容：手动切换卡片状态文本
        var statusMenu = new NativeMenuItem { Header = "状态", Menu = new NativeMenu() };
        foreach (var status in new[] { "今天没有课程", "正在上课", "课间休息", "已放学" })
        {
            var item = new NativeMenuItem { Header = status };
            item.Click += (_, _) =>
            {
                if (_mainWindow?.DataContext is MainWindowViewModel vm)
                    vm.SetManualStatus(status);
            };
            statusMenu.Menu.Add(item);
        }
        menu.Add(statusMenu);

        menu.Add(new NativeMenuItemSeparator());

        // 退出
        var exitItem = new NativeMenuItem { Header = "退出" };
        exitItem.Click += (_, _) =>
        {
            try
            {
                var icons = TrayIcon.GetIcons(this);
                if (icons is not null && _trayIcon is not null)
                {
                    icons.Remove(_trayIcon);
                    _trayIcon.Dispose();
                }
            }
            catch { }
            _mainWindow?.ForceClose();
            desktop.Shutdown();
        };
        menu.Add(exitItem);

        return menu;
    }

    /// <summary>
    /// 从嵌入资源加载 32x32 托盘图标。
    /// 加载位图文件不依赖渲染设备，避免软件渲染下 RenderTargetBitmap 死锁。
    /// </summary>
    private static Bitmap LoadTrayIcon()
    {
        using var stream = AssetLoader.Open(new Uri("avares://ClassEasy/Assets/tray.png"));
        return new Bitmap(stream);
    }

    /// <summary>
    /// 弹出文件选择框导入课表，并提示结果。
    /// </summary>
    private async Task ImportScheduleAsync()
    {
        if (_mainWindow is null)
            return;

#pragma warning disable CS0618 // OpenFileDialog 在 Avalonia 11.0 仍可用
        var dlg = new OpenFileDialog
        {
            Title = "选择课表 Excel 文件",
            AllowMultiple = false,
            Filters =
            {
                new FileDialogFilter { Name = "Excel 工作簿 (*.xlsx)", Extensions = { "xlsx" } }
            }
        };
        var paths = await dlg.ShowAsync(_mainWindow);
#pragma warning restore CS0618

        if (paths is null || paths.Length == 0)
            return;

        string msg = _mainWindow.DataContext is MainWindowViewModel vm
            ? vm.ImportSchedule(paths[0])
            : "未就绪。";

        var hwnd = _mainWindow.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        Win32Helper.ShowMessageBox(hwnd, msg, "导入课表");
    }
}
