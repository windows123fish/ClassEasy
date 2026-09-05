using System;

namespace ClassEasy.Models;

/// <summary>
/// 科目：课程的基本信息单元，可被多个课表复用。
/// </summary>
public class Subject
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Initial { get; set; } = string.Empty;
    public string TeacherName { get; set; } = string.Empty;

    /// <summary>默认科目颜色（十六进制，用于界面标记）</summary>
    public string Color { get; set; } = "#FF4A90E2";

    public static Subject Empty => new() { Name = "", Initial = "" };
    public static Subject Breaking => new() { Name = "课间休息", Initial = "休" };
    public static Subject Fallback => new() { Name = "???", Initial = "?" };
}
