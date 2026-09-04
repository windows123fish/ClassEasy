using System;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ClassEasy.Models;

namespace ClassEasy.Services
{
    /// <summary>
    /// 天气服务：支持多数据源切换。
    ///   - uapis.cn：国内城市较全，返回中文天气描述；
    ///   - open-meteo.com：全球免费，无需 API Key，适合国外城市或 uapis 失败时回退。
    /// </summary>
    public static class WeatherService
    {
        private static readonly HttpClient Client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        /// <summary>
        /// 按指定数据源获取指定城市的实时天气。
        /// </summary>
        /// <param name="city">城市名（中文或英文）。</param>
        /// <param name="source">数据源标识：uapis / openmeteo。</param>
        public static async Task<WeatherInfo> GetWeatherAsync(string city, string source = "uapis")
        {
            string src = (source ?? "uapis").Trim().ToLowerInvariant();

            if (src == "openmeteo")
            {
                return await GetOpenMeteoAsync(city).ConfigureAwait(false);
            }

            // 默认 uapis
            return await GetUapisAsync(city).ConfigureAwait(false);
        }

        private static async Task<WeatherInfo> GetUapisAsync(string city)
        {
            string url = "https://uapis.cn/api/v1/misc/weather?lang=zh&city="
                         + Uri.EscapeDataString(city);

            string json = await Client.GetStringAsync(url).ConfigureAwait(false);

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
                ReportTime = ExtractString(json, "report_time"),
                Source = "uapis"
            };
        }

        private static async Task<WeatherInfo> GetOpenMeteoAsync(string city)
        {
            // 1. 用 Open-Meteo Geocoding API 把城市名转成经纬度
            string geoUrl = "https://geocoding-api.open-meteo.com/v1/search?name="
                            + Uri.EscapeDataString(city)
                            + "&count=1&language=zh&format=json";

            string geoJson = await Client.GetStringAsync(geoUrl).ConfigureAwait(false);
            double lat = ExtractDouble(geoJson, "latitude");
            double lon = ExtractDouble(geoJson, "longitude");
            string resolvedCity = ExtractString(geoJson, "name");
            string country = ExtractString(geoJson, "country");

            if (lat == 0 && lon == 0)
            {
                throw new InvalidOperationException("Open-Meteo 无法解析城市：" + city);
            }

            // 2. 用经纬度取当前天气（摄氏度）
            string weatherUrl = $"https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}&current_weather=true";
            string weatherJson = await Client.GetStringAsync(weatherUrl).ConfigureAwait(false);

            double temp = ExtractDouble(weatherJson, "temperature");
            int code = ExtractInt(weatherJson, "weathercode");

            return new WeatherInfo
            {
                City = string.IsNullOrEmpty(resolvedCity) ? city : resolvedCity,
                Province = country,
                Weather = WeatherCodeToText(code),
                Temperature = (int)Math.Round(temp),
                WindDirection = "",
                WindPower = "",
                Humidity = 0,
                ReportTime = "",
                Source = "openmeteo"
            };
        }

        /// <summary>WMO Weather interpretation codes 转中文描述。</summary>
        private static string WeatherCodeToText(int code)
        {
            switch (code)
            {
                case 0: return "晴";
                case 1: case 2: case 3: return "多云";
                case 45: case 48: return "雾";
                case 51: case 53: case 55: return "毛毛雨";
                case 56: case 57: return "冻雨";
                case 61: case 63: case 65: return "雨";
                case 66: case 67: return "雨夹雪";
                case 71: case 73: case 75: return "雪";
                case 77: return "雪粒";
                case 80: case 81: case 82: return "阵雨";
                case 85: case 86: return "阵雪";
                case 95: return "雷雨";
                case 96: case 99: return "雷暴伴冰雹";
                default: return "未知";
            }
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

        private static double ExtractDouble(string json, string key)
        {
            var match = Regex.Match(json, "\""+key+"\"\\s*:\\s*(-?\\d+(?:\\.\\d+)?)");
            return match.Success && double.TryParse(match.Groups[1].Value, out double v) ? v : 0;
        }
    }
}
