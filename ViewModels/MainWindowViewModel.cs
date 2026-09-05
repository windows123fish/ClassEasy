using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Timers;
using Avalonia.Media;
using Avalonia.Threading;
using ClassEasy.Models;
using ClassEasy.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Timer = System.Timers.Timer;

namespace ClassEasy.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly Timer _timer;
    private readonly CultureInfo _culture = CultureInfo.GetCultureInfo("zh-CN");
    private bool _manualStatus;

    [ObservableProperty] private string _currentTime = string.Empty;
    [ObservableProperty] private string _currentDate = string.Empty;
    [ObservableProperty] private string _weatherTemp = "26°";
    [ObservableProperty] private string _weatherDesc = "小雨";
    [ObservableProperty] private string _middleText = "今天没有课程";

    /// <summary>今天课程流卡片列表（参考 ClassIsland LessonControl 横向排列）</summary>
    [ObservableProperty] private ObservableCollection<LessonCardViewModel> _lessonFlow = new();

    /// <summary>今天是否有课程</summary>
    [ObservableProperty] private bool _hasLessonsToday;

    /// <summary>是否显示分隔线</summary>
    [ObservableProperty] private bool _showDivider;

    public MainWindowViewModel()
    {
        UpdateTime();
        UpdateLessonFlow(DateTime.Now);

        ScheduleService.Instance.Changed += OnScheduleChanged;

        _timer = new Timer(1000) { AutoReset = true };
        _timer.Elapsed += OnTimerElapsed;
        _timer.Start();
    }

    private void OnScheduleChanged()
        => Dispatcher.UIThread.InvokeAsync(() =>
        {
            UpdateSchedule(DateTime.Now);
            UpdateLessonFlow(DateTime.Now);
        });

    public void Dispose()
    {
        ScheduleService.Instance.Changed -= OnScheduleChanged;
        _timer?.Stop();
        _timer?.Dispose();
    }

    private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            UpdateTime();
            UpdateSchedule(DateTime.Now);
            UpdateLessonFlow(DateTime.Now);
        });
    }

    private void UpdateTime()
    {
        var now = DateTime.Now;
        CurrentTime = now.ToString("HH:mm", _culture);
        CurrentDate = now.ToString("M月d日 ddd", _culture);
    }

    /// <summary>
    /// 更新课程流：参考 ClassIsland 主界面课程流设计。
    /// - 当前课程：显示完整名称 + 颜色条 + 进度 + 时间
    /// - 已结束：只显示首字母，淡化
    /// - 未开始：只显示首字母，正常亮度
    /// - 课间：显示"休"
    /// </summary>
    private void UpdateLessonFlow(DateTime now)
    {
        var lessons = ScheduleService.Instance.GetTodayLessons(now);
        var flow = new ObservableCollection<LessonCardViewModel>();

        if (lessons.Count == 0)
        {
            HasLessonsToday = false;
            ShowDivider = false;
            LessonFlow = flow;
            return;
        }

        HasLessonsToday = true;
        ShowDivider = true;

        var currentTime = now.TimeOfDay;
        TimeLayoutItem? prevItem = null;

        for (int i = 0; i < lessons.Count; i++)
        {
            var lesson = lessons[i];
            var isCurrent = currentTime >= lesson.Start && currentTime <= lesson.End;
            var isEnded = currentTime > lesson.End;
            var isFuture = currentTime < lesson.Start;

            // 课间休息检测（当前时间在上节课结束和下节课开始之间）
            if (prevItem != null && currentTime > prevItem.EndTime && currentTime < lesson.Start && !isCurrent)
            {
                flow.Add(new LessonCardViewModel
                {
                    SubjectName = "课间休息",
                    SubjectInitial = "休",
                    SubjectColor = new SolidColorBrush(Color.Parse("#FF888888")),
                    IsBreaking = true,
                    IsCurrent = false,
                    IsEnded = false
                });
            }

            var colorHex = lesson.Subject.Color;
            if (string.IsNullOrEmpty(colorHex)) colorHex = "#FF4A90E2";
            var color = Color.Parse(colorHex);

            var vm = new LessonCardViewModel
            {
                SubjectName = lesson.Subject.Name,
                SubjectInitial = !string.IsNullOrEmpty(lesson.Subject.Initial)
                    ? lesson.Subject.Initial
                    : lesson.Subject.Name.Length > 0
                        ? lesson.Subject.Name[..1]
                        : "?",
                SubjectColor = new SolidColorBrush(color),
                TimeInfo = $"{lesson.Start:hh\\:mm}-{lesson.End:hh\\:mm}",
                IsCurrent = isCurrent,
                IsEnded = isEnded,
                IsBreaking = false
            };

            if (isCurrent)
            {
                var elapsed = currentTime - lesson.Start;
                var total = lesson.End - lesson.Start;
                vm.ProgressPercent = total.TotalSeconds > 0
                    ? Math.Min(100, elapsed.TotalSeconds / total.TotalSeconds * 100)
                    : 0;
                vm.TimeInfo = $"剩余 {(lesson.End - currentTime):mm\\:ss}";
            }

            flow.Add(vm);
            prevItem = new TimeLayoutItem { StartTime = lesson.Start, EndTime = lesson.End };
        }

        LessonFlow = flow;
    }

    /// <summary>根据当前时间更新中间状态文本（非手动覆盖时）</summary>
    private void UpdateSchedule(DateTime now)
    {
        if (_manualStatus)
            return;

        if (!HasLessonsToday)
        {
            MiddleText = "今天没有课程";
            return;
        }

        var running = ScheduleService.Instance.FindRunning(now);
        if (running is not null)
        {
            var remain = running.End - now.TimeOfDay;
            if (remain < TimeSpan.Zero) remain = TimeSpan.Zero;
            MiddleText = $"正在上 {running.Subject.Name} · 剩余 {remain:mm\\:ss}";
            return;
        }

        var next = ScheduleService.Instance.FindNext(now);
        if (next is not null)
        {
            MiddleText = $"下一节：{next.Subject.Name} {FormatTime(next.Start)}";
            return;
        }

        MiddleText = "今天没有课程";
    }

    public string ImportSchedule(string xlsxPath)
    {
        var grid = XlsxReader.ReadSheet(xlsxPath, out var err);
        if (grid is null) return err;

        var (lessons, msg) = ScheduleParser.Parse(grid);
        if (lessons.Count == 0) return msg;

        ScheduleService.Instance.ImportFromLessons(lessons);
        _manualStatus = false;
        UpdateSchedule(DateTime.Now);
        UpdateLessonFlow(DateTime.Now);
        return msg;
    }

    public void SetManualStatus(string text)
    {
        _manualStatus = true;
        MiddleText = text;
    }

    private static string FormatTime(TimeSpan t) => t.ToString(@"h\:mm");
}
