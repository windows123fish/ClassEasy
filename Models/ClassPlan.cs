using System;
using System.Collections.ObjectModel;

namespace ClassEasy.Models;

/// <summary>
/// 课表：一周中每天的课程安排，关联时间表与课程列表。
/// </summary>
public class ClassPlan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "新课表";

    /// <summary>关联的时间表ID</summary>
    public Guid TimeLayoutId { get; set; } = Guid.Empty;

    /// <summary>一周中每天的课程列表（周一到周日，索引 0-6）</summary>
    public ObservableCollection<ClassInfo> Classes { get; set; } = new();

    /// <summary>触发规则：周几启用（0=周一，6=周日）</summary>
    public int WeekDay { get; set; }

    /// <summary>是否启用</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>是否为临时层课表（换课用）</summary>
    public bool IsOverlay { get; set; }

    /// <summary>临时层源课表ID</summary>
    public Guid? OverlaySourceId { get; set; }
}
