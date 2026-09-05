using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace ClassEasy;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();

        Opened += (_, _) => PositionTopCenter();
        PointerPressed += OnPointerPressed;
    }

    private void PositionTopCenter()
    {
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        if (screen is null) return;

        var area = screen.WorkingArea;
        var scale = RenderScaling;
        var widthPixels = (int)(ClientSize.Width * scale);

        var x = area.X + (area.Width - widthPixels) / 2;
        var y = area.Y + (int)(40 * scale);
        Position = new PixelPoint(x, y);
    }

    // 允许拖拽移动悬浮卡片
    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }
}
