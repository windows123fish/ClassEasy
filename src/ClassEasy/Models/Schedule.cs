using System.Collections.Generic;

namespace ClassEasy.Models
{
    /// <summary>
    /// 周课表：一周内所有课程（DayIndex 区分星期几）。
    /// </summary>
    public class Schedule
    {
        public List<Lesson> Lessons { get; set; } = new List<Lesson>();
    }
}
