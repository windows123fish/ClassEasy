using System;
using Avalonia.Controls;
using ClassEasy.ViewModels;

namespace ClassEasy.Views
{
    /// <summary>设置窗口：外观 / 数据 / 自启。</summary>
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            Opened += OnOpened;
        }

        private void OnOpened(object? sender, EventArgs e)
        {
            if (DataContext is SettingsViewModel vm)
            {
                vm.CloseRequested += (_, _) => Close();
            }
        }
    }
}
