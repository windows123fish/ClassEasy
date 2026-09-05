using System;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassEasy.ViewModels;

/// <summary>
/// 课程卡片视图模型：参考 ClassIsland LessonControl 设计。
/// 每个卡片显示一节课的状态（已结束/当前/课间/未来）。
/// </summary>
public partial class LessonCardViewModel : ObservableObject
{
    [ObservableProperty] private string _subjectName = "";
    [ObservableProperty] private string _subjectInitial = "";
    [ObservableProperty] private IBrush _subjectColor = new SolidColorBrush(Color.Parse("#FF4A90E2"));
    [ObservableProperty] private string _timeInfo = "";
    [ObservableProperty] private bool _isCurrent;
    [ObservableProperty] private bool _isEnded;
    [ObservableProperty] private bool _isBreaking;
    [ObservableProperty] private double _progressPercent;

    /// <summary>卡片透明度（根据状态）</summary>
    public double CardOpacity => IsCurrent ? 1.0 : IsEnded ? 0.35 : IsBreaking ? 0.5 : 0.75;

    /// <summary>进度条宽度（绑定到 Border.Width）</summary>
    public double ProgressWidth => 40 * (ProgressPercent / 100.0);
}
