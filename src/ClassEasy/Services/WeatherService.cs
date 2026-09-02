using System;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ClassEasy.Models;

namespace ClassEasy.Services
{
    /// <summary>
    /// 天气服务：调用 uapis.cn 免费天气接口获取指定城市的实时天气。
    /// </summary>
    public static class WeatherService
    {
        private static readonly HttpClient Client = CreateClient();

        private static HttpClient CreateClient()
        {
            // 兼容 Win7：显式启用 TLS 1.2，否则 HTTPS 请求可能失败。
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            return new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(10)
            };
        }

        /// <summary>
        /// 获取指定城市的实时天气。
        /// </summary>
        public static async Task<WeatherInfo> GetWeatherAsync(string city)
        {
            string url = "https://uapis.cn/api/v1/misc/weather?lang=zh&city="
                         + Uri.EscapeDataString(city);

            string json = await Client.GetStringAsync(url);

            return new WeatherInfo
            {
                City = ExtractString(json, "city"),
                Province = ExtractString(json, "province"),
                Weather = ExtractString(json, "weather"),
                WeatherIcon = ExtractString(json, "weather_icon"),
                Temperature = ExtractInt(json, "temperature"),
                WindDirection = ExtractString(json, "wind_direction"),
                WindPower = ExtractString(json, "wind_power"),
                Humidity = ExtractInt(json, "humidity"),
                ReportTime = ExtractString(json, "report_time")
            };
        }

        private static string ExtractString(string json, string key)
        {
            var match = Regex.Match(json, "\"" + key + "\"\\s*:\\s*\"(.*?)\"");
            return match.Success ? match.Groups[1].Value : "";
        }

        private static int ExtractInt(string json, string key)
        {
            var match = Regex.Match(json, "\"" + key + "\"\\s*:\\s*(-?\\d+)");
            return match.Success ? int.Parse(match.Groups[1].Value) : 0;
        }
    }
}