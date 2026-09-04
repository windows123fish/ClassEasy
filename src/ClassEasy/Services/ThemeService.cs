using System;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using FluentAvalonia.Styling;
using FluentAvalonia.UI.Media;

namespace ClassEasy.Services
{
    /// <summary>
    /// 主题服务：深色/浅色模式 + 自定义强调色（FluentAvalonia Fluent 2 风格）。
    /// </summary>
    public static class ThemeService
    {
        /// <summary>应用主题模式（Auto / Light / Dark）与强调色（ARGB 十六进制）。</summary>
        public static void Apply(string? accentHex, string? mode)
        {
            var app = Application.Current;
            if (app == null) return;

            // 1. 深浅色模式
            switch ((mode ?? "Auto").Trim())
            {
                case "Light":
                    app.RequestedThemeVariant = ThemeVariant.Light;
                    break;
                case "Dark":
                    app.RequestedThemeVariant = ThemeVariant.Dark;
                    break;
                default:
                    app.RequestedThemeVariant = ThemeVariant.Default;
                    break;
            }

            // 2. 强调色
            if (string.IsNullOrWhiteSpace(accentHex))
            {
                return;
            }

            try
            {
                var theme = app.Styles.OfType<FluentAvaloniaTheme>().FirstOrDefault();
                if (theme != null)
                {
                    // FluentAvaloniaTheme 在运行时改 CustomAccentColor 会自动重刷全部画刷资源
                    var color = Color.Parse(accentHex);
                    theme.CustomAccentColor = Color2.FromARGB(color.A, color.R, color.G, color.B);
                }
            }
            catch (Exception ex)
            {
                LogService.Warn("应用强调色失败: " + accentHex + " — " + ex.Message);
            }
        }

        /// <summary>预设强调色色板（类名 + 十六进制），供设置窗口使用。</summary>
        public static readonly (string Name, string Hex)[] AccentPresets =
        {
            ("蓝", "#FF0078D4"),
            ("紫", "#FF8764B8"),
            ("青", "#FF00B7C3"),
            ("绿", "#FF107C10"),
            ("橙", "#FFCA5010"),
            ("粉", "#FFC239B3"),
        };
    }
}
