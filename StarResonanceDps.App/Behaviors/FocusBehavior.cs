using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace StarResonanceDps.App.Behaviors;

public static class FocusBehavior
{
    public static readonly DependencyProperty ClearFocusOnBackgroundClickProperty = DependencyProperty.RegisterAttached(
        "ClearFocusOnBackgroundClick",
        typeof(bool),
        typeof(FocusBehavior),
        new PropertyMetadata(false, OnClearFocusOnBackgroundClickChanged));

    public static bool GetClearFocusOnBackgroundClick(DependencyObject obj)
    {
        return (bool)obj.GetValue(ClearFocusOnBackgroundClickProperty);
    }

    public static void SetClearFocusOnBackgroundClick(DependencyObject obj, bool value)
    {
        obj.SetValue(ClearFocusOnBackgroundClickProperty, value);
    }

    private static void OnClearFocusOnBackgroundClickChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            element.MouseDown += OnMouseDown;
        }
        else
        {
            element.MouseDown -= OnMouseDown;
        }
    }

    private static void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        if (IsInsideInteractiveElement(source))
        {
            return;
        }

        Keyboard.ClearFocus();

        if (sender is DependencyObject focusScopeSource)
        {
            var focusScope = FocusManager.GetFocusScope(focusScopeSource);
            FocusManager.SetFocusedElement(focusScope, null);
        }
    }

    private static bool IsInsideInteractiveElement(DependencyObject source)
    {
        while (source is not null)
        {
            if (source is TextBoxBase
                or PasswordBox
                or ButtonBase
                or ComboBox
                or Selector
                or MenuItem)
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }
}
