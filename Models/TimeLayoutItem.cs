using System;

namespace ClassEasy.Models;

/// <summary>
/// 时间点：一天中的一个时间片段，可对应上课、课间、分隔线等。
/// </summary>
public class TimeLayoutItem
{
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }

    /// <summary>
    /// 时间点类型
    /// 0 - 上课
    /// 1 - 课间
    /// 2 - 分割线
    /// </summary>
    public int TimeType { get; set; }

    /// <summary>自定义课间名称，空则用默认</summary>
    public string BreakName { get; set; } = string.Empty;

    /// <summary>实际显示的课间名称</summary>
    public string BreakNameText => string.IsNullOrEmpty(BreakName) ? "课间休息" : BreakName;

    /// <summary>持续时间</summary>
    public TimeSpan Duration => EndTime - StartTime;

    /// <summary>是否跨天（结束时间小于开始时间）</summary>
    public bool IsCrossDay => EndTime < StartTime;

    public static TimeLayoutItem Empty => new()
    {
        StartTime = TimeSpan.Zero,
        EndTime = TimeSpan.Zero,
        TimeType = 0
    };
}
