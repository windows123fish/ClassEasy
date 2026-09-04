using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using ClassEasy.Models;
using ClassEasy.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClassEasy.ViewModels
{
    /// <summary>
    /// 主窗口视图模型：
    ///   - 顶栏组件化胶囊（时间 / 当前课 / 下一节 / 天气），支持拖拽排序
    ///   - 日/周课表视图切换，当前课高亮、已过课淡化
    ///   - 下课/上课倒计时与提醒 Toast
    ///   - NTP 授时 + 定时天气刷新
    /// </summary>
    public partial class MainViewModel : ViewModelBase
    {
        private static readonly string[] WeekDayNames =
            { "周一", "周二", "周三", "周四", "周五", "周六", "周日" };

        private AppConfig _config;
        private readonly Schedule _schedule;
        private DispatcherTimer? _timer;
        private DateTime _lastWeatherUpdate = DateTime.MinValue;
        private DateTime _lastNtpSync = DateTime.MinValue;
        private int _lastDayKey = -1;
        private string? _lastStartToastKey;
        private string? _lastEndToastKey;
        private CancellationTokenSource? _toastCts;

        // 胶囊实例（BuildCapsules 时按配置顺序放入 Capsules）
        private readonly ClockCapsuleViewModel _clock = new();
        private readonly CurrentClassCapsuleViewModel _current = new();
        private readonly NextClassCapsuleViewModel _next = new();
        private readonly WeatherCapsuleViewModel _weather = new();

        /// <summary>配置被外部（设置窗口）修改后触发；View 据此刷新亚克力/置顶等。</summary>
        public event EventHandler? ConfigChanged;

        /// <summary>鼠标穿透状态变更时触发：MainWindow 调用 Win32 切换窗口样式。</summary>
        public event EventHandler<bool>? ClickThroughChanged;

        public MainViewModel()
        {
            _config = ConfigService.Load<AppConfig>(Paths.ConfigFile);
            _schedule = ConfigService.Load<Schedule>(Paths.ScheduleFile);

            // 首次运行没有数据时，填充示例周课表便于预览。
            if (_schedule.Lessons.Count == 0)
            {
                LoadSampleLessons();
                ConfigService.Save(Paths.ScheduleFile, _schedule);
            }

            IsClickThrough = _config.ClickThroughMode;

            BuildCapsules();

            Tick();
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (s, e) => Tick();
            _timer.Start();

            // 启动即同步授时与天气（异步、失败自动降级，不阻塞 UI）
            _lastNtpSync = DateTime.Now;
            SyncNtpAsync().FireAndForget();
            RefreshWeatherAsync().FireAndForget();
        }

        // ------------------------------------------------------------------
        // 顶栏胶囊
        // ------------------------------------------------------------------

        /// <summary>顶栏组件集合（顺序 = 显示顺序，可拖拽调整）。</summary>
        public ObservableCollection<CapsuleViewModel> Capsules { get; } = new();

        /// <summary>当前是否处于"鼠标穿透"模式。</summary>
        public bool IsClickThrough { get; private set; }

        /// <summary>当前生效配置（设置窗口读写）。</summary>
        public AppConfig Config => _config;

        private void BuildCapsules()
        {
            Capsules.Clear();
            foreach (var kind in _config.CapsuleOrder)
            {
                switch (kind)
                {
                    case "clock": if (!Capsules.Contains(_clock)) Capsules.Add(_clock); break;
                    case "current": if (!Capsules.Contains(_current)) Capsules.Add(_current); break;
                    case "next": if (!Capsules.Contains(_next)) Capsules.Add(_next); break;
                    case "weather": if (_config.ShowWeather && !Capsules.Contains(_weather)) Capsules.Add(_weather); break;
                }
            }

            // 兜底：确保核心胶囊都在
            if (!Capsules.Contains(_clock)) Capsules.Add(_clock);
            if (!Capsules.Contains(_current)) Capsules.Add(_current);
            if (!Capsules.Contains(_next)) Capsules.Add(_next);
            if (_config.ShowWeather && !Capsules.Contains(_weather)) Capsules.Add(_weather);

            UpdateSeparators();
        }

        /// <summary>第一个组件不显示左侧竖分隔线，其余显示（ClassIsland 组件分隔样式）。</summary>
        private void UpdateSeparators()
        {
            for (int i = 0; i < Capsules.Count; i++)
            {
                Capsules[i].ShowSeparator = i > 0;
            }
        }

        /// <summary>拖拽排序：把 drag 组件移动到 target 组件之前/之后，并持久化。</summary>
        public void MoveCapsuleRelative(string dragKind, string targetKind, bool before)
        {
            var drag = Capsules.FirstOrDefault(c => c.Kind == dragKind);
            var target = Capsules.FirstOrDefault(c => c.Kind == targetKind);
            if (drag == null || target == null || ReferenceEquals(drag, target))
            {
                return;
            }

            int removeIndex = Capsules.IndexOf(drag);
            int targetIndex = Capsules.IndexOf(target);
            if (removeIndex < targetIndex)
            {
                targetIndex--;
            }
            if (!before)
            {
                targetIndex++;
            }

            targetIndex = Math.Max(0, Math.Min(Capsules.Count - 1, targetIndex));
            Capsules.Move(removeIndex, targetIndex);
            UpdateSeparators();

            _config.CapsuleOrder = Capsules.Select(c => c.Kind).ToList();
            ConfigService.Save(Paths.ConfigFile, _config);
        }

        // ------------------------------------------------------------------
        // 展开面板：日/周视图
        // ------------------------------------------------------------------

        [ObservableProperty]
        private bool _isExpanded;

        [ObservableProperty]
        private bool _isDayView = true;

        [ObservableProperty]
        private bool _isDayEmpty;

        /// <summary>今天课程卡片（按开始时间排序）。</summary>
        public ObservableCollection<LessonCardViewModel> TodayLessons { get; } = new();

        /// <summary>本周 7 天的列。</summary>
        public ObservableCollection<DayColumnViewModel> WeekDays { get; } = new();

        [RelayCommand]
        private void ToggleExpand() => IsExpanded = !IsExpanded;

        [RelayCommand]
        private void ShowDay() => IsDayView = true;

        [RelayCommand]
        private void ShowWeek() => IsDayView = false;

        // ------------------------------------------------------------------
        // 提醒 Toast
        // ------------------------------------------------------------------

        [ObservableProperty]
        private string _toastText = "";

        [ObservableProperty]
        private bool _toastVisible;

        private void ShowToast(string text)
        {
            ToastText = text;
            ToastVisible = true;

            _toastCts?.Cancel();
            _toastCts?.Dispose();
            var cts = new CancellationTokenSource();
            _toastCts = cts;
            var token = cts.Token;
            Task.Delay(4000, token).ContinueWith(t =>
            {
                if (!t.IsCanceled)
                {
                    Dispatcher.UIThread.Post(() => ToastVisible = false);
                }
            });
        }

        // ------------------------------------------------------------------
        // 每秒刷新
        // ------------------------------------------------------------------

        /// <summary>窗口关闭时停止定时器。</summary>
        public void Stop()
        {
            _timer?.Stop();
            _timer = null;
        }

        /// <summary>每秒刷新时间/课程状态/倒计时，并按间隔刷新天气与授时。</summary>
        public void Tick()
        {
            var now = NtpTimeService.GetNow();

            _clock.TimeText = now.ToString("HH:mm:ss");
            _clock.HourText = now.ToString("HH");
            _clock.MinuteText = now.ToString("mm");
            _clock.DateText = now.ToString("M月d日");
            _clock.WeekText = "周" + "日一二三四五六"[(int)now.DayOfWeek];

            var holiday = _config.ShowHoliday ? HolidayService.GetHolidayName(now.Date) : "";
            _clock.HolidayText = string.IsNullOrEmpty(holiday) ? "" : "· " + holiday;
            _clock.NtpWarning = NtpTimeService.ClockWarningText;

            // 冒号闪烁：默认关闭（常显）；开启后按配置间隔淡出/恢复（透明度过渡，占位不变）
            if (_config.ClockColonBlink)
            {
                int interval = Math.Max(1, Math.Min(60, _config.ClockColonBlinkSeconds));
                long secOfDay = (long)(now - now.Date).TotalSeconds;
                _clock.ColonBlinking = secOfDay / interval % 2 == 1;
            }
            else
            {
                _clock.ColonBlinking = false;
            }

            int dayKey = now.Year * 10000 + now.Month * 100 + now.Day;
            if (dayKey != _lastDayKey)
            {
                _lastDayKey = dayKey;
                RebuildDayCollections(now);
            }

            UpdateLessonStates(now);

            // 定时刷新天气与授时（防休眠/时钟跳跃）
            int ntpInterval = Math.Max(5, Math.Min(240, _config.NtpSyncIntervalMinutes));
            if ((DateTime.Now - _lastWeatherUpdate).TotalMinutes >= 30)
            {
                RefreshWeatherAsync().FireAndForget();
            }
            if ((DateTime.Now - _lastNtpSync).TotalMinutes >= ntpInterval)
            {
                _lastNtpSync = DateTime.Now;
                SyncNtpAsync().FireAndForget();
            }
        }

        private void RebuildDayCollections(DateTime now)
        {
            int todayIndex = ((int)now.DayOfWeek + 6) % 7; // 0=周一

            TodayLessons.Clear();
            foreach (var lesson in _schedule.Lessons
                         .Where(l => l.DayIndex == todayIndex)
                         .OrderBy(l => ParseTime(l.StartTime)))
            {
                TodayLessons.Add(new LessonCardViewModel(lesson));
            }

            WeekDays.Clear();
            for (int i = 0; i < 7; i++)
            {
                var date = now.Date.AddDays(i - todayIndex);
                var col = new DayColumnViewModel(i, WeekDayNames[i], date, i == todayIndex);
                foreach (var lesson in _schedule.Lessons
                             .Where(l => l.DayIndex == i)
                             .OrderBy(l => ParseTime(l.StartTime)))
                {
                    col.Lessons.Add(new LessonCardViewModel(lesson));
                }
                WeekDays.Add(col);
            }
        }

        private void UpdateLessonStates(DateTime now)
        {
            var t = now.TimeOfDay;
            LessonCardViewModel? currentCard = null;

            for (int i = 0; i < TodayLessons.Count; i++)
            {
                var card = TodayLessons[i];
                var start = ParseTime(card.Source.StartTime);
                var end = ParseTime(card.Source.EndTime);
                bool isCurrent = t >= start && t < end;
                card.IsCurrent = isCurrent;
                card.IsPast = t >= end;
                card.IsNext = false;
                card.IsFirst = i == 0;
                card.Progress = isCurrent && end > start
                    ? Math.Min(1.0, Math.Max(0.0, (t - start) / (end - start)))
                    : 0;
                if (isCurrent)
                {
                    currentCard = card;
                }
            }

            // 下一节：当前课后的第一张；无当前课时为开始时间在未来的第一张
            LessonCardViewModel? nextCard = null;
            if (currentCard != null)
            {
                int idx = TodayLessons.IndexOf(currentCard);
                if (idx >= 0 && idx + 1 < TodayLessons.Count)
                {
                    nextCard = TodayLessons[idx + 1];
                }
            }
            else
            {
                nextCard = TodayLessons.FirstOrDefault(c => ParseTime(c.Source.StartTime) > t);
            }

            // 当前课胶囊
            if (currentCard != null)
            {
                var src = currentCard.Source;
                var end = ParseTime(src.EndTime);
                var remain = end - t;

                _current.HasClass = true;
                _current.ClassName = src.Name;
                _current.DetailText = JoinNonEmpty(" · ",
                    src.Teacher, src.Location, src.StartTime + "–" + src.EndTime);
                _current.Progress = currentCard.Progress;
                _current.CountdownText = remain.TotalMinutes < 1
                    ? "即将下课"
                    : remain.ToString(@"mm\:ss") + " 后下课";

                // 顶栏课程流：当前课倒计时（"-15:30" 样式）
                currentCard.CurrentCountdown = "-" + remain.ToString(@"mm\:ss");

                // 提醒：上课 & 临近下课
                string startKey = "start:" + src.Name + "@" + src.StartTime;
                if (_lastStartToastKey != startKey)
                {
                    _lastStartToastKey = startKey;
                    ShowToast("开始上课：" + src.Name +
                              (string.IsNullOrEmpty(src.Location) ? "" : "（" + src.Location + "）"));
                }
                string endKey = "end:" + src.Name + "@" + src.StartTime;
                if (remain.TotalMinutes <= 2 && remain.TotalMinutes > 0 && _lastEndToastKey != endKey)
                {
                    _lastEndToastKey = endKey;
                    ShowToast("距下课不足 2 分钟，请做好准备");
                }
            }
            else
            {
                _current.HasClass = false;
            }

            // 无当前课时清掉课程流中的倒计时残留
            foreach (var card in TodayLessons.Where(c => !c.IsCurrent))
            {
                card.CurrentCountdown = "";
            }

            // 下一节课胶囊
            _next.HasNext = nextCard != null;
            if (nextCard != null)
            {
                var src = nextCard.Source;
                _next.NameText = src.Name;
                _next.InitialText = string.IsNullOrEmpty(src.Name) ? "?" : src.Name.Substring(0, 1);
                _next.TimeText = src.StartTime;
                var mins = (ParseTime(src.StartTime) - t).TotalMinutes;
                _next.DetailText = JoinNonEmpty(" · ", src.Location,
                    mins > 0 ? (int)Math.Ceiling(mins) + " 分钟后上课" : "即将上课");
            }

            IsDayEmpty = TodayLessons.Count == 0;
        }

        private static string JoinNonEmpty(string sep, params string[] parts)
        {
            var list = parts.Where(p => !string.IsNullOrWhiteSpace(p));
            return string.Join(sep, list);
        }

        /// <summary>首次运行时填充示例周课表（周一~周五），便于预览与调试。</summary>
        private void LoadSampleLessons()
        {
            void Add(int day, string name, string start, string end, string teacher, string location)
            {
                _schedule.Lessons.Add(new Lesson
                {
                    DayIndex = day, Name = name, StartTime = start, EndTime = end,
                    Teacher = teacher, Location = location
                });
            }

            // 周一
            Add(0, "语文", "08:00", "08:45", "张老师", "A101");
            Add(0, "数学", "08:55", "09:40", "李老师", "A102");
            Add(0, "英语", "10:00", "10:45", "王老师", "A103");
            Add(0, "物理", "10:55", "11:40", "赵老师", "B201");
            Add(0, "化学", "14:00", "14:45", "孙老师", "B202");
            Add(0, "生物", "14:55", "15:40", "周老师", "B203");

            // 周二
            Add(1, "数学", "08:00", "08:45", "李老师", "A102");
            Add(1, "语文", "08:55", "09:40", "张老师", "A101");
            Add(1, "化学", "10:00", "10:45", "孙老师", "B202");
            Add(1, "英语", "10:55", "11:40", "王老师", "A103");
            Add(1, "物理", "14:00", "14:45", "赵老师", "B201");
            Add(1, "体育", "14:55", "15:40", "刘老师", "操场");

            // 周三
            Add(2, "英语", "08:00", "08:45", "王老师", "A103");
            Add(2, "物理", "08:55", "09:40", "赵老师", "B201");
            Add(2, "数学", "10:00", "10:45", "李老师", "A102");
            Add(2, "语文", "10:55", "11:40", "张老师", "A101");
            Add(2, "生物", "14:00", "14:45", "周老师", "B203");
            Add(2, "历史", "14:55", "15:40", "吴老师", "C301");

            // 周四
            Add(3, "化学", "08:00", "08:45", "孙老师", "B202");
            Add(3, "生物", "08:55", "09:40", "周老师", "B203");
            Add(3, "物理", "10:00", "10:45", "赵老师", "B201");
            Add(3, "数学", "10:55", "11:40", "李老师", "A102");
            Add(3, "英语", "14:00", "14:45", "王老师", "A103");
            Add(3, "美术", "14:55", "15:40", "郑老师", "D401");

            // 周五
            Add(4, "语文", "08:00", "08:45", "张老师", "A101");
            Add(4, "英语", "08:55", "09:40", "王老师", "A103");
            Add(4, "数学", "10:00", "10:45", "李老师", "A102");
            Add(4, "历史", "10:55", "11:40", "吴老师", "C301");
            Add(4, "班会", "14:00", "14:45", "张老师", "A101");
        }

        private static TimeSpan ParseTime(string time)
        {
            return TimeSpan.TryParse(time, out var result) ? result : TimeSpan.Zero;
        }

        // ------------------------------------------------------------------
        // 授时与天气
        // ------------------------------------------------------------------

        private async Task SyncNtpAsync()
        {
            await NtpTimeService.SyncOnceAsync().ConfigureAwait(true);
            _clock.NtpWarning = NtpTimeService.ClockWarningText;
        }

        /// <summary>异步获取天气（FireAndForget，内部捕获所有异常）。</summary>
        public async Task RefreshWeatherAsync()
        {
            _lastWeatherUpdate = DateTime.Now;
            try
            {
                var weather = await WeatherService.GetWeatherAsync(_config.City, _config.WeatherSource)
                    .ConfigureAwait(true);
                _weather.Text = weather.Temperature + "° " + weather.Weather;
                _weather.IconGeometry = WeatherIconLibrary.GetIcon(weather.Weather);
                _weather.City = weather.City;
                _weather.HasWeather = true;
            }
            catch (Exception ex)
            {
                LogService.Error("天气获取失败", ex);
                _weather.Text = "天气获取失败";
                _weather.IconGeometry = WeatherIconLibrary.Cloud;
                _weather.HasWeather = true;
            }
        }

        /// <summary>外部（设置窗口）修改课表文件后调用：重新加载并立即刷新。</summary>
        public void ReloadSchedule()
        {
            var fresh = ConfigService.Load<Schedule>(Paths.ScheduleFile);
            _schedule.Lessons = fresh.Lessons ?? new System.Collections.Generic.List<Lesson>();
            _lastDayKey = -1;
            Tick();
        }

        // ------------------------------------------------------------------
        // 穿透与配置联动
        // ------------------------------------------------------------------

        /// <summary>切换鼠标穿透（托盘菜单 / Ctrl+Alt+T）。</summary>
        public void SetClickThrough(bool enabled)
        {
            if (IsClickThrough == enabled)
            {
                return;
            }

            IsClickThrough = enabled;
            _config.ClickThroughMode = enabled;
            ConfigService.Save(Paths.ConfigFile, _config);
            ClickThroughChanged?.Invoke(this, enabled);
        }

        /// <summary>设置窗口保存后调用：应用新配置并通知 View。</summary>
        public void ApplyConfig(AppConfig cfg)
        {
            _config = cfg;
            ConfigService.Save(Paths.ConfigFile, _config);
            BuildCapsules();
            ConfigChanged?.Invoke(this, EventArgs.Empty);
            RefreshWeatherAsync().FireAndForget();
        }
    }
}
