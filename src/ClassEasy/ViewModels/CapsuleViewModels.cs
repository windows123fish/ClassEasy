using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassEasy.ViewModels
{
    /// <summary>
    /// 顶栏"组件胶囊"基类。顶栏以组件化方式排列（时间 / 当前课 / 下一节 / 天气），
    /// 支持鼠标拖拽调整顺序，顺序持久化到配置。
    /// </summary>
    public abstract partial class CapsuleViewModel : ViewModelBase
    {
        /// <summary>组件标识（持久化用）：clock / current / next / weather。</summary>
        public abstract string Kind { get; }

        /// <summary>组件左侧是否显示竖分隔线（第一个组件不显示）。</summary>
        [ObservableProperty]
        private bool _showSeparator;
    }

    /// <summary>时间胶囊：ClassIsland 式大字时钟（冒号闪烁）+ 日期/星期小字。</summary>
    public partial class ClockCapsuleViewModel : CapsuleViewModel
    {
        public override string Kind => "clock";

        [ObservableProperty]
        private string _timeText = "--:--";

        /// <summary>小时（大字）。</summary>
        [ObservableProperty]
        private string _hourText = "--";

        /// <summary>分钟（大字）。</summary>
        [ObservableProperty]
        private string _minuteText = "--";

        /// <summary>冒号是否处于闪烁淡出状态（设置开启闪烁时才可能为 true；淡出仅调透明度，占位不变不跳动）。</summary>
        [ObservableProperty]
        private bool _colonBlinking;

        [ObservableProperty]
        private string _dateText = "";

        [ObservableProperty]
        private string _weekText = "";

        [ObservableProperty]
        private string _holidayText = "";

        [ObservableProperty]
        private string _ntpWarning = "";

        public bool HasHoliday => !string.IsNullOrEmpty(HolidayText);

        public bool HasNtpWarning => !string.IsNullOrEmpty(NtpWarning);

        partial void OnHolidayTextChanged(string value) => OnPropertyChanged(nameof(HasHoliday));

        partial void OnNtpWarningChanged(string value) => OnPropertyChanged(nameof(HasNtpWarning));
    }

    /// <summary>当前课程胶囊：课程名 + 详情 + 下课倒计时 + 进度。</summary>
    public partial class CurrentClassCapsuleViewModel : CapsuleViewModel
    {
        public override string Kind => "current";

        [ObservableProperty]
        private bool _hasClass;

        [ObservableProperty]
        private string _className = "";

        [ObservableProperty]
        private string _detailText = "";

        [ObservableProperty]
        private string _countdownText = "";

        [ObservableProperty]
        private double _progress;
    }

    /// <summary>下一节课胶囊：课程名 + 上课时间 + 上课倒计时。</summary>
    public partial class NextClassCapsuleViewModel : CapsuleViewModel
    {
        public override string Kind => "next";

        [ObservableProperty]
        private bool _hasNext;

        [ObservableProperty]
        private string _nameText = "";

        /// <summary>科目首字（ClassIsland 迷你课程风格大字）。</summary>
        [ObservableProperty]
        private string _initialText = "";

        [ObservableProperty]
        private string _timeText = "";

        [ObservableProperty]
        private string _detailText = "";

        /// <summary>时间 + 详情组合（一行显示）。</summary>
        public string TimeDetailText => TimeText + "  " + DetailText;

        partial void OnTimeTextChanged(string value) => OnPropertyChanged(nameof(TimeDetailText));

        partial void OnDetailTextChanged(string value) => OnPropertyChanged(nameof(TimeDetailText));
    }

    /// <summary>天气胶囊：MDI 风格图标 + 温度/天气描述。</summary>
    public partial class WeatherCapsuleViewModel : CapsuleViewModel
    {
        public override string Kind => "weather";

        [ObservableProperty]
        private bool _hasWeather;

        [ObservableProperty]
        private string _text = "天气加载中…";

        [ObservableProperty]
        private Geometry _iconGeometry = WeatherIconLibrary.Cloud;

        [ObservableProperty]
        private string _city = "";
    }
}
