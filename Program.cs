using System;
using System.Runtime;
using Avalonia;
using Avalonia.Win32;

namespace ClassEasy;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // 针对 8GB 内存的 Win7：采用持续低延迟 GC 模式，减少后台 GC 造成的卡顿
        GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            // Win7 老旧显卡驱动可能导致硬件加速崩溃，强制使用软件渲染
            .With(new Win32PlatformOptions
            {
                RenderingMode = new[] { Win32RenderingMode.Software }
            })
            .WithInterFont()
            .LogToTrace();
}
