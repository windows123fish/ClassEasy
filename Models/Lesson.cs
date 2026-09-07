using System;

namespace ClassEasy.Models;

/// <summary>
/// 运行时解析后的一节课：包含完整的时间、科目、地点信息。
/// 由 ScheduleService 根据当前时间从 Profile 中动态生成，供 UI 直接显示。
/// </summary>
public class Lesson
{
    /// <summary>科目</summary>
    public Subject Subject { get; set; } = Subject.Fallback;

    /// <summary>开始时间</summary>
    public TimeSpan Start { get; set; }

    /// <summary>结束时间</summary>
    public TimeSpan End { get; set; }

    /// <summary>上课地点</summary>
    public string Location { get; set; } = string.Empty;

    /// <summary>学分</summary>
    public int Credits { get; set; }

    /// <summary>是否正在上</summary>
    public bool IsRunning(DateTime now)
    {
        // WeekDay 是"周一=0"的索引，转换为 DayOfWeek 时 (WeekDay + 1) % 7 是兼容的映射
        var expected = (DayOfWeek)((WeekDay + 1) % 7);
        return now.DayOfWeek == expected && now.TimeOfDay >= Start && now.TimeOfDay <= End;
    }

    /// <summary>课表中的星期（0=周一，6=周日）</summary>
    public int WeekDay { get; set; }

    /// <summary>格式化时间范围</summary>
    public string TimeRange => $"{Start:hh\\:mm} - {End:hh\\:mm}";

    /// <summary>星期简称</summary>
    public string DayShort => WeekDay switch
    {
        0 => "周一", 1 => "周二", 2 => "周三", 3 => "周四",
        4 => "周五", 5 => "周六", 6 => "周日", _ => ""
    };
}
