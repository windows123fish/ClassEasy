using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ClassEasy.Models;

/// <summary>
/// 档案：存储所有课表、时间表、科目的根对象。
/// </summary>
public class Profile
{
    public string Name { get; set; } = "默认档案";

    /// <summary>时间表字典</summary>
    public Dictionary<Guid, TimeLayout> TimeLayouts { get; set; } = new();

    /// <summary>课表字典</summary>
    public Dictionary<Guid, ClassPlan> ClassPlans { get; set; } = new();

    /// <summary>科目字典</summary>
    public Dictionary<Guid, Subject> Subjects { get; set; } = new();

    /// <summary>当前激活的课表ID</summary>
    public Guid? ActiveClassPlanId { get; set; }

    /// <summary>临时层课表ID（用于换课）</summary>
    public Guid? OverlayClassPlanId { get; set; }

    /// <summary>是否启用临时层</summary>
    public bool IsOverlayEnabled { get; set; }

    /// <summary>获取指定日期应该使用的课表</summary>
    public ClassPlan? GetClassPlanByDate(DateTime date)
    {
        // 优先返回临时层（如果启用且存在）
        if (IsOverlayEnabled && OverlayClassPlanId.HasValue && ClassPlans.TryGetValue(OverlayClassPlanId.Value, out var overlay))
            return overlay;

        // 根据星期匹配
        int weekday = ((int)date.DayOfWeek + 6) % 7; // Monday=0, Sunday=6
        foreach (var kvp in ClassPlans)
        {
            if (kvp.Value.IsEnabled && kvp.Value.WeekDay == weekday)
                return kvp.Value;
        }

        // 默认返回第一个启用的课表
        return ClassPlans.Values.FirstOrDefault(c => c.IsEnabled);
    }

    /// <summary>获取当前激活的课表</summary>
    public ClassPlan? GetActiveClassPlan()
    {
        if (ActiveClassPlanId.HasValue && ClassPlans.TryGetValue(ActiveClassPlanId.Value, out var plan))
            return plan;
        return ClassPlans.Values.FirstOrDefault(c => c.IsEnabled);
    }
}
