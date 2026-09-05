using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using ClassEasy.ViewModels;

namespace ClassEasy;

public partial class MainWindow : Window
{
    private static readonly BoxShadows NormalShadow = new(
        new BoxShadow
        {
            OffsetX = 0,
            OffsetY = 8,
            Blur = 32,
            Spread = 0,
            Color = Color.Parse("#88000000")
        });

    private static readonly BoxShadows HoverShadow = new(
        new BoxShadow
        {
            OffsetX = 0,
            OffsetY = 12,
            Blur = 40,
            Spread = 0,
            Color = Color.Parse("#AA000000")
        });

    private bool _forceClose;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
        CardBorder.RenderTransform = TransformOperations.Identity;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        // 不显示任务栏按钮（代码设置，避免 XAML 属性触发编译器问题）
        ShowInTaskbar = false;

        // WindowStartupLocation=CenterScreen 已处理水平居中，这里只把窗口上移到距顶部 80px
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        if (screen is not null)
        {
            var pos = Position;
            Position = new PixelPoint(pos.X, (int)(screen.WorkingArea.Y + 80));
        }

        // 从 Alt+Tab 列表中隐藏（Win32 调用已封装到 Win32Helper）
        Win32Helper.HideFromAltTab(TryGetPlatformHandle()?.Handle ?? IntPtr.Zero);

        // 窗口已显示、消息循环已运行，此时创建托盘图标
        (Application.Current as App)?.EnsureTray();
    }

    /// <summary>
    /// 关闭按钮：默认隐藏到托盘，不退出程序
    /// </summary>
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

    /// <summary>
    /// 供托盘"退出"菜单项调用：真正关闭窗口
    /// </summary>
    public void ForceClose()
    {
        _forceClose = true;
        Close();
    }

    private void CardBorder_PointerEntered(object? sender, PointerEventArgs e)
    {
        var builder = TransformOperations.CreateBuilder(1);
        builder.AppendTranslate(0, -2);
        CardBorder.RenderTransform = builder.Build();
        CardBorder.BoxShadow = HoverShadow;
    }

    private void CardBorder_PointerExited(object? sender, PointerEventArgs e)
    {
        CardBorder.RenderTransform = TransformOperations.Identity;
        CardBorder.BoxShadow = NormalShadow;
    }
}