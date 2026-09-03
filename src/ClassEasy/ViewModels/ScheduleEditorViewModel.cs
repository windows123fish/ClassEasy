using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using ClassEasy.Models;
using ClassEasy.Services;

namespace ClassEasy.ViewModels
{
    /// <summary>
    /// 课表编辑器视图模型：负责加载课程、增删改、排序、落盘。
    /// 严格绑定真实的 Schedule 实例，避免"UI改的是新对象、业务用的是旧对象"。
    /// </summary>
    public class ScheduleEditorViewModel : ViewModelBase
    {
        private Lesson _selectedLesson;
        private readonly Schedule _schedule;

        public ScheduleEditorViewModel()
        {
            // 加载用户真实的课表（不是新建），保存后直接写回同一位置。
            _schedule = ConfigService.Load<Schedule>(Paths.ScheduleFile);

            // 如果首次运行没数据，给一个空列表，不要加载示例（用户来编辑就是为了自己填）。
            Lessons = new ObservableCollection<Lesson>(_schedule.Lessons ?? Enumerable.Empty<Lesson>());

            AddLessonCommand = new RelayCommand(AddLesson);
            RemoveLessonCommand = new RelayCommand(RemoveLesson, _ => SelectedLesson != null);
            MoveUpCommand = new RelayCommand(MoveUp, _ => CanMoveUp);
            MoveDownCommand = new RelayCommand(MoveDown, _ => CanMoveDown);
            SaveCommand = new RelayCommand(Save);
        }

        /// <summary>保存成功后通知外部（主窗口刷新）。</summary>
        public event EventHandler Saved;

        public ObservableCollection<Lesson> Lessons { get; }

        public Lesson SelectedLesson
        {
            get => _selectedLesson;
            set
            {
                if (SetProperty(ref _selectedLesson, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public bool CanMoveUp =>
            SelectedLesson != null && Lessons.IndexOf(SelectedLesson) > 0;

        public bool CanMoveDown =>
            SelectedLesson != null &&
            Lessons.IndexOf(SelectedLesson) < Lessons.Count - 1;

        public ICommand AddLessonCommand { get; }
        public ICommand RemoveLessonCommand { get; }
        public ICommand MoveUpCommand { get; }
        public ICommand MoveDownCommand { get; }
        public ICommand SaveCommand { get; }

        private void AddLesson(object obj)
        {
            var previous = Lessons.LastOrDefault();
            var start = "08:00";
            var end = "08:45";
            if (previous != null && TimeSpan.TryParse(previous.EndTime, out var prevEnd))
            {
                var s = prevEnd.Add(TimeSpan.FromMinutes(10)); // 课间 10 分钟
                var e = s.Add(TimeSpan.FromMinutes(45));
                start = string.Format("{0:D2}:{1:D2}", s.Hours, s.Minutes);
                end = string.Format("{0:D2}:{1:D2}", e.Hours, e.Minutes);
            }

            var lesson = new Lesson { StartTime = start, EndTime = end };
            Lessons.Add(lesson);
            SelectedLesson = lesson;
        }

        private void RemoveLesson(object obj)
        {
            if (SelectedLesson == null)
            {
                return;
            }

            var idx = Lessons.IndexOf(SelectedLesson);
            Lessons.Remove(SelectedLesson);

            if (Lessons.Count > 0)
            {
                SelectedLesson = idx < Lessons.Count ? Lessons[idx] : Lessons[Lessons.Count - 1];
            }
            else
            {
                SelectedLesson = null;
            }
        }

        private void MoveUp(object obj)
        {
            if (!CanMoveUp) return;
            var idx = Lessons.IndexOf(SelectedLesson);
            Lessons.Move(idx, idx - 1);
        }

        private void MoveDown(object obj)
        {
            if (!CanMoveDown) return;
            var idx = Lessons.IndexOf(SelectedLesson);
            Lessons.Move(idx, idx + 1);
        }

        private void Save(object obj)
        {
            try
            {
                // 按经验 801350：保存必须真落盘，且写入应用实际使用的配置位置。
                _schedule.Lessons = Lessons.ToList();
                ConfigService.Save(Paths.ScheduleFile, _schedule);

                MessageBox.Show("课表已保存。", "ClassEasy",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                Saved?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                LogService.Error("保存课表失败", ex);
                MessageBox.Show("保存课表失败：" + ex.Message, "ClassEasy",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    /// <summary>简单的 ICommand 实现，支持无参委托。</summary>
    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Func<object, bool> _canExecute;

        public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter) => _canExecute?.Invoke(parameter) ?? true;

        public void Execute(object parameter) => _execute?.Invoke(parameter);

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
}