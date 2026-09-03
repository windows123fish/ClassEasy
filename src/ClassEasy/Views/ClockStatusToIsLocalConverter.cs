using System;
using System.Globalization;
using System.Windows.Data;

namespace ClassEasy.Views
{
    /// <summary>
    /// 授时状态文本 → 是否是"使用本地时间"。
    /// ClockStatusText 为 "⚠ 本地时间" 时返回 true；
    /// 为 "✓ 国家授时"（或任何其他不含"本地"的字符串）时返回 false。
    /// 用法：作为授时胶囊 DataTrigger 的转换器，决定用绿色还是橙红色。
    /// </summary>
    public class ClockStatusToIsLocalConverter : IValueConverter
    {
        public static readonly ClockStatusToIsLocalConverter Instance = new ClockStatusToIsLocalConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var s = value as string;
            return !string.IsNullOrEmpty(s) && s.Contains("本地");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}