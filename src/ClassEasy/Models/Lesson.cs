namespace ClassEasy.Models
{
    /// <summary>
    /// 单节课程（一节课）。
    /// </summary>
    public class Lesson
    {
        /// <summary>课程名称。</summary>
        public string Name { get; set; } = "";

        /// <summary>授课教师。</summary>
        public string Teacher { get; set; } = "";

        /// <summary>上课地点（教室）。</summary>
        public string Location { get; set; } = "";

        /// <summary>开始时间，格式 HH:mm。</summary>
        public string StartTime { get; set; } = "08:00";

        /// <summary>结束时间，格式 HH:mm。</summary>
        public string EndTime { get; set; } = "08:45";
    }
}