using System.Linq;
using ClassEasy.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassEasy.ViewModels
{
    /// <summary>
    /// 课程卡片视图模型：包装 Lesson 并维护展示状态
    /// （进行中 / 已结束 / 下一节）与进度，供日/周视图绑定。
    /// </summary>
    public partial class LessonCardViewModel : ObservableObject
    {
        public LessonCardViewModel(Lesson source)
        {
            Source = source;
        }

        /// <summary>原始课程数据。</summary>
        public Lesson Source { get; }

        public string Name => Source.Name;
        public string Teacher => Source.Teacher;
        public string Location => Source.Location;
        public string TimeRange => Source.StartTime + " – " + Source.EndTime;

        /// <summary>科目首字（ClassIsland 迷你课程流中大字显示）。</summary>
        public string Initial => string.IsNullOrEmpty(Source.Name) ? "?" : Source.Name.Substring(0, 1);

        /// <summary>教师/地点拼接（可空）。</summary>
        public string Detail
        {
            get
            {
                var parts = new[] { Source.Teacher, Source.Location };
                var joined = string.Join(" · ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
                return joined;
            }
        }

        /// <summary>课程是否正在进行中（高亮）。</summary>
        [ObservableProperty]
        private bool _isCurrent;

        /// <summary>课程是否已结束（淡化）。</summary>
        [ObservableProperty]
        private bool _isPast;

        /// <summary>是否为下一节课。</summary>
        [ObservableProperty]
        private bool _isNext;

        /// <summary>当前课程进度（0~1）。</summary>
        [ObservableProperty]
        private double _progress;

        /// <summary>当前课剩余时间（"-15:30" 样式，仅进行中的课更新；其他课为空）。</summary>
        [ObservableProperty]
        private string _currentCountdown = "";

        /// <summary>是否为当日第一节课（顶栏课程流首项不显示左侧分隔竖线）。</summary>
        [ObservableProperty]
        private bool _isFirst;
    }
}
