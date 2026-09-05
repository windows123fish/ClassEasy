using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using ClassEasy.Models;

namespace ClassEasy.Services;

/// <summary>
/// 课表数据服务：基于 Profile 模型管理课表、时间表、科目，
/// 提供当前课程解析、持久化等能力。
/// </summary>
public class ScheduleService
{
    private static readonly Lazy<ScheduleService> _lazy = new(() => new ScheduleService());
    public static ScheduleService Instance => _lazy.Value;

    private readonly string _filePath;
    private Profile _profile = new();

    public Profile Profile
    {
        get => _profile;
        private set
        {
            _profile = value;
            Changed?.Invoke();
        }
    }

    /// <summary>课表数据发生变更时触发</summary>
    public event Action? Changed;

    private ScheduleService()
    {
        _filePath = ResolvePath();
        Load();
        EnsureDefaults();
    }

    // ---------- 查询 ----------

    /// <summary>获取当前时间正在上的课</summary>
    public Lesson? FindRunning(DateTime now)
        => GetTodayLessons(now).FirstOrDefault(l => l.IsRunning(now));

    /// <summary>获取今天下一节课</summary>
    public Lesson? FindNext(DateTime now)
        => GetTodayLessons(now)
            .Where(l => l.Start > now.TimeOfDay)
            .OrderBy(l => l.Start)
            .FirstOrDefault();

    /// <summary>获取今天的所有课程（已排序）</summary>
    public List<Lesson> GetTodayLessons(DateTime now)
        => GetLessonsByWeekday(((int)now.DayOfWeek + 6) % 7);

    /// <summary>获取指定星期的所有课程（0=周一，6=周日）</summary>
    public List<Lesson> GetLessonsByWeekday(int weekday)
    {
        var lessons = new List<Lesson>();

        // 按传入的 weekday 匹配课表：优先启用且 WeekDay 匹配的课表，其次回退到激活课表
        var plan = Profile.ClassPlans.Values.FirstOrDefault(c => c.IsEnabled && c.WeekDay == weekday)
                   ?? Profile.GetActiveClassPlan();
        if (plan == null || !Profile.TimeLayouts.TryGetValue(plan.TimeLayoutId, out var layout))
            return lessons;

        var classItems = layout.ClassItems.ToList();
        for (int i = 0; i < classItems.Count && i < plan.Classes.Count; i++)
        {
            var classInfo = plan.Classes[i];
            if (!classInfo.IsEnabled || classInfo.IsEmpty || classInfo.SubjectId == Guid.Empty)
                continue;

            if (!Profile.Subjects.TryGetValue(classInfo.SubjectId, out var subject))
                subject = Subject.Fallback;

            var item = classItems[i];
            lessons.Add(new Lesson
            {
                Subject = subject,
                Start = item.StartTime,
                End = item.EndTime,
                WeekDay = weekday,
                Credits = 0,
                Location = string.Empty
            });
        }

        return lessons.OrderBy(l => l.Start).ToList();
    }

    /// <summary>获取一周全部课程（周一到周日）</summary>
    public Dictionary<int, List<Lesson>> GetWeekLessons()
    {
        var result = new Dictionary<int, List<Lesson>>();
        for (int i = 0; i < 7; i++)
            result[i] = GetLessonsByWeekday(i);
        return result;
    }

    // ---------- 管理 ----------

    public void AddSubject(Subject subject)
    {
        Profile.Subjects[subject.Id] = subject;
        Save();
        Changed?.Invoke();
    }

    public void UpdateSubject(Subject subject)
    {
        if (Profile.Subjects.ContainsKey(subject.Id))
        {
            Profile.Subjects[subject.Id] = subject;
            Save();
            Changed?.Invoke();
        }
    }

    public void RemoveSubject(Guid id)
    {
        if (Profile.Subjects.Remove(id))
        {
            Save();
            Changed?.Invoke();
        }
    }

    public void AddTimeLayout(TimeLayout layout)
    {
        var id = Guid.NewGuid();
        Profile.TimeLayouts[id] = layout;
        Save();
        Changed?.Invoke();
    }

    public void AddClassPlan(ClassPlan plan)
    {
        Profile.ClassPlans[plan.Id] = plan;
        Save();
        Changed?.Invoke();
    }

    public void UpdateClassPlan(ClassPlan plan)
    {
        if (Profile.ClassPlans.ContainsKey(plan.Id))
        {
            Profile.ClassPlans[plan.Id] = plan;
            Save();
            Changed?.Invoke();
        }
    }

    public void RemoveClassPlan(Guid id)
    {
        if (Profile.ClassPlans.Remove(id))
        {
            Save();
            Changed?.Invoke();
        }
    }

    public void SetActiveClassPlan(Guid id)
    {
        Profile.ActiveClassPlanId = id;
        Save();
        Changed?.Invoke();
    }

    // ---------- Excel 导入 ----------

    /// <summary>
    /// 从解析出的 Lesson 列表重建课表。
    /// 按 weekday 分组，自动创建/复用 Subject 和 TimeLayout。
    /// </summary>
    public void ImportFromLessons(List<Lesson> lessons)
    {
        if (lessons.Count == 0) return;

        // 1. 按课程名去重创建 Subject
        var subjectByName = new Dictionary<string, Subject>(StringComparer.Ordinal);
        foreach (var l in lessons)
        {
            var name = l.Subject.Name.Trim();
            if (!subjectByName.ContainsKey(name))
            {
                var sub = new Subject { Name = name, TeacherName = l.Subject.TeacherName };
                subjectByName[name] = sub;
                Profile.Subjects[sub.Id] = sub;
            }
        }

        // 2. 按时间段去重创建 TimeLayout（所有天共用同一个）
        var uniquePoints = lessons
            .Select(l => (l.Start, l.End))
            .Distinct()
            .OrderBy(t => t.Start)
            .ToList();

        var timeLayout = new TimeLayout { Name = "导入时间表" };
        foreach (var (s, e) in uniquePoints)
            timeLayout.Items.Add(new TimeLayoutItem { StartTime = s, EndTime = e, TimeType = 0 });
        var layoutId = Guid.NewGuid();
        Profile.TimeLayouts[layoutId] = timeLayout;

        // 3. 按 weekday 分组，每天一个 ClassPlan
        var byWeekday = lessons.GroupBy(l => l.WeekDay).ToList();
        // 清除旧课表
        var oldPlanIds = Profile.ClassPlans.Keys.ToList();
        foreach (var id in oldPlanIds)
            Profile.ClassPlans.Remove(id);

        foreach (var group in byWeekday)
        {
            var plan = new ClassPlan
            {
                Name = $"星期{WeekDayToLabel(group.Key)}课表",
                TimeLayoutId = layoutId,
                WeekDay = group.Key
            };

            // 按时间段顺序填充 Classes（与 TimeLayout 对齐）
            foreach (var point in uniquePoints)
            {
                var match = group.FirstOrDefault(l => l.Start == point.Start && l.End == point.End);
                if (match != null)
                {
                    var subName = match.Subject.Name.Trim();
                    var subId = subjectByName[subName].Id;
                    plan.Classes.Add(new ClassInfo { SubjectId = subId, IsEnabled = true });
                }
                else
                {
                    plan.Classes.Add(new ClassInfo { IsEmpty = true, IsEnabled = false });
                }
            }

            Profile.ClassPlans[plan.Id] = plan;
        }

        if (Profile.ClassPlans.Count > 0)
            Profile.ActiveClassPlanId = Profile.ClassPlans.First().Key;

        Save();
        Changed?.Invoke();
    }

    private static string WeekDayToLabel(int wd) => wd switch
    {
        0 => "一", 1 => "二", 2 => "三", 3 => "四",
        4 => "五", 5 => "六", 6 => "日", _ => "?"
    };

    // ---------- 兼容旧接口 ----------

    public List<Lesson> GetLessons() => GetTodayLessons(DateTime.Now);

    // ---------- 持久化 ----------

    private void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(Profile, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }
        catch { }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return;
            var json = File.ReadAllText(_filePath);
            var profile = JsonSerializer.Deserialize<Profile>(json);
            if (profile != null)
                Profile = profile;
        }
        catch { }
    }

    private void EnsureDefaults()
    {
        // 确保至少有一个时间表和一个课表
        if (Profile.TimeLayouts.Count == 0)
        {
            var layout = new TimeLayout { Name = "标准时间表" };
            layout.Items.Add(new TimeLayoutItem { StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(8, 45, 0), TimeType = 0 });
            layout.Items.Add(new TimeLayoutItem { StartTime = new TimeSpan(8, 55, 0), EndTime = new TimeSpan(9, 40, 0), TimeType = 0 });
            layout.Items.Add(new TimeLayoutItem { StartTime = new TimeSpan(10, 0, 0), EndTime = new TimeSpan(10, 45, 0), TimeType = 0 });
            layout.Items.Add(new TimeLayoutItem { StartTime = new TimeSpan(14, 0, 0), EndTime = new TimeSpan(14, 45, 0), TimeType = 0 });
            layout.Items.Add(new TimeLayoutItem { StartTime = new TimeSpan(14, 55, 0), EndTime = new TimeSpan(15, 40, 0), TimeType = 0 });
            var layoutId = Guid.NewGuid();
            Profile.TimeLayouts[layoutId] = layout;

            var plan = new ClassPlan { Name = "默认课表", TimeLayoutId = layoutId, WeekDay = 0 };
            for (int i = 0; i < layout.Items.Count; i++)
                plan.Classes.Add(new ClassInfo { Index = i, IsEnabled = true });
            Profile.ClassPlans[plan.Id] = plan;
            Profile.ActiveClassPlanId = plan.Id;
        }
    }

    private static string ResolvePath()
    {
        string[] bases =
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Path.GetTempPath()
        };
        foreach (var b in bases)
        {
            if (string.IsNullOrEmpty(b)) continue;
            try
            {
                var dir = Path.Combine(b, "ClassEasy");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "profile.json");
            }
            catch { }
        }
        return Path.Combine(Path.GetTempPath(), "ClassEasy", "profile.json");
    }
}
