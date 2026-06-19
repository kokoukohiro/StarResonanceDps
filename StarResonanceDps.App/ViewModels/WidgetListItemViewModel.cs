using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarResonanceDps.App.Config;
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
    private int _windowColorIndex;

    [ObservableProperty]
    private int _textColorIndex;

    [ObservableProperty]
    private int _windowOpacity = 100;

    [ObservableProperty]
    private Brush? _panelBrush;

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
        WindowColorIndex = normalized.WindowColorIndex;
        TextColorIndex = normalized.TextColorIndex;
        WindowOpacity = normalized.WindowOpacity;
        UpdatePanelBrush();
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

    partial void OnWindowColorIndexChanged(int value)
    {
        UpdatePanelBrush();
    }

    private void UpdatePanelBrush()
    {
        WidgetConfigDefaults.NormalizeTheme(_theme);

        if (_theme.WindowColorIndex <= 0 || _theme.WindowColors.Count == 0)
        {
            PanelBrush = null;
            return;
        }

        var hex = _theme.WindowColors[Math.Clamp(_theme.WindowColorIndex, 0, _theme.WindowColors.Count - 1)];
        PanelBrush = TryCreateBrush(hex, out var brush) ? brush : null;
    }

    private static bool TryCreateBrush(string hex, out SolidColorBrush brush)
    {
        brush = new SolidColorBrush(Colors.Transparent);

        if (hex.StartsWith("#", StringComparison.Ordinal))
        {
            hex = hex[1..];
        }

        if (hex.Length != 6
            || !byte.TryParse(hex[..2], System.Globalization.NumberStyles.HexNumber, null, out var r)
            || !byte.TryParse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out var g)
            || !byte.TryParse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out var b))
        {
            return false;
        }

        brush = new SolidColorBrush(Color.FromArgb(0x33, r, g, b));
        brush.Freeze();
        return true;
    }
}
