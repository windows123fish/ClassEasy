namespace ClassEasy.Models
{
    /// <summary>
    /// 天气信息（对应 uapis.cn 天气接口返回字段）。
    /// </summary>
    public class WeatherInfo
    {
        public string City { get; set; } = "";
        public string Province { get; set; } = "";

        /// <summary>天气现象描述，如"晴""多云"。</summary>
        public string Weather { get; set; } = "";

        /// <summary>天气图标代码。</summary>
        public string WeatherIcon { get; set; } = "";

        /// <summary>当前温度（摄氏度）。</summary>
        public int Temperature { get; set; }

        /// <summary>风向。</summary>
        public string WindDirection { get; set; } = "";

        /// <summary>风力。</summary>
        public string WindPower { get; set; } = "";

        /// <summary>相对湿度。</summary>
        public int Humidity { get; set; }

        /// <summary>发布时间描述。</summary>
        public string ReportTime { get; set; } = "";

        /// <summary>实际使用的数据源（uapis / openmeteo），便于调试与显示。</summary>
        public string Source { get; set; } = "";
    }
}
