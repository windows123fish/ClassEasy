using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ClassEasy.Views
{
    /// <summary>反向 BooleanToVisibility：true → Collapsed，false → Visible。</summary>
    public class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b)
                return Visibility.Collapsed;
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility v)
                return v != Visibility.Visible;
            return false;
        }
    }
}
