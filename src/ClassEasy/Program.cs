using System;
using System.Threading;
using Avalonia;

namespace ClassEasy
{
    internal static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            // 单实例保护：已有实例运行时直接退出。
            using (var mutex = new Mutex(true, "Local\\ClassEasy.SingleInstance", out var createdNew))
            {
                if (!createdNew)
                {
                    return;
                }

                BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            }
        }

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .LogToTrace();
    }
}
