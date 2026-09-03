using System;
using System.Drawing;
using System.Windows.Forms;
using ClassEasy.Models;
using ClassEasy.Services;

namespace ClassEasy.Services
{
    /// <summary>
    /// 系统托盘服务：提供托盘图标与右键菜单（显示/隐藏、设置、退出）。
    /// 使用 WinForms 的 NotifyIcon，配合 WPF 窗口共存。
    /// </summary>
    public class TrayIconService : IDisposable
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly Action _showMainWindow;
        private readonly Action _openSettings;
        private readonly Action _openScheduleEditor;
        private readonly Action _exitApplication;

        public TrayIconService(Action showMainWindow, Action openSettings,
            Action openScheduleEditor, Action exitApplication)
        {
            _showMainWindow = showMainWindow;
            _openSettings = openSettings;
            _openScheduleEditor = openScheduleEditor;
            _exitApplication = exitApplication;

            var menu = new ContextMenuStrip();

            var showItem = new ToolStripMenuItem("显示主窗口");
            showItem.Click += (sender, args) => _showMainWindow();
            menu.Items.Add(showItem);

            menu.Items.Add(new ToolStripSeparator());

            var scheduleItem = new ToolStripMenuItem("课表编辑");
            scheduleItem.Click += (sender, args) => _openScheduleEditor();
            menu.Items.Add(scheduleItem);

            var settingsItem = new ToolStripMenuItem("设置");
            settingsItem.Click += (sender, args) => _openSettings();
            menu.Items.Add(settingsItem);

            menu.Items.Add(new ToolStripSeparator());

            var exitItem = new ToolStripMenuItem("退出");
            exitItem.Click += (sender, args) => _exitApplication();
            menu.Items.Add(exitItem);

            _notifyIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Text = "ClassEasy 课表",
                Visible = true,
                ContextMenuStrip = menu
            };

            // 双击托盘图标显示主窗口。
            _notifyIcon.DoubleClick += (sender, args) => _showMainWindow();
        }

        public void Dispose()
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
    }
}