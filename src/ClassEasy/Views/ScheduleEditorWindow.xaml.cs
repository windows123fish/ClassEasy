using System;
using System.Windows;
using ClassEasy.ViewModels;

namespace ClassEasy.Views
{
    /// <summary>
    /// 课表编辑窗口：左侧课程列表 + 右侧详情编辑 + 保存。
    /// 非模态打开（Show），保存成功后通过 Saved 事件通知主窗口。
    /// </summary>
    public partial class ScheduleEditorWindow : Window
    {
        public event EventHandler Saved;

        public ScheduleEditorWindow()
        {
            InitializeComponent();

            var vm = new ScheduleEditorViewModel();
            vm.Saved += (sender, args) =>
            {
                Saved?.Invoke(this, EventArgs.Empty);
                Close();
            };
            DataContext = vm;
        }
    }
}