using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Services;
using StarResonanceDps.App.Models.Widgets;

namespace StarResonanceDps.App.ViewModels;

public partial class WidgetListItemViewModel : ViewModelBase
{
    public WidgetKind Kind { get; init; }

    public int OriginalIndex { get; init; }

    public string DisplayNameResourceKey { get; init; } = string.Empty;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private WidgetState _state;

    [ObservableProperty]
    private bool _isFavorite;

    [ObservableProperty]
    private bool _isPinned;

    [ObservableProperty]
    private ThemeColorPalette _themePalette = ThemeColorPalette.Create(Color.FromRgb(0x0B, 0x16, 0x24));

    [ObservableProperty]
    private WidgetWindowThemePalette _widgetWindowPalette = WidgetWindowThemePalette.Create(Color.FromRgb(0x0B, 0x16, 0x24), 50);

    private WidgetThemeConfig _theme = WidgetConfigDefaults.CreateTheme();

    public string StateText => State == WidgetState.Running
        ? LocalizationManager.Instance.GetString("Widget_State_Running")
        : LocalizationManager.Instance.GetString("Widget_State_Stopped");

    public bool IsRunning => State == WidgetState.Running;

    public void RefreshLocalizedText()
    {
        DisplayName = LocalizationManager.Instance.GetString(DisplayNameResourceKey);
        OnPropertyChanged(nameof(StateText));
    }

    public WidgetConfig CreateWidgetConfig()
    {
        return new WidgetConfig
        {
            IsFavorite = IsFavorite,
            IsPinned = IsPinned,
            State = State,
            Theme = _theme.Clone()
        };
    }

    public void ApplyWidgetConfig(WidgetConfig config)
    {
        WidgetConfigDefaults.Normalize(config);

        IsFavorite = config.IsFavorite;
        IsPinned = config.IsPinned;

        if (config.State is { } state)
        {
            State = state;
        }

        ApplyTheme(config.Theme);
    }

    public void ApplyTheme(WidgetThemeConfig theme)
    {
        var normalized = WidgetConfigDefaults.CloneNormalizedTheme(theme);

        _theme = normalized.Clone();

        var selectedHex = normalized.WindowColors[
            Math.Clamp(normalized.WindowColorIndex, 0, normalized.WindowColors.Count - 1)];

        if (!ColorUtilities.TryParseHex(selectedHex, out var windowSurface))
        {
            windowSurface = Color.FromRgb(0x0B, 0x16, 0x24);
        }

        ThemePalette = ThemeColorPalette.Create(windowSurface);
        WidgetWindowPalette = WidgetWindowThemePalette.Create(windowSurface, normalized.WindowOpacity);
    }

    [RelayCommand]
    private void ToggleFavorite()
    {
        IsFavorite = !IsFavorite;
    }

    [RelayCommand]
    private void TogglePin()
    {
        IsPinned = !IsPinned;
    }

    [RelayCommand]
    private void ToggleRunning()
    {
        State = State == WidgetState.Running
            ? WidgetState.Stopped
            : WidgetState.Running;
    }

    partial void OnStateChanged(WidgetState value)
    {
        OnPropertyChanged(nameof(StateText));
        OnPropertyChanged(nameof(IsRunning));
    }
}
