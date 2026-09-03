using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using ClassEasy.Models;
using ClassEasy.Services;

namespace ClassEasy.ViewModels
{
    /// <summary>
    /// 主窗口视图模型：维护当前时间、当前课程与下一节课程。
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        private static readonly string[] WeekDays =
            { "周日", "周一", "周二", "周三", "周四", "周五", "周六" };

        private readonly DispatcherTimer _timer;
        private readonly AppConfig _config;
        private readonly Schedule _schedule;
        private DateTime _lastWeatherUpdate = DateTime.MinValue;
        private DateTime _lastNtpSync = DateTime.MinValue;

        // 悬停去抖：悬停穿透延迟启用的取消令牌与最后状态
        private CancellationTokenSource _hoverDebounceCts;
        private bool _lastHoverDetected;

        public MainViewModel()
        {
            _config = ConfigService.Load<AppConfig>(Paths.ConfigFile);
            _schedule = ConfigService.Load<Schedule>(Paths.ScheduleFile);

            // 首次运行没有数据时，填充示例课表便于预览与开发调试。
            if (_schedule.Lessons.Count == 0)
            {
                LoadSampleLessons();
            }

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (sender, args) => Refresh();
            _timer.Start();

            Refresh();
            WindowOpacity = _config.WindowOpacity;
            _isClickThrough = _config.ClickThroughMode;
            ClockStatusText = NtpTimeService.ClockSourceText;
            // 启动时立即同步一次授时中心（异步、失败自动降级，不阻塞UI）
            SyncNtpAsync().FireAndForget();
            RefreshWeatherAsync().FireAndForget();
        }

        private async Task SyncNtpAsync()
        {
            await NtpTimeService.SyncOnceAsync().ConfigureAwait(true);
            // 回到 UI 线程刷新显示
            ClockStatusText = NtpTimeService.ClockSourceText;
        }

        /// <summary>外部修改课表后调用：重新从磁盘加载并立即刷新。</summary>
        public void ReloadSchedule()
        {
            var fresh = ConfigService.Load<Schedule>(Paths.ScheduleFile);
            _schedule.Lessons = fresh.Lessons ?? new System.Collections.Generic.List<Lesson>();
            Refresh();
        }

        /// <summary>窗口关闭时释放定时器，避免 GC 延迟造成的后台刷新。</summary>
        public void DisposeTimer()
        {
            _timer?.Stop();
        }

        public string SchoolName => _config.SchoolName;

        private string _clockStatusText = "[警告] 当前为本地时间，时间可能不准确！";
        /// <summary>授时状态显示：✓ 国家授时  或  [警告] 当前为本地时间，时间可能不准确！。</summary>
        public string ClockStatusText
        {
            get => _clockStatusText;
            set => SetProperty(ref _clockStatusText, value);
        }

        private bool _hoverClickThrough;
        /// <summary>悬停去抖后最终生效的"瞬时穿透"标志。为 true 时顶栏会穿透点击（不持久化）。</summary>
        public bool HoverClickThrough
        {
            get => _hoverClickThrough;
            private set
            {
                if (SetProperty(ref _hoverClickThrough, value))
                {
                    // 悬停穿透也会改变窗口样式，通知 View 层去调用 Win32。
                    ClickThroughChanged?.Invoke(this, EventArgs.Empty);
                    OnPropertyChanged(nameof(EffectiveOpacity));
                }
            }
        }

        /// <summary>
        /// 由 MainWindow 的鼠标位置轮询循环每 ~100ms 调用一次，报告"鼠标当前是否压在顶栏窗口矩形内"。
        /// 内部做 120ms debounce：
        ///   - hoverDetected=true：延迟 120ms 后仍保持→ 进入 HoverClickThrough 穿透态
        ///   - hoverDetected=false：立即取消穿透（用户鼠标移走时应该立刻不穿透，能点回顶栏自身内容）
        /// </summary>
        public void SetHoverDetected(bool hoverDetected)
        {
            if (_lastHoverDetected == hoverDetected && hoverDetected) return;
            _lastHoverDetected = hoverDetected;

            // 先取消之前的去抖令牌
            _hoverDebounceCts?.Cancel();
            _hoverDebounceCts?.Dispose();
            _hoverDebounceCts = null;

            if (!hoverDetected)
            {
                // 鼠标离开 → 立即关穿透（不用延迟，保证用户能立刻点回顶栏内容）
                HoverClickThrough = false;
                return;
            }

            // 鼠标进入 → 延迟 120ms；期间若又 SetHoverDetected(false)，令牌取消跳过穿透
            var cts = new CancellationTokenSource();
            _hoverDebounceCts = cts;
            Task.Delay(120, cts.Token).ContinueWith(t =>
            {
                if (t.IsCanceled) return;
                // 再次确认：120ms 后鼠标仍然"压在顶栏上"才开穿透
                if (_lastHoverDetected)
                {
                    System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        HoverClickThrough = true;
                    });
                }
            }, TaskContinuationOptions.ExecuteSynchronously);
        }

        private double _windowOpacity;
        /// <summary>主窗口透明度（0.3~1.0），供设置窗口实时预览绑定。</summary>
        public double WindowOpacity
        {
            get => _windowOpacity;
            set
            {
                if (SetProperty(ref _windowOpacity, Math.Max(0.3, Math.Min(1.0, value))))
                {
                    // 配置值变化时，派生的 EffectiveOpacity 也要重算通知。
                    OnPropertyChanged(nameof(EffectiveOpacity));
                }
            }
        }

        private bool _isHovering;
        /// <summary>鼠标是否悬停在窗口上（由 MainWindow MouseEnter/Leave 维护）。</summary>
        public bool IsHovering
        {
            get => _isHovering;
            set
            {
                if (SetProperty(ref _isHovering, value))
                {
                    OnPropertyChanged(nameof(EffectiveOpacity));
                }
            }
        }

        /// <summary>实际生效的窗口不透明度。
        /// 优先级：
        ///   1. 手动穿透（持久化 Ctrl+Alt+T）→ baseOpacity × 0.35（最淡，告知用户"这就是穿透态"）
        ///   2. 悬停即穿透（120ms 去抖）→ baseOpacity × 0.22（非常淡，提示"正在穿透点后面"）
        ///   3. 鼠标悬停在上面但没进入穿透 → 清晰（方便拖动/点内容）
        ///   4. 普通态 → × 0.70
        /// </summary>
        public double EffectiveOpacity
        {
            get
            {
                double baseOpacity = WindowOpacity;
                double target;
                if (IsClickThrough)
                {
                    target = baseOpacity * 0.35;
                }
                else if (HoverClickThrough)
                {
                    target = baseOpacity * 0.22;
                }
                else if (IsHovering)
                {
                    target = baseOpacity;
                }
                else
                {
                    target = baseOpacity * 0.70;
                }
                return Math.Max(0.08, Math.Min(1.0, target));
            }
        }

        private bool _isClickThrough;
        /// <summary>当前是否处于"点击穿透"模式。
        /// 修改此属性会写入持久化配置（下次启动沿用）。</summary>
        public bool IsClickThrough
        {
            get => _isClickThrough;
            set
            {
                if (SetProperty(ref _isClickThrough, value))
                {
                    _config.ClickThroughMode = value;
                    // 穿透模式也会影响透明度显示，需要通知界面刷新
                    OnPropertyChanged(nameof(EffectiveOpacity));
                    ClickThroughChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        /// <summary>点击穿透状态变更时触发：MainWindow 调用 Win32 API 切换窗口样式。</summary>
        public event EventHandler ClickThroughChanged;

        /// <summary>预览透明度（不保存），设置窗口拖动滑块时调用。
        /// 预览值若用户点「取消」则调用 <see cref="RestoreOpacityFromConfig"/> 回滚。</summary>
        public void PreviewOpacity(double value)
        {
            WindowOpacity = value;
        }

        /// <summary>将透明度写回配置对象（在设置保存时调用）。</summary>
        public void CommitOpacityToConfig()
        {
            _config.WindowOpacity = WindowOpacity;
        }

        /// <summary>取消预览，回到配置中保存的值。</summary>
        public void RestoreOpacityFromConfig()
        {
            WindowOpacity = _config.WindowOpacity;
        }

        private string _dateText = "";
        public string DateText
        {
            get => _dateText;
            set => SetProperty(ref _dateText, value);
        }

        private string _currentWeekText = "";
        public string CurrentWeekText
        {
            get => _currentWeekText;
            set => SetProperty(ref _currentWeekText, value);
        }

        private double _lessonProgress;
        public double LessonProgress
        {
            get => _lessonProgress;
            set => SetProperty(ref _lessonProgress, value);
        }

        private string _countdownText = "";
        public string CountdownText
        {
            get => _countdownText;
            set => SetProperty(ref _countdownText, value);
        }

        private string _weatherText = "天气加载中…";
        public string WeatherText
        {
            get => _weatherText;
            set => SetProperty(ref _weatherText, value);
        }

        private string _currentTime;
        public string CurrentTime
        {
            get => _currentTime;
            set => SetProperty(ref _currentTime, value);
        }

        private Lesson _currentLesson;
        public Lesson CurrentLesson
        {
            get => _currentLesson;
            set
            {
                if (SetProperty(ref _currentLesson, value))
                {
                    OnPropertyChanged(nameof(CurrentLessonText));
                    OnPropertyChanged(nameof(CurrentLessonDetail));
                }
            }
        }

        private Lesson _nextLesson;
        public Lesson NextLesson
        {
            get => _nextLesson;
            set
            {
                if (SetProperty(ref _nextLesson, value))
                {
                    OnPropertyChanged(nameof(NextLessonText));
                }
            }
        }

        // 以下为便于 XAML 直接绑定的只读显示属性，避免处理空值与拼接逻辑。

        public string CurrentLessonText => CurrentLesson?.Name ?? "暂无课程";

        public string CurrentLessonDetail
        {
            get
            {
                if (CurrentLesson == null)
                {
                    return "课间休息";
                }

                string detail = "";
                if (!string.IsNullOrEmpty(CurrentLesson.Teacher))
                {
                    detail += CurrentLesson.Teacher + "  ";
                }
                if (!string.IsNullOrEmpty(CurrentLesson.Location))
                {
                    detail += CurrentLesson.Location + "  ";
                }
                detail += CurrentLesson.StartTime + "-" + CurrentLesson.EndTime;
                return detail;
            }
        }

        public string NextLessonText
        {
            get
            {
                if (NextLesson == null)
                {
                    return "今日课程已结束";
                }

                return "下一节：" + NextLesson.Name + "（" + NextLesson.StartTime + "）";
            }
        }

        /// <summary>每秒刷新当前时间，并重新判断当前/下一节课程。</summary>
        private void Refresh()
        {
            // 时间源：优先国家授时中心校准过的值（失败时 GetNow 退回本地时间）
            var now = NtpTimeService.GetNow();
            CurrentTime = now.ToString("HH:mm:ss");
            DateText = now.ToString("M月d日");
            CurrentWeekText = WeekDays[(int)now.DayOfWeek];

            // 授时状态秒级同步（授时成功/失败切换时刷新胶囊颜色与文本）
            var cs = NtpTimeService.ClockSourceText;
            if (ClockStatusText != cs) ClockStatusText = cs;

            var timeOfDay = now.TimeOfDay;
            var lessons = _schedule.Lessons
                .OrderBy(l => ParseTime(l.StartTime))
                .ToList();

            Lesson current = null;
            Lesson next = null;
            bool currentFound = false;

            foreach (var lesson in lessons)
            {
                var start = ParseTime(lesson.StartTime);
                var end = ParseTime(lesson.EndTime);

                if (timeOfDay >= start && timeOfDay < end)
                {
                    current = lesson;
                    currentFound = true;
                    continue;
                }

                if (currentFound)
                {
                    next = lesson;
                    break;
                }
            }

            // 还未开始第一节时，把第一节当作下一节。
            if (current == null && next == null && lessons.Count > 0 && timeOfDay < ParseTime(lessons[0].StartTime))
            {
                next = lessons[0];
            }

            CurrentLesson = current;
            NextLesson = next;
            UpdateLessonProgress(current, timeOfDay);

            // 每 30 分钟（或用户自定义授时同步频率）刷新一次天气与授时中心（防止系统休眠/时钟跳跃）
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

        /// <summary>计算当前课程的进度（0~100）与下课倒计时。</summary>
        private void UpdateLessonProgress(Lesson current, TimeSpan now)
        {
            if (current == null)
            {
                LessonProgress = 0;
                CountdownText = "";
                return;
            }

            var start = ParseTime(current.StartTime);
            var end = ParseTime(current.EndTime);
            var total = (end - start).TotalMinutes;
            var elapsed = (now - start).TotalMinutes;

            LessonProgress = total > 0
                ? Math.Max(0, Math.Min(100, elapsed / total * 100))
                : 0;

            var remaining = end - now;
            if (remaining.TotalMinutes < 0)
            {
                CountdownText = "已下课";
            }
            else if (remaining.TotalMinutes < 1)
            {
                CountdownText = "即将下课";
            }
            else
            {
                CountdownText = string.Format("{0} 分钟后下课",
                    (int)Math.Ceiling(remaining.TotalMinutes));
            }
        }

        /// <summary>异步获取天气，避免阻塞 UI 线程。</summary>
        private async System.Threading.Tasks.Task RefreshWeatherAsync()
        {
            _lastWeatherUpdate = DateTime.Now;

            try
            {
                // 重新读取城市，确保设置修改后能立即生效。
                var config = ConfigService.Load<AppConfig>(Paths.ConfigFile);
                var weather = await WeatherService.GetWeatherAsync(config.City);
                WeatherText = string.Format("{0} {1}℃  {2}",
                    weather.Weather, weather.Temperature, weather.City);
            }
            catch (Exception ex)
            {
                LogService.Error("天气获取失败", ex);
                WeatherText = "天气获取失败";
            }
        }

        /// <summary>外部（设置窗口）可调用此方法立即刷新天气。</summary>
        public void RefreshWeather()
        {
            RefreshWeatherAsync().FireAndForget();
        }

        private static TimeSpan ParseTime(string time)
        {
            return TimeSpan.TryParse(time, out var result) ? result : TimeSpan.Zero;
        }

        private void LoadSampleLessons()
        {
            _schedule.Lessons = new List<Lesson>
            {
                new Lesson { Name = "语文", Teacher = "张老师", Location = "A101", StartTime = "08:00", EndTime = "08:45" },
                new Lesson { Name = "数学", Teacher = "李老师", Location = "A102", StartTime = "08:55", EndTime = "09:40" },
                new Lesson { Name = "英语", Teacher = "王老师", Location = "A103", StartTime = "10:00", EndTime = "10:45" },
                new Lesson { Name = "物理", Teacher = "赵老师", Location = "B201", StartTime = "10:55", EndTime = "11:40" },
                new Lesson { Name = "化学", Teacher = "孙老师", Location = "B202", StartTime = "14:00", EndTime = "14:45" },
                new Lesson { Name = "生物", Teacher = "周老师", Location = "B203", StartTime = "14:55", EndTime = "15:40" }
            };
        }
    }
}