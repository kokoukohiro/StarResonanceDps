using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.ViewModels;

public sealed partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _searchText = string.Empty;

    public ObservableCollection<WidgetListItemViewModel> Widgets { get; } = new()
    {
        new() { Kind = WidgetKind.DpsMeter, DisplayName = "DPSメーター", State = WidgetState.Running },
        new() { Kind = WidgetKind.HpsMeter, DisplayName = "HPSメーター", State = WidgetState.Running },
        new() { Kind = WidgetKind.DtpsMeter, DisplayName = "DTPSメーター", State = WidgetState.Stopped },
        new() { Kind = WidgetKind.SkillLog, DisplayName = "スキルログ", State = WidgetState.Running },
        new() { Kind = WidgetKind.TrainingMode, DisplayName = "トレーニングモード", State = WidgetState.Stopped },
        new() { Kind = WidgetKind.PlayerInfoDebug, DisplayName = "PlayerInfo Debug", State = WidgetState.Running }
    };
}
