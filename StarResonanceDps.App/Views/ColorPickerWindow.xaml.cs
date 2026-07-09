using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.ViewModels;

namespace StarResonanceDps.App.Views;

public partial class ColorPickerWindow : Window, INotifyPropertyChanged
{
    private readonly ConfigManager _configManager = ConfigManager.Instance;

    public ColorPickerWindow()
        : this(null)
    {
    }

    public ColorPickerWindow(Color? initialColor)
    {
        RecentColors = new ColorPaletteViewModel([], AppConfigDefaults.MaxRecentColorCount);
        RecentColors.PaletteChanged += (_, _) => OnPropertyChanged(nameof(RecentColorsVisibility));

        InitializeComponent();

        var colorPickerConfig = _configManager.GetColorPickerSnapshot();
        RecentColors.LoadRecent(colorPickerConfig.RecentColors);

        if (initialColor.HasValue)
        {
            ColorPicker.SelectedColor = initialColor.Value;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ColorPaletteViewModel RecentColors { get; }

    public Visibility RecentColorsVisibility => RecentColors.Colors.Count > 0
        ? Visibility.Visible
        : Visibility.Collapsed;

    public Color SelectedColor { get; private set; }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void RecentColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ColorOptionViewModel option })
        {
            return;
        }

        ColorPicker.SelectedColor = option.Color;
    }

    private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!ColorPicker.IsHexTextInputFocused
            || e.OriginalSource is not DependencyObject source
            || IsInsideTextBox(source))
        {
            return;
        }

        MoveFocusToSink();
    }

    private void MoveFocusToSink()
    {
        Keyboard.ClearFocus();
        FocusManager.SetFocusedElement(this, KeyboardFocusSink);
        Keyboard.Focus(KeyboardFocusSink);
    }

    private static bool IsInsideTextBox(DependencyObject source)
    {
        var current = source;
        while (current != null)
        {
            if (current is TextBox)
            {
                return true;
            }

            current = GetParent(current);
        }

        return false;
    }

    private static DependencyObject? GetParent(DependencyObject current)
    {
        DependencyObject? visualParent = null;
        try
        {
            visualParent = VisualTreeHelper.GetParent(current);
        }
        catch (InvalidOperationException)
        {
        }

        return visualParent ?? LogicalTreeHelper.GetParent(current);
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        ColorPicker.CommitTextInput();
        SelectedColor = ColorPicker.SelectedColor;

        RecentColors.AddRecent(SelectedColor);
        _configManager.SaveColorPicker(new ColorPickerConfig
        {
            RecentColors = [.. RecentColors.GetHexColors()]
        });

        DialogResult = true;
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
