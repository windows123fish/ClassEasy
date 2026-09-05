using System;

namespace ClassEasy.Models;

/// <summary>
/// 课表中的一节课，关联时间点与科目。
/// </summary>
public class ClassInfo
{
    public Guid SubjectId { get; set; } = Guid.Empty;

    /// <summary>在课表中的索引位置</summary>
    public int Index { get; set; }

    /// <summary>是否启用（临时停用某节课而不删除）</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>是否为临时换课</summary>
    public bool IsChanged { get; set; }

    /// <summary>空课程</summary>
    public bool IsEmpty { get; set; }

    public static ClassInfo Empty => new() { IsEmpty = true };
}
