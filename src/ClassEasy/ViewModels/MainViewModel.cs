using System;
using System.Collections.Generic;
using System.Linq;
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
            RefreshWeatherAsync().FireAndForget();
        }

        /// <summary>外部修改课表后调用：重新从磁盘加载并立即刷新。</summary>
        public void ReloadSchedule()
        {
            var fresh = ConfigService.Load<Schedule>(Paths.ScheduleFile);
            _schedule.Lessons = fresh.Lessons ?? new System.Collections.Generic.List<Lesson>();
            Refresh();
        }

        public string SchoolName => _config.SchoolName;

        private double _windowOpacity;
        /// <summary>主窗口透明度（0.3~1.0），供设置窗口实时预览绑定。</summary>
        public double WindowOpacity
        {
            get => _windowOpacity;
            set => SetProperty(ref _windowOpacity,
                Math.Max(0.3, Math.Min(1.0, value)));
        }

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
            var now = DateTime.Now;
            CurrentTime = now.ToString("HH:mm:ss");
            DateText = now.ToString("M月d日");
            CurrentWeekText = WeekDays[(int)now.DayOfWeek];

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

            // 每 30 分钟刷新一次天气。
            if ((DateTime.Now - _lastWeatherUpdate).TotalMinutes >= 30)
            {
                RefreshWeatherAsync().FireAndForget();
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