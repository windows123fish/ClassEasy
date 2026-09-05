using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
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

    private void LoadWeek()
    {
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
        control.ItemTemplate = new FuncDataTemplate<Lesson>((lesson, _) =>
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
                Text = lesson.Subject.Name,
                FontWeight = FontWeight.SemiBold,
                Foreground = Brushes.White,
                FontSize = 13
            });
            stack.Children.Add(new TextBlock
            {
                Text = lesson.TimeRange,
                Foreground = new SolidColorBrush(Color.Parse("#AAAAAA")),
                FontSize = 11
            });
            if (!string.IsNullOrEmpty(lesson.Subject.TeacherName))
            {
                stack.Children.Add(new TextBlock
                {
                    Text = lesson.Subject.TeacherName,
                    Foreground = new SolidColorBrush(Color.Parse("#888888")),
                    FontSize = 11
                });
            }

            border.Child = stack;
            return border;
        });
    }
}
