using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ClassEasy.Models
{
    /// <summary>
    /// 单节课程（一节课）。实现 INotifyPropertyChanged，
    /// 供课表编辑器的双向绑定使用。
    /// </summary>
    public class Lesson : INotifyPropertyChanged
    {
        private string _name = "";
        /// <summary>课程名称。</summary>
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        private string _teacher = "";
        /// <summary>授课教师。</summary>
        public string Teacher
        {
            get => _teacher;
            set => SetProperty(ref _teacher, value);
        }

        private string _location = "";
        /// <summary>上课地点（教室）。</summary>
        public string Location
        {
            get => _location;
            set => SetProperty(ref _location, value);
        }

        private string _startTime = "08:00";
        /// <summary>开始时间，格式 HH:mm。</summary>
        public string StartTime
        {
            get => _startTime;
            set => SetProperty(ref _startTime, value);
        }

        private string _endTime = "08:45";
        /// <summary>结束时间，格式 HH:mm。</summary>
        public string EndTime
        {
            get => _endTime;
            set => SetProperty(ref _endTime, value);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        /// <summary>返回课程的显示摘要（便于调试和日志）。</summary>
        public override string ToString()
        {
            return $"{Name} {StartTime}-{EndTime} @{Location}";
        }
    }
}