using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.ViewModels;

public partial class WidgetListItemViewModel : ViewModelBase
{
    public WidgetKind Kind { get; init; }

    public int OriginalIndex { get; init; }

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private WidgetState _state;

    [ObservableProperty]
    private bool _isFavorite;

    [ObservableProperty]
    private bool _isPinned;

    [ObservableProperty]
    private int _windowOpacity = 100;

    [ObservableProperty]
    private ThemeColorPalette _themePalette = ThemeColorPalette.Create(Color.FromRgb(0x0B, 0x16, 0x24));

    private WidgetThemeConfig _theme = WidgetConfigDefaults.CreateTheme();

    public string StateText => State switch
    {
        WidgetState.Running => "起動中",
        WidgetState.Stopped => "停止中",
        WidgetState.Error => "エラー",
        _ => "不明"
    };

    public string StatusGlyph => State switch
    {
        WidgetState.Running => "●",
        WidgetState.Stopped => "●",
        WidgetState.Error => "●",
        _ => "●"
    };

    public bool IsRunning => State == WidgetState.Running;

    public WidgetConfig CreateWidgetConfig()
    {
        return new WidgetConfig
        {
            IsFavorite = IsFavorite,
            IsPinned = IsPinned,
            Theme = _theme.Clone()
        };
    }

    public void ApplyWidgetConfig(WidgetConfig config)
    {
        WidgetConfigDefaults.Normalize(config);

        IsFavorite = config.IsFavorite;
        IsPinned = config.IsPinned;
        ApplyTheme(config.Theme);
    }

    public void ApplyTheme(WidgetThemeConfig theme)
    {
        var normalized = WidgetConfigDefaults.CloneNormalizedTheme(theme);

        _theme = normalized.Clone();
        WindowOpacity = normalized.WindowOpacity;

        var selectedHex = normalized.WindowColors[
            Math.Clamp(normalized.WindowColorIndex, 0, normalized.WindowColors.Count - 1)];

        if (!ColorUtilities.TryParseHex(selectedHex, out var windowSurface))
        {
            windowSurface = Color.FromRgb(0x0B, 0x16, 0x24);
        }

        ThemePalette = ThemeColorPalette.Create(windowSurface);
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
        OnPropertyChanged(nameof(StatusGlyph));
        OnPropertyChanged(nameof(IsRunning));
    }
}
