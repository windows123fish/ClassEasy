using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using ClassIsland.Core.Abstractions.Services;
using FluentAvalonia.UI.Windowing;
using HarmonyLib;

namespace ClassIsland.Platform.Windows.Patches;

[HarmonyPatch(typeof(AppWindow), "InitializeAppWindow")]
public class AppWindowInitializeAppWindowPatcher
{
    // net6 移植：UnsafeAccessor 是 .NET 8+ 运行时特性，改为反射访问私有属性。
    private static readonly PropertyInfo? IsWindowsProperty =
        typeof(AppWindow).GetProperty("IsWindows",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    static void Postfix(AppWindow __instance)
    {
        if (!IThemeService.UseNativeTitlebar) return;
        // StyledElement.PseudoClasses 非公开，伪类存储于公开的 Classes 集合中
        __instance.Classes.Remove(":windows");
        IsWindowsProperty?.SetValue(__instance, false);
    }
}
