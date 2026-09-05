using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ClassEasy.Controls;

public partial class LessonCard : UserControl
{
    public LessonCard()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
