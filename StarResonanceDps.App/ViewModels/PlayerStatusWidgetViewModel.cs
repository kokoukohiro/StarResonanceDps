using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.ViewModels;

public sealed partial class PlayerStatusWidgetViewModel : PlayerWidgetWindowViewModel
{
    [ObservableProperty]
    private PlayerStatusEntry? _playerStatus;

    public PlayerStatusWidgetViewModel(
        WidgetListItemViewModel statusWidget,
        long? requestedCharacterId,
        PlayerRosterEntry? initialPlayer)
        : base(statusWidget, requestedCharacterId, showPlayerIdentityInHeader: false)
    {
        InitializePlayer(initialPlayer);
    }

    public WidgetListItemViewModel StatusWidget => PlayerWidget;

    protected override void OnSelectedPlayerChanged(PlayerRosterEntry? player)
    {
        PlayerStatus = player is null
            ? null
            : PlayerStatusEntry.Create(player);
    }
}
