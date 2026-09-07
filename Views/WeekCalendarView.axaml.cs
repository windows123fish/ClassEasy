using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using ClassEasy.Models;
using ClassEasy.Services;

namespace ClassEasy.Views;

public partial class WeekCalendarView : Window
{
    public WeekCalendarView()
    {
        InitializeComponent();
        LoadWeek();
    }

    private void InitializeComponent()
        => AvaloniaXamlLoader.Load(this);

    private void LoadWeek()
    {
        // 共享同一份 DataTemplate（避免每个 ItemsControl 都 new 一个 FuncDataTemplate）
        var template = BuildLessonTemplate();
        MonItems.ItemTemplate = template;
        TueItems.ItemTemplate = template;
        WedItems.ItemTemplate = template;
        ThuItems.ItemTemplate = template;
        FriItems.ItemTemplate = template;
        SatItems.ItemTemplate = template;
        SunItems.ItemTemplate = template;

        BindDay(MonItems, DayOfWeek.Monday);
        BindDay(TueItems, DayOfWeek.Tuesday);
        BindDay(WedItems, DayOfWeek.Wednesday);
        BindDay(ThuItems, DayOfWeek.Thursday);
        BindDay(FriItems, DayOfWeek.Friday);
        BindDay(SatItems, DayOfWeek.Saturday);
        BindDay(SunItems, DayOfWeek.Sunday);
    }

    private void BindDay(ItemsControl control, DayOfWeek day)
    {
        int weekday = ((int)day + 6) % 7; // Monday=0, Sunday=6
        var lessons = ScheduleService.Instance.GetLessonsByWeekday(weekday);
        control.ItemsSource = lessons;
    }

    private static IDataTemplate BuildLessonTemplate()
    {
        // FuncDataTemplate 会为每个 Lesson 实例化一份 Border，由 Avalonia 负责回收，
        // 避免之前手写 new Border/StackPanel 导致的内存常驻。
        return new FuncDataTemplate<Lesson>((lesson, _) =>
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#33444444")),
                BorderBrush = new SolidColorBrush(Color.Parse("#22FFFFFF")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 6),
                Margin = new Thickness(4, 4, 4, 0)
            };

            var stack = new StackPanel { Spacing = 2 };
            stack.Children.Add(new TextBlock
            {
                [!TextBlock.TextProperty] = new Binding("Subject.Name"),
                FontWeight = FontWeight.SemiBold,
                Foreground = Brushes.White,
                FontSize = 13
            });
            stack.Children.Add(new TextBlock
            {
                [!TextBlock.TextProperty] = new Binding("TimeRange"),
                Foreground = new SolidColorBrush(Color.Parse("#AAAAAA")),
                FontSize = 11
            });
            stack.Children.Add(new TextBlock
            {
                [!TextBlock.TextProperty] = new Binding("Subject.TeacherName"),
                Foreground = new SolidColorBrush(Color.Parse("#888888")),
                FontSize = 11
            });

            border.Child = stack;
            return border;
        });
    }
}
