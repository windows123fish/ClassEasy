using Avalonia;
using Avalonia.Controls;
using ClassEasy.ViewModels;

namespace ClassEasy;

public partial class MainWindow : Window
{
    private bool _forceClose;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        ShowInTaskbar = false;

        // 屏幕正上方顶端
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        if (screen is not null)
        {
            var pos = Position;
            Position = new PixelPoint(pos.X, screen.WorkingArea.Y);
        }

        Win32Helper.HideFromAltTab(TryGetPlatformHandle()?.Handle ?? IntPtr.Zero);
        (Application.Current as App)?.EnsureTray();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (!_forceClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        base.OnClosing(e);
    }

    public void ForceClose()
    {
        _forceClose = true;
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.Dispose();
        base.OnClosed(e);
    }
}
