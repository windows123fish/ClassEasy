using System;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassEasy;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _currentTime = "";

    [ObservableProperty]
    private string _currentDate = "";

    [ObservableProperty]
    private string _statusText = "今天没有课程";

    [ObservableProperty]
    private string _temperature = "26°";

    [ObservableProperty]
    private string _weatherDescription = "小雨";

    private readonly DispatcherTimer _timer;

    public MainWindowViewModel()
    {
        UpdateTime();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => UpdateTime();
        _timer.Start();
    }

    private void UpdateTime()
    {
        var now = DateTime.Now;
        CurrentTime = now.ToString("HH:mm");
        CurrentDate = now.ToString("M月d日 dddd");
    }
}
