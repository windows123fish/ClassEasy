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
        public string Theme { get; set; } = "蓝色";

        /// <summary>界面字体族名称。</summary>
        public string FontFamily { get; set; } = "Microsoft YaHei UI";

        /// <summary>主时间字号。</summary>
        public double TimeFontSize { get; set; } = 40.0;

        /// <summary>天气查询城市。</summary>
        public string City { get; set; } = "上海";

        /// <summary>主窗口透明度（0.3 = 很淡  ~ 1.0 = 完全不透明）。</summary>
        public double WindowOpacity { get; set; } = 0.92;

        /// <summary>窗口是否处于"点击穿透"模式（鼠标移到窗口上时点到后面的程序）。
        /// 可通过托盘菜单或 Ctrl+Alt+T 切换。</summary>
        public bool ClickThroughMode { get; set; } = false;

        /// <summary>授时中心同步频率（分钟，5~120）。默认每 30 分钟同步一次国家授时中心。</summary>
        public int NtpSyncIntervalMinutes { get; set; } = 30;
    }
}