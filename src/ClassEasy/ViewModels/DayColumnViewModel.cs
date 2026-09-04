using System;
using System.Collections.ObjectModel;
using ClassEasy.Models;

namespace ClassEasy.ViewModels
{
    /// <summary>
    /// 周视图中"一天"的列：星期表头 + 日期 + 当天课程小卡片列表。
    /// </summary>
    public class DayColumnViewModel : ViewModelBase
    {
        public DayColumnViewModel(int dayIndex, string dayName, DateTime date, bool isToday)
        {
            DayIndex = dayIndex;
            DayName = dayName;
            DateText = date.ToString("M/d");
            IsToday = isToday;
        }

        /// <summary>星期索引：0=周一 … 6=周日。</summary>
        public int DayIndex { get; }

        /// <summary>星期名称，如"周一"。</summary>
        public string DayName { get; }

        /// <summary>日期文本，如"9/4"。</summary>
        public string DateText { get; }

        /// <summary>是否为今天（高亮列头）。</summary>
        public bool IsToday { get; }

        /// <summary>当天的课程卡片。</summary>
        public ObservableCollection<LessonCardViewModel> Lessons { get; } = new();
    }
}
