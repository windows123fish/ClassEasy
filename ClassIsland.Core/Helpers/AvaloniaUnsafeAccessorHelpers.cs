using System.Reflection;
using Avalonia;
using Avalonia.Platform;
using Avalonia.Rendering;
using Avalonia.Vulkan;

namespace ClassIsland.Core.Helpers;

internal static class AvaloniaUnsafeAccessorHelpers
{
    // net6 移植：UnsafeAccessor 是 .NET 8+ 运行时特性，net6 上改为反射访问。
    private static readonly PropertyInfo? CurrentProperty =
        typeof(AvaloniaLocator).GetProperty("Current",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

    private static IAvaloniaDependencyResolver? AvaloniaLocator { get; } =
        CurrentProperty?.GetValue(null) as IAvaloniaDependencyResolver;

    // GetService 声明于内部基接口，编译期不可直接访问，运行时通过反射调用。
    private static readonly MethodInfo? GetServiceMethod =
        typeof(IAvaloniaDependencyResolver).GetMethod("GetService",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, new[] { typeof(Type) }, null);

    internal static T? GetAvaloniaLocatorService<T>()
        where T : class
    {
        if (AvaloniaLocator is null || GetServiceMethod is null)
            return null;
        object? result = GetServiceMethod.Invoke(AvaloniaLocator, new object?[] { typeof(T) });
        return result as T;
    }

    public static Win32CompositionMode? GetActiveWin32CompositionMode()
    {
        var renderTimer = GetAvaloniaLocatorService<IRenderTimer>();
        var renderTimerClassName = renderTimer?.GetType().Name;
        var win32CompositionMode = renderTimerClassName switch
        {
            "WinUiCompositorConnection" => Win32CompositionMode.WinUIComposition,
            "DirectCompositionConnection" => Win32CompositionMode.DirectComposition,
            "DxgiConnection" => Win32CompositionMode.LowLatencyDxgiSwapChain,
            _ => Win32CompositionMode.RedirectionSurface
        };
        return win32CompositionMode;
    }

    public static Win32ActiveRenderingMode? GetActiveWin32RenderingMode()
    {
        var platformGraphics = GetAvaloniaLocatorService<IPlatformGraphics>();
        var platformGraphicsClassName = platformGraphics?.GetType().Name;
        return platformGraphicsClassName switch
        {
            null when GetAvaloniaLocatorService<Win32PlatformOptions>()?.CustomPlatformGraphics is not null => Win32ActiveRenderingMode.Custom,
            null => Win32ActiveRenderingMode.Software,
            "D3D9AngleWin32PlatformGraphics" => Win32ActiveRenderingMode.AngleEglD3D9,
            "D3D11AngleWin32PlatformGraphics" => Win32ActiveRenderingMode.AngleEglD3D11,
            "WglPlatformOpenGlInterface" => Win32ActiveRenderingMode.Wgl,
            nameof(VulkanPlatformGraphics) => Win32ActiveRenderingMode.Vulkan,
            _ => null
        };
    }

    public enum Win32ActiveRenderingMode
    {
        Custom,
        Software,
        AngleEglD3D9,
        AngleEglD3D11,
        Wgl,
        Vulkan
    }
}
