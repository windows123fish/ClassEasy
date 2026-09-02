using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using ClassEasy.Models;

namespace ClassEasy.Services
{
    /// <summary>
    /// 预设主题：定义卡片背景、边框与各文字颜色。
    /// 颜色使用 ARGB 十六进制字符串，运行时转为画刷。
    /// </summary>
    public class ThemeDefinition
    {
        public string Name { get; set; }
        public string CardBackground { get; set; }      // 卡片背景
        public string CardBorder { get; set; }           // 卡片边框
        public string TitleText { get; set; }            // 学校名 / 标题
        public string TimeText { get; set; }             // 时间
        public string LessonCardBackground { get; set; } // 当前课程卡片背景
        public string LessonCardLabel { get; set; }      // "当前课程"标签
        public string LessonCardText { get; set; }       // 课程名
        public string LessonCardDetail { get; set; }     // 课程详情
        public string SecondaryText { get; set; }        // 下一节课文字
    }

    /// <summary>
    /// 主题与字体服务。将主题色写入应用级资源，供 XAML 通过 DynamicResource 引用，
    /// 修改后所有窗口自动刷新，无需手动更新控件。
    /// </summary>
    public static class ThemeService
    {
        public static readonly List<string> FontFamilies = new List<string>
        {
            "Microsoft YaHei UI", "微软雅黑", "Segoe UI", "宋体", "黑体", "楷体"
        };

        public static readonly List<ThemeDefinition> Themes = new List<ThemeDefinition>
        {
            new ThemeDefinition
            {
                Name = "蓝色", CardBackground = "#F2FFFFFF", CardBorder = "#DD4A6CF7",
                TitleText = "#2D3748", TimeText = "#4A6CF7",
                LessonCardBackground = "#334A6CF7", LessonCardLabel = "#8899DD",
                LessonCardText = "#FFFFFF", LessonCardDetail = "#DDE6FF", SecondaryText = "#718096"
            },
            new ThemeDefinition
            {
                Name = "绿色", CardBackground = "#F2FFFFFF", CardBorder = "#DD38A169",
                TitleText = "#2D3748", TimeText = "#38A169",
                LessonCardBackground = "#3338A169", LessonCardLabel = "#9AE6B4",
                LessonCardText = "#FFFFFF", LessonCardDetail = "#E6FFED", SecondaryText = "#718096"
            },
            new ThemeDefinition
            {
                Name = "紫色", CardBackground = "#F2FFFFFF", CardBorder = "#DD805AD5",
                TitleText = "#2D3748", TimeText = "#805AD5",
                LessonCardBackground = "#33805AD5", LessonCardLabel = "#D6BCFA",
                LessonCardText = "#FFFFFF", LessonCardDetail = "#F3E8FF", SecondaryText = "#718096"
            },
            new ThemeDefinition
            {
                Name = "橙色", CardBackground = "#F2FFFFFF", CardBorder = "#DDED8936",
                TitleText = "#2D3748", TimeText = "#ED8936",
                LessonCardBackground = "#33ED8936", LessonCardLabel = "#FBD38D",
                LessonCardText = "#FFFFFF", LessonCardDetail = "#FFF3E0", SecondaryText = "#718096"
            },
            new ThemeDefinition
            {
                Name = "深色", CardBackground = "#F01A202C", CardBorder = "#804A6CF7",
                TitleText = "#E2E8F0", TimeText = "#7C9CFF",
                LessonCardBackground = "#334A6CF7", LessonCardLabel = "#A3B8FF",
                LessonCardText = "#FFFFFF", LessonCardDetail = "#D0DAFF", SecondaryText = "#9AA6B2"
            }
        };

        private static Brush Parse(string hex)
        {
            return (Brush)new BrushConverter().ConvertFromString(hex);
        }

        /// <summary>根据主题名查找预设主题，找不到时返回默认（蓝色）。</summary>
        public static ThemeDefinition FindTheme(string name)
        {
            return Themes.Find(t => t.Name == name) ?? Themes[0];
        }

        /// <summary>将配置中的主题、字体与字号写入应用级资源。</summary>
        public static void Apply(AppConfig config)
        {
            if (Application.Current == null)
            {
                return;
            }

            var theme = FindTheme(config.Theme);
            var resources = Application.Current.Resources;

            resources["Brush.CardBackground"] = Parse(theme.CardBackground);
            resources["Brush.CardBorder"] = Parse(theme.CardBorder);
            resources["Brush.TitleText"] = Parse(theme.TitleText);
            resources["Brush.TimeText"] = Parse(theme.TimeText);
            resources["Brush.LessonCardBackground"] = Parse(theme.LessonCardBackground);
            resources["Brush.LessonCardLabel"] = Parse(theme.LessonCardLabel);
            resources["Brush.LessonCardText"] = Parse(theme.LessonCardText);
            resources["Brush.LessonCardDetail"] = Parse(theme.LessonCardDetail);
            resources["Brush.SecondaryText"] = Parse(theme.SecondaryText);

            resources["Font.AppFamily"] = new FontFamily(config.FontFamily);
            resources["Double.TimeFontSize"] = config.TimeFontSize;
        }
    }
}