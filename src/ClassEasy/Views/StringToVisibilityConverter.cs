using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ClassEasy.Views
{
    /// <summary>字符串 → Visibility：非空字符串 Visible，空或 null 则 Collapsed。</summary>
    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
