using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using ClassEasy.Models;
using ClassEasy.Services;

namespace ClassEasy.Views;

public partial class ScheduleManagerWindow : Window
{
    private Guid _currentPlanId = Guid.Empty;

    public ScheduleManagerWindow()
    {
        InitializeComponent();
        LoadPlans();
        LoadSubjects();
    }

    private void LoadPlans()
    {
        var plans = ScheduleService.Instance.Profile.ClassPlans
            .Select(kvp => new PlanItem { Id = kvp.Key, Name = kvp.Value.Name })
            .ToList();
        PlanBox.ItemsSource = plans;
        PlanBox.DisplayMemberBinding = new Avalonia.Data.Binding("Name");
        if (plans.Count > 0)
        {
            PlanBox.SelectedIndex = 0;
            _currentPlanId = plans[0].Id;
            LoadPlanData();
        }
    }

    private void LoadSubjects()
    {
        var subjects = ScheduleService.Instance.Profile.Subjects
            .Select(kvp => new SubjectItem { Id = kvp.Key, Name = kvp.Value.Name, TeacherName = kvp.Value.TeacherName })
            .ToList();
        SubjectBox.ItemsSource = subjects;
        SubjectBox.DisplayMemberBinding = new Avalonia.Data.Binding("Name");
    }

    private void LoadLayouts()
    {
        var layouts = ScheduleService.Instance.Profile.TimeLayouts
            .Select(kvp => new LayoutItem { Id = kvp.Key, Name = kvp.Value.Name })
            .ToList();
        LayoutBox.ItemsSource = layouts;
        LayoutBox.DisplayMemberBinding = new Avalonia.Data.Binding("Name");
    }

    private void LoadPlanData()
    {
        LoadLayouts();
        if (_currentPlanId == Guid.Empty) return;
        if (!ScheduleService.Instance.Profile.ClassPlans.TryGetValue(_currentPlanId, out var plan))
            return;

        // 选中当前课表关联的时间表
        if (ScheduleService.Instance.Profile.TimeLayouts.TryGetValue(plan.TimeLayoutId, out _))
        {
            for (int i = 0; i < LayoutBox.ItemCount; i++)
            {
                if (LayoutBox.Items[i] is LayoutItem item && item.Id == plan.TimeLayoutId)
                {
                    LayoutBox.SelectedIndex = i;
                    break;
                }
            }
        }

        // 加载课程列表
        var rows = new List<ClassRow>();
        var layout = ScheduleService.Instance.Profile.TimeLayouts
            .GetValueOrDefault(plan.TimeLayoutId);
        if (layout != null)
        {
            var classItems = layout.ClassItems.ToList();
            for (int i = 0; i < classItems.Count && i < plan.Classes.Count; i++)
            {
                var classInfo = plan.Classes[i];
                var subject = ScheduleService.Instance.Profile.Subjects
                    .GetValueOrDefault(classInfo.SubjectId, Subject.Fallback);
                var item = classItems[i];
                rows.Add(new ClassRow
                {
                    Index = i,
                    IndexDisplay = $"第{i + 1}节",
                    SubjectName = subject.Name,
                    TeacherName = subject.TeacherName,
                    TimeRange = $"{item.StartTime:hh\\:mm} - {item.EndTime:hh\\:mm}",
                    StatusText = classInfo.IsEnabled ? "启用" : "停用",
                    SubjectId = classInfo.SubjectId,
                    StartTime = item.StartTime,
                    EndTime = item.EndTime,
                    IsEnabled = classInfo.IsEnabled
                });
            }
        }
        CourseGrid.ItemsSource = rows;
    }

    private void PlanBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (PlanBox.SelectedItem is PlanItem plan)
        {
            _currentPlanId = plan.Id;
            LoadPlanData();
        }
    }

    private void CourseGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (CourseGrid.SelectedItem is ClassRow row)
        {
            // 选中科目
            for (int i = 0; i < SubjectBox.ItemCount; i++)
            {
                if (SubjectBox.Items[i] is SubjectItem sub && sub.Id == row.SubjectId)
                {
                    SubjectBox.SelectedIndex = i;
                    break;
                }
            }
            StartBox.Text = row.StartTime.ToString(@"hh\:mm");
            EndBox.Text = row.EndTime.ToString(@"hh\:mm");
            FormTip.Text = $"正在编辑：{row.SubjectName}（{row.IndexDisplay}）";
        }
    }

    private void SetActiveBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_currentPlanId != Guid.Empty)
        {
            ScheduleService.Instance.SetActiveClassPlan(_currentPlanId);
            FormTip.Text = "已设为激活课表";
            FormTip.Foreground = Avalonia.Media.Brushes.LightGreen;
        }
    }

    private void NewSubjectBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var subject = new Subject { Name = "新科目", TeacherName = "" };
        ScheduleService.Instance.AddSubject(subject);
        LoadSubjects();
        FormTip.Text = $"已新增科目：{subject.Name}，请在下拉框中选择并编辑";
        FormTip.Foreground = Avalonia.Media.Brushes.LightGreen;
    }

    private void UpdateTimePointBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (CourseGrid.SelectedItem is not ClassRow row)
        {
            FormTip.Text = "请先选中课程行";
            FormTip.Foreground = Avalonia.Media.Brushes.OrangeRed;
            return;
        }
        if (!TimeSpan.TryParse(StartBox.Text?.Trim(), out var start) || !TimeSpan.TryParse(EndBox.Text?.Trim(), out var end))
        {
            FormTip.Text = "时间格式错误（应为 HH:mm）";
            FormTip.Foreground = Avalonia.Media.Brushes.OrangeRed;
            return;
        }
        if (end <= start)
        {
            FormTip.Text = "结束时间必须晚于开始时间";
            FormTip.Foreground = Avalonia.Media.Brushes.OrangeRed;
            return;
        }

        // 更新时间表中的时间点
        if (_currentPlanId != Guid.Empty &&
            ScheduleService.Instance.Profile.ClassPlans.TryGetValue(_currentPlanId, out var plan) &&
            ScheduleService.Instance.Profile.TimeLayouts.TryGetValue(plan.TimeLayoutId, out var layout))
        {
            var classItems = layout.ClassItems.ToList();
            if (row.Index < classItems.Count)
            {
                classItems[row.Index].StartTime = start;
                classItems[row.Index].EndTime = end;
                ScheduleService.Instance.UpdateClassPlan(plan);
                LoadPlanData();
                FormTip.Text = "时间点已更新";
                FormTip.Foreground = Avalonia.Media.Brushes.LightGreen;
            }
        }
    }

    private void SaveBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_currentPlanId != Guid.Empty &&
            ScheduleService.Instance.Profile.ClassPlans.TryGetValue(_currentPlanId, out var plan))
        {
            ScheduleService.Instance.UpdateClassPlan(plan);
            FormTip.Text = "课表已保存";
            FormTip.Foreground = Avalonia.Media.Brushes.LightGreen;
        }
    }

    private void NewPlanBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // 复制第一个时间表作为默认
        var firstLayout = ScheduleService.Instance.Profile.TimeLayouts.FirstOrDefault();
        if (firstLayout.Value is null)
        {
            FormTip.Text = "请先创建时间表";
            FormTip.Foreground = Avalonia.Media.Brushes.OrangeRed;
            return;
        }
        var plan = new ClassPlan
        {
            Name = $"新课表 {ScheduleService.Instance.Profile.ClassPlans.Count + 1}",
            TimeLayoutId = firstLayout.Key,
            WeekDay = 0
        };
        for (int i = 0; i < firstLayout.Value.Items.Count; i++)
            plan.Classes.Add(new ClassInfo { Index = i, IsEnabled = true });
        ScheduleService.Instance.AddClassPlan(plan);
        LoadPlans();
        FormTip.Text = $"已新增课表：{plan.Name}";
        FormTip.Foreground = Avalonia.Media.Brushes.LightGreen;
    }

    private void DeletePlanBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_currentPlanId != Guid.Empty)
        {
            ScheduleService.Instance.RemoveClassPlan(_currentPlanId);
            _currentPlanId = Guid.Empty;
            LoadPlans();
            CourseGrid.ItemsSource = null;
            FormTip.Text = "课表已删除";
            FormTip.Foreground = Avalonia.Media.Brushes.LightGreen;
        }
    }

    private async void ImportBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
#pragma warning disable CS0618
        var dlg = new OpenFileDialog
        {
            Title = "选择课表 Excel 文件",
            AllowMultiple = false,
            Filters = { new FileDialogFilter { Name = "Excel 工作簿 (*.xlsx)", Extensions = { "xlsx" } } }
        };
        var paths = await dlg.ShowAsync(this);
#pragma warning restore CS0618

        if (paths is null || paths.Length == 0) return;

        var grid = XlsxReader.ReadSheet(paths[0], out var err);
        if (grid is null)
        {
            FormTip.Text = "导入失败：" + err;
            FormTip.Foreground = Avalonia.Media.Brushes.OrangeRed;
            return;
        }

        var (lessons, msg) = ScheduleParser.Parse(grid);
        if (lessons.Count == 0)
        {
            FormTip.Text = msg;
            FormTip.Foreground = Avalonia.Media.Brushes.OrangeRed;
            return;
        }

        // TODO: 将 Excel 导入的课程映射到新模型
        FormTip.Text = "Excel 导入功能待适配新数据模型，当前版本暂不支持";
        FormTip.Foreground = Avalonia.Media.Brushes.OrangeRed;
    }

    private void CloseBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }

    private class PlanItem { public Guid Id { get; set; } public string Name { get; set; } = ""; }
    private class LayoutItem { public Guid Id { get; set; } public string Name { get; set; } = ""; }
    private class SubjectItem { public Guid Id { get; set; } public string Name { get; set; } = ""; public string TeacherName { get; set; } = ""; }
    private class ClassRow
    {
        public int Index { get; set; }
        public string IndexDisplay { get; set; } = "";
        public string SubjectName { get; set; } = "";
        public string TeacherName { get; set; } = "";
        public string TimeRange { get; set; } = "";
        public string StatusText { get; set; } = "";
        public Guid SubjectId { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public bool IsEnabled { get; set; }
    }
}
