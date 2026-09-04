using Avalonia.Media;
using ClassEasy.Services;

namespace ClassEasy.ViewModels
{
    /// <summary>
    /// MDI（Material Design Icons）风格天气图标库。
    /// 以 StreamGeometry 缓存常用天气图标路径（24x24 viewBox）。
    /// </summary>
    public static class WeatherIconLibrary
    {
        public static StreamGeometry Sunny { get; } = Parse(
            "M12,7A5,5 0 0,1 17,12A5,5 0 0,1 12,17A5,5 0 0,1 7,12A5,5 0 0,1 12,7M12,9A3,3 0 0,0 9,12A3,3 0 0,0 12,15A3,3 0 0,0 15,12A3,3 0 0,0 12,9M12,2L14.39,5.42C13.65,5.15 12.84,5 12,5C11.16,5 10.35,5.15 9.61,5.42L12,2M3.34,7L7.59,6.62C7.19,7.22 6.87,7.88 6.67,8.59L3.34,7M3.34,17L6.67,15.41C6.87,16.12 7.19,16.78 7.59,17.38L3.34,17M20.66,17L16.41,17.38C16.81,16.78 17.13,16.12 17.33,15.41L20.66,17M20.66,7L17.33,8.59C17.13,7.88 16.81,7.22 16.41,6.62L20.66,7M12,22L9.61,18.58C10.35,18.85 11.16,19 12,19C12.84,19 13.65,18.85 14.39,18.58L12,22Z");

        public static StreamGeometry Cloud { get; } = Parse(
            "M19.35,10.04C18.67,6.59 15.64,4 12,4C9.11,4 6.6,5.64 5.35,8.04C2.34,8.36 0,10.91 0,14A6,6 0 0,0 6,20H19A5,5 0 0,0 24,15C24,12.36 21.95,10.22 19.35,10.04M19,18H6A4,4 0 0,1 2,14C2,11.95 3.53,10.24 5.56,10.03L6.63,9.92L7.13,8.97C8.08,7.14 9.94,6 12,6C14.62,6 16.88,7.86 17.39,10.43L17.69,11.93L19.22,12.04C20.78,12.14 22,13.45 22,15A3,3 0 0,1 19,18Z");

        public static StreamGeometry Rain { get; } = Parse(
            "M12,2A7,7 0 0,1 19,9C19,11.38 17.81,13.47 16,14.74V17A1,1 0 0,1 15,18H9A1,1 0 0,1 8,17V14.74C6.19,13.47 5,11.38 5,9A7,7 0 0,1 12,2M9,20H10A1,1 0 0,1 11,21V22A1,1 0 0,1 10,23H9A1,1 0 0,1 8,22V21A1,1 0 0,1 9,20M14,20H15A1,1 0 0,1 16,21V22A1,1 0 0,1 15,23H14A1,1 0 0,1 13,22V21A1,1 0 0,1 14,20Z");

        public static StreamGeometry Snow { get; } = Parse(
            "M12,2A7,7 0 0,1 19,9C19,11.38 17.81,13.47 16,14.74V17A1,1 0 0,1 15,18H9A1,1 0 0,1 8,17V14.74C6.19,13.47 5,11.38 5,9A7,7 0 0,1 12,2M9,20H10A1,1 0 0,1 11,21V22A1,1 0 0,1 10,23H9A1,1 0 0,1 8,22V21A1,1 0 0,1 9,20M13,20H14A1,1 0 0,1 15,21V22A1,1 0 0,1 14,23H13A1,1 0 0,1 12,22V21A1,1 0 0,1 13,20Z");

        public static StreamGeometry Thunder { get; } = Parse(
            "M12,2A7,7 0 0,1 19,9C19,11.38 17.81,13.47 16,14.74V17A1,1 0 0,1 15,18H13.6L11,23L12,17H9.5L11.4,13H8.6L11,7.8V9H12V2Z");

        public static StreamGeometry Fog { get; } = Parse(
            "M3,15H13A1,1 0 0,1 14,16A1,1 0 0,1 13,17H3A1,1 0 0,1 2,16A1,1 0 0,1 3,15M16,15H21A1,1 0 0,1 22,16A1,1 0 0,1 21,17H16A1,1 0 0,1 15,16A1,1 0 0,1 16,15M17,19H21A1,1 0 0,1 22,20A1,1 0 0,1 21,21H17A1,1 0 0,1 16,20A1,1 0 0,1 17,19M3,19H14A1,1 0 0,1 15,20A1,1 0 0,1 14,21H3A1,1 0 0,1 2,20A1,1 0 0,1 3,19M6,4H15.5C17.71,4 19.5,5.79 19.5,8C19.5,10.21 17.71,12 15.5,12H6C4.34,12 3,10.66 3,9C3,7.34 4.34,6 6,6C6,4.9 6.9,4 8,4H6,4Z");

        public static StreamGeometry Parse(string path)
        {
            return StreamGeometry.Parse(path);
        }

        /// <summary>按天气描述选择图标。</summary>
        public static StreamGeometry GetIcon(string? weatherText)
        {
            var t = weatherText ?? "";
            if (t.Contains("雷")) return Thunder;
            if (t.Contains("雪")) return Snow;
            if (t.Contains("雨")) return Rain;
            if (t.Contains("雾") || t.Contains("霾")) return Fog;
            if (t.Contains("阴") || t.Contains("云")) return Cloud;
            if (t.Contains("晴")) return Sunny;
            return Cloud;
        }
    }
}
