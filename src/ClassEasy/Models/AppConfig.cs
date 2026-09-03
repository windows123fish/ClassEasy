namespace ClassEasy.Models
{
    /// <summary>
    /// 应用运行配置（窗口行为、显示名称等）。
    /// </summary>
    public class AppConfig
    {
        /// <summary>窗口是否置顶显示。</summary>
        public bool Topmost { get; set; } = true;

        /// <summary>窗口初始 X 坐标。</summary>
        public double WindowLeft { get; set; } = 100;

        /// <summary>窗口初始 Y 坐标。</summary>
        public double WindowTop { get; set; } = 100;

        /// <summary>界面标题 / 学校名称。</summary>
        public string SchoolName { get; set; } = "ClassEasy 课表";

        /// <summary>主题名称（对应 ThemeService 中的预设主题）。</summary>
        public string Theme { get; set; } = "淡白";

        /// <summary>界面字体族名称。</summary>
        public string FontFamily { get; set; } = "Microsoft YaHei UI";

        /// <summary>主时间字号。</summary>
        public double TimeFontSize { get; set; } = 40.0;

        /// <summary>天气查询城市。</summary>
        public string City { get; set; } = "上海";

        /// <summary>天气数据源：uapis / openmeteo / qweather（预留）。默认 uapis。</summary>
        public string WeatherSource { get; set; } = "uapis";

        /// <summary>是否显示节假日（在日期后面标注，如"周三 国庆节"）。</summary>
        public bool ShowHoliday { get; set; } = true;

        /// <summary>是否开机自动启动。</summary>
        public bool AutoStart { get; set; } = false;

        /// <summary>主窗口透明度（0.3 = 很淡  ~ 1.0 = 完全不透明）。</summary>
        public double WindowOpacity { get; set; } = 0.92;

        /// <summary>磨砂背景的白底不透明度（0.2 = 非常透  ~ 1.0 = 全白不透）。
        /// Win11 上叠在系统 Acrylic 上，Win7-10 上作为半透渐变；越大越"实"，越小越"虚"。</summary>
        public double BackdropOpacity { get; set; } = 0.55;

        /// <summary>窗口是否处于"点击穿透"模式（鼠标移到窗口上时点到后面的程序）。
        /// 可通过托盘菜单或 Ctrl+Alt+T 切换。</summary>
        public bool ClickThroughMode { get; set; } = false;

        /// <summary>授时中心同步频率（分钟，5~120）。默认每 30 分钟同步一次国家授时中心。</summary>
        public int NtpSyncIntervalMinutes { get; set; } = 30;

        /// <summary>开发者模式开关。开启后下次启动才会真正生效，配置中可解锁高级设置面板。</summary>
        public bool DevMode { get; set; } = false;

        /// <summary>开发者自定义 NTP 服务器列表（逗号或换行分隔）。为空时使用内置清单；非空时优先使用本列表。</summary>
        public string CustomNtpServers { get; set; } = "";

        /// <summary>日志最低输出级别：Info / Warning / Error。开发调试时可设为 Info 看到更详细日志。</summary>
        public string LogLevel { get; set; } = "Info";
    }
}