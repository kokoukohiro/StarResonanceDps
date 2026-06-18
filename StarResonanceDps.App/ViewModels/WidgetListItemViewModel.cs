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
            Theme = new WidgetThemeConfig
            {
                WindowColorIndex = WindowColorIndex,
                TextColorIndex = TextColorIndex,
                WindowOpacity = WindowOpacity
            }
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
        PanelBrush = WindowColorIndex switch
        {
            1 => CreateBrush(0x22, 0x1B, 0x49),
            2 => CreateBrush(0x1D, 0x35, 0x25),
            3 => CreateBrush(0x3A, 0x28, 0x13),
            4 => CreateBrush(0x3A, 0x1A, 0x2A),
            _ => null
        };
    }

    private static SolidColorBrush CreateBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
