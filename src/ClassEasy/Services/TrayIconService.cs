using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClassEasy.Services
{
    /// <summary>
    /// 系统托盘服务：提供托盘图标与右键菜单（显示/隐藏、课表编辑、设置、穿透模式、退出）。
    /// 使用 WinForms 的 NotifyIcon，配合 WPF 窗口共存。
    /// </summary>
    public class TrayIconService : IDisposable
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly ToolStripMenuItem _clickThroughItem;

        private readonly Action _showMainWindow;
        private readonly Action _openSettings;
        private readonly Action _openScheduleEditor;
        private readonly Action<bool> _setClickThrough;   // bool = 是否勾选穿透模式（持久化）
        private readonly Func<bool> _getClickThrough;      // 读取主窗口当前穿透状态（打开菜单时同步 Checked）
        private readonly Action _exitApplication;

        public TrayIconService(
            Action showMainWindow,
            Action openSettings,
            Action openScheduleEditor,
            Action<bool> setClickThrough,
            Func<bool> getClickThrough,
            Action exitApplication)
        {
            _showMainWindow = showMainWindow;
            _openSettings = openSettings;
            _openScheduleEditor = openScheduleEditor;
            _setClickThrough = setClickThrough;
            _getClickThrough = getClickThrough;
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

            // 穿透模式：勾选菜单项 —— 每次打开菜单时根据主窗口的实时状态刷新 Checked。
            _clickThroughItem = new ToolStripMenuItem("点击穿透模式 (Ctrl+Alt+T)");
            _clickThroughItem.CheckOnClick = true;
            _clickThroughItem.Click += ClickThroughItem_Click;
            menu.Items.Add(_clickThroughItem);

            menu.Opening += Menu_Opening;

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

        /// <summary>
        /// 每次打开托盘菜单前，与主窗口的穿透状态做一次同步，
        /// 防止 Ctrl+Alt+T 切换后 Checked 显示不一致。
        /// </summary>
        private void Menu_Opening(object sender, EventArgs e)
        {
            try
            {
                _clickThroughItem.Checked = _getClickThrough?.Invoke() ?? false;
            }
            catch
            {
                // 打开菜单时即使查询异常也不能让托盘菜单崩掉（比如主窗口被释放）
                _clickThroughItem.Checked = false;
            }
        }

        private void ClickThroughItem_Click(object sender, EventArgs e)
        {
            // CheckOnClick=true 会先自动切换 Checked，这里用 Checked 的新值作为穿透目标值，
            // 并通知 App/MainWindow 持久化 + 调用 Win32 改样式。
            var item = (ToolStripMenuItem)sender;
            try
            {
                _setClickThrough?.Invoke(item.Checked);
            }
            catch (Exception ex)
            {
                LogService.Error("切换穿透模式失败", ex);
                // 回滚显示
                item.Checked = !item.Checked;
            }
        }

        public void Dispose()
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
    }
}