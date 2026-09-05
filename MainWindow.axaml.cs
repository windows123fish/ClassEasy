using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using ClassEasy.ViewModels;

namespace ClassEasy;

public partial class MainWindow : Window
{
    // 默认阴影：偏移 (0,8)，模糊 32，颜色 #88000000
    private static readonly BoxShadows NormalShadow = new(
        new BoxShadow
        {
            OffsetX = 0,
            OffsetY = 8,
            Blur = 32,
            Spread = 0,
            Color = Color.Parse("#88000000")
        });

    // 悬浮阴影：偏移 (0,12)，模糊 40，颜色 #AA000000
    private static readonly BoxShadows HoverShadow = new(
        new BoxShadow
        {
            OffsetX = 0,
            OffsetY = 12,
            Blur = 40,
            Spread = 0,
            Color = Color.Parse("#AA000000")
        });

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();

        // 初始变换：原位（Y=0）。用 TransformOperations 以便配合 TransformOperationsTransition 做平移动画
        CardBorder.RenderTransform = TransformOperations.Identity;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        // 屏幕水平居中，垂直靠上（距工作区顶部 80px）
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        if (screen is not null)
        {
            var wa = screen.WorkingArea;
            var x = wa.X + (wa.Width - Bounds.Width) / 2;
            var y = wa.Y + 80;
            Position = new PixelPoint((int)x, (int)y);
        }
    }

    private void CardBorder_PointerEntered(object? sender, PointerEventArgs e)
    {
        // 上浮 2px + 阴影加深（0.2s EaseOut 过渡由 XAML 的 Transitions 负责）
        var builder = TransformOperations.CreateBuilder(1);
        builder.AppendTranslate(0, -2);
        CardBorder.RenderTransform = builder.Build();
        CardBorder.BoxShadow = HoverShadow;
    }

    private void CardBorder_PointerExited(object? sender, PointerEventArgs e)
    {
        // 复位
        CardBorder.RenderTransform = TransformOperations.Identity;
        CardBorder.BoxShadow = NormalShadow;
    }
}
