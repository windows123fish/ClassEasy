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
        GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            // Win7 老旧显卡驱动可能导致硬件加速崩溃，强制使用软件渲染。
            // Avalonia 11.0.0 的 Win32PlatformOptions 仅暴露 RenderingMode；
            // 这里把 Software 放在数组首项以确保渲染管线走软件路径，避免 Avalonia
            // 在 Win7 上静默退化到 Direct2D/GPU 合成。
            .With(new Win32PlatformOptions
            {
                RenderingMode = new[] { Win32RenderingMode.Software }
            })
            .WithInterFont()
            .LogToTrace();
}
