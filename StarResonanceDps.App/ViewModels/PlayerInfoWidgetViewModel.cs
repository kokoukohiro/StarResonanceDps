using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.ViewModels;

public sealed partial class PlayerInfoWidgetViewModel : PlayerWidgetWindowViewModel
{
    [ObservableProperty]
    private PlayerInfoEntry? _playerInfo;

    public PlayerInfoWidgetViewModel(
        WidgetListItemViewModel infoWidget,
        long? requestedCharacterId,
        PlayerRosterEntry? initialPlayer)
        : base(infoWidget, requestedCharacterId)
    {
        InitializePlayer(initialPlayer);
    }

    public WidgetListItemViewModel InfoWidget => PlayerWidget;

    protected override void OnSelectedPlayerChanged(PlayerRosterEntry? player)
    {
        PlayerInfo = player is null
            ? null
            : PlayerInfoEntry.Create(player);
    }
}
