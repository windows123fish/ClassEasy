using System;
using System.Collections.Generic;
using ClassEasy.Models;

namespace ClassEasy.Services;

/// <summary>
/// 把 XlsxReader 读取的二维网格转换为课表（List&lt;Lesson&gt;）。
/// 约定表头：第一行为 节次 | 开始 | 结束 | 周一 | 周二 | ... | 周五（可含周六/周日）。
/// 从第二行起，每行定义一个课时段，各星期列填课程名。
/// </summary>
public static class ScheduleParser
{
    private static readonly Dictionary<string, DayOfWeek> DayMap = new()
    {
        ["一"] = DayOfWeek.Monday, ["二"] = DayOfWeek.Tuesday, ["三"] = DayOfWeek.Wednesday,
        ["四"] = DayOfWeek.Thursday, ["五"] = DayOfWeek.Friday,
        ["六"] = DayOfWeek.Saturday, ["日"] = DayOfWeek.Sunday,
    };

    public static (List<Lesson> lessons, string message) Parse(string[,] grid)
    {
        var lessons = new List<Lesson>();
        var rows = grid.GetLength(0);
        var cols = grid.GetLength(1);

        if (rows < 2 || cols < 3)
            return (lessons, "课表行数不足（至少需表头 + 1 行数据、3 列）。");

        // 定位列
        int startCol = -1, endCol = -1;
        var dayCols = new List<(int col, DayOfWeek day)>();

        for (int c = 0; c < cols; c++)
        {
            var header = grid[0, c]?.Trim() ?? "";
            if (header.Contains("开始")) startCol = c;
            else if (header.Contains("结束")) endCol = c;

            foreach (var kv in DayMap)
            {
                if (header.Contains(kv.Key))
                {
                    dayCols.Add((c, kv.Value));
                    break;
                }
            }
        }

        if (startCol < 0 || endCol < 0)
            return (lessons, "未找到\"开始\"或\"结束\"时间列。");

        int added = 0;
        for (int r = 1; r < rows; r++)
        {
            var startText = grid[r, startCol]?.Trim();
            var endText = grid[r, endCol]?.Trim();

            if (!TimeSpan.TryParse(startText, out var start) || !TimeSpan.TryParse(endText, out var end))
                continue;

            foreach (var (col, day) in dayCols)
            {
                var name = grid[r, col]?.Trim();
                if (string.IsNullOrEmpty(name))
                    continue;

                var subject = new Subject { Name = name };
                lessons.Add(new Lesson { Subject = subject, Start = start, End = end, WeekDay = ((int)day + 6) % 7 });
                added++;
            }
        }

        return (lessons, added > 0 ? $"成功导入 {added} 节课。" : "未解析到任何课程，请检查表头列名和课程内容。");
    }
}
