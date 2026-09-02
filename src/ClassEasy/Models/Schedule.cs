using System.Collections.Generic;

namespace ClassEasy.Models
{
    /// <summary>
    /// 课表：某个时间表下所有课程的集合。
    /// 当前版本先以“单日课表”为主，未来可扩展为多日课表。
    /// </summary>
    public class Schedule
    {
        public List<Lesson> Lessons { get; set; } = new List<Lesson>();
    }
}