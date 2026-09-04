# Debug Session: app-launch-no-response
- **Status**: [RESOLVED]
- **Reported**: 2026-09-04 用户双击编译好的 .exe 后"什么反应也没有"
- **Resolved**: 2026-09-04

## 根因 (Root Cause)
**H4 确认成立**: Avalonia 11 的 XAML 编译生成代码 `!XamlIlPopulate` 在处理 `<TrayIcon.Icons>` 声明时会触发 `NullReferenceException`（精确位置：App.axaml 中任何 NativeMenuItem 行）。该异常导致 `App.Initialize()` 失败，Avalonia 生命周期在创建主窗口之前就静默退出，因此用户看到的是"没反应"（进程短暂存在但立即退出，没有窗口、没有托盘、没有错误提示）。

## 证据链
1. `EnumWindows` 枚举 ClassEasy 进程下的可见窗口 → **NO VISIBLE WINDOWS**
2. 插桩后的 crash.txt 堆栈：
   ```
   System.NullReferenceException
      at ClassEasy.App.!XamlIlPopulate(IServiceProvider, App) in App.axaml:line 23
      at ClassEasy.App.!XamlIlPopulateTrampoline(App)
      at ClassEasy.App.Initialize() in App.axaml.cs:line 22
   ```
   line 23 正好是 `<NativeMenuItem Header="退出" Click="OnExitApp" />`
3. 二分验证：移除整个 `<TrayIcon.Icons>` 块 → **窗口出现，位置 (443,12)，Size=820x92，可见**

## 修复
| 文件 | 变更 |
|------|------|
| [App.axaml](file:///D:/main/ClassEasy/src/ClassEasy/App.axaml) | 删除 `<TrayIcon.Icons>...</TrayIcon.Icons>` 整个块 |
| [App.axaml.cs](file:///D:/main/ClassEasy/src/ClassEasy/App.axaml.cs) | 新增 `SetupTrayIcon()` 方法，在 `OnFrameworkInitializationCompleted()` 中用代码构建 TrayIcon + NativeMenu + 图标；若 `TrayIcon.GetIcons(this)` 返回 null 则先 `new TrayIcons()` 并 `SetValue(TrayIcon.IconsProperty, icons)` |

## 经验教训
- **Avalonia 11 的 TrayIcon 不能在 Application.XAML (App.axaml) 里直接用 `<TrayIcon.Icons>` 属性声明**——编译时不报错但运行时 XamlIlPopulate 会 NRE。这应当是 Avalonia 11 的已知限制/缺陷。
- `ShowInTaskbar=False` + `WS_EX_TOOLWINDOW` 的窗口在应用启动初期用户非常容易"误以为没启动"，后续版本建议在首次启动时在屏幕中央显示一次性欢迎提示或引导说明。

## 验证结果
- Debug build：0 错 0 警；启动后 HWND 存在，Position / Size / EXSTYLE (TOOL=True, APP=False) 均符合预期
- Publish 单文件：`D:\main\ClassEasy\publish\ClassEasy.exe` (43.67 MB)，单文件，启动后窗口可见，无崩溃文件

## 调试产物
- 已清理 Program.cs / App.axaml.cs / MainWindow.axaml.cs 中的 debug-point 插桩
- 遗留供参考文件：`.dbg/` 目录与 `debug-app-launch-no-response.md` 保留至用户确认后删除
