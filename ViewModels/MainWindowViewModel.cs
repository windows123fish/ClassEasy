using System;
using System.Globalization;
using System.Timers;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Timer = System.Timers.Timer;

namespace ClassEasy.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly Timer _timer;
    private readonly CultureInfo _culture = CultureInfo.GetCultureInfo("zh-CN");

    [ObservableProperty] private string _currentTime = string.Empty;
    [ObservableProperty] private string _currentDate = string.Empty;
    [ObservableProperty] private string _statusText = "今天没有课程";
    [ObservableProperty] private string _weatherTemp = "26°";
    [ObservableProperty] private string _weatherDesc = "小雨";

    public MainWindowViewModel()
    {
        UpdateTime();

        _timer = new Timer(1000)
        {
            AutoReset = true
        };
        _timer.Elapsed += OnTimerElapsed;
        _timer.Start();
    }

    private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        // 跨线程更新 UI：切回 UI 线程
        Dispatcher.UIThread.InvokeAsync(UpdateTime);
    }

    private void UpdateTime()
    {
        var now = DateTime.Now;
        CurrentTime = now.ToString("HH:mm", _culture);
        CurrentDate = now.ToString("M月d日 ddd", _culture);
    }
}
