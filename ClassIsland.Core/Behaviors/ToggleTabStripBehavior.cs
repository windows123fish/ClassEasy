using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Xaml.Interactivity;

namespace ClassIsland.Core.Behaviors;

/// <summary>
///
/// </summary>
public class ToggleTabStripBehavior : Behavior<TabStrip>
{
    // net6 移植：UnsafeAccessor 是 .NET 8+ 运行时特性，改用公开的 Classes 集合（实现 IPseudoClasses）。
    private static IPseudoClasses GetPseudoClasses(StyledElement element) => element.Classes;

    protected override void OnAttached()
    {
        if (AssociatedObject != null)
        {
            AssociatedObject.PointerPressed += AssociatedObjectOnPointerPressed;
            AssociatedObject.PointerReleased += AssociatedObjectOnPointerReleased;
        }
        base.OnAttached();
    }

    private void AssociatedObjectOnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        e.Handled = true;
        if (AssociatedObject != null)
        {
            var ni = AssociatedObject.SelectedIndex + 1;
            AssociatedObject.SelectedIndex = ni >= AssociatedObject.ItemsView.Count ? 0 : ni;
            GetPseudoClasses(AssociatedObject).Set(":pointerdown", false);
        }
    }

    private void AssociatedObjectOnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
        if (AssociatedObject != null)
        {
            GetPseudoClasses(AssociatedObject).Set(":pointerdown", true);
        }
    }

    protected override void OnDetaching()
    {
        if (AssociatedObject != null)
        {
            GetPseudoClasses(AssociatedObject).Set(":pointerdown", false);
            AssociatedObject.PointerPressed -= AssociatedObjectOnPointerPressed;
            AssociatedObject.PointerReleased -= AssociatedObjectOnPointerReleased;
        }
        base.OnDetaching();
    }
}