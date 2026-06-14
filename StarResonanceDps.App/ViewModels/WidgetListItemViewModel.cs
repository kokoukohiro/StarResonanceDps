using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.ViewModels;

public partial class WidgetListItemViewModel : ViewModelBase
{
    public WidgetKind Kind { get; init; }

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private WidgetState _state;

    // Theme default. Later, per-widget settings can replace this brush per item.
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

    partial void OnStateChanged(WidgetState value)
    {
        OnPropertyChanged(nameof(StateText));
        OnPropertyChanged(nameof(StatusGlyph));
        OnPropertyChanged(nameof(IsRunning));
    }
}
