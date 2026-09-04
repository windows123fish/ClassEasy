using System.Collections.Generic;

namespace ClassEasy.Models
{
    /// <summary>
    /// 应用运行配置（外观、行为、数据源等），以 JSON 持久化。
    /// </summary>
    public class AppConfig
    {
        /// <summary>强调色（ARGB 十六进制，如 #FF0078D4）。默认蓝色系。</summary>
        public string AccentColor { get; set; } = "#FF0078D4";

        /// <summary>深浅色模式：Auto（跟随系统）/ Light / Dark。</summary>
        public string ThemeMode { get; set; } = "Auto";

        /// <summary>天气查询城市。</summary>
        public string City { get; set; } = "上海";

        /// <summary>天气数据源：uapis / openmeteo。</summary>
        public string WeatherSource { get; set; } = "uapis";

        /// <summary>是否开机自动启动。</summary>
        public bool AutoStart { get; set; } = false;

        /// <summary>窗口是否置顶。</summary>
        public bool Topmost { get; set; } = true;

        /// <summary>是否处于"鼠标穿透"模式（点击会穿过窗口）。</summary>
        public bool ClickThroughMode { get; set; } = false;

        /// <summary>亚克力背景强度（0.2~1.0），越大越"实"。</summary>
        public double BackdropStrength { get; set; } = 0.65;

        /// <summary>授时中心同步频率（分钟，5~120）。</summary>
        public int NtpSyncIntervalMinutes { get; set; } = 30;

        /// <summary>顶栏组件顺序（clock / current / next / weather），拖拽排序后持久化。</summary>
        public List<string> CapsuleOrder { get; set; } = new List<string>
        {
            "clock", "current", "next", "weather"
        };

        /// <summary>是否显示节假日标注。</summary>
        public bool ShowHoliday { get; set; } = true;

        /// <summary>是否显示天气组件。</summary>
        public bool ShowWeather { get; set; } = true;
    }
}
