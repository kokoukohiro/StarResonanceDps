using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.ViewModels;

public sealed class PlayerEquipmentWidgetViewModel : PlayerWidgetWindowViewModel
{
    public PlayerEquipmentWidgetViewModel(
        WidgetListItemViewModel equipmentWidget,
        long? requestedCharacterId,
        PlayerRosterEntry? initialPlayer)
        : base(equipmentWidget, requestedCharacterId)
    {
        InitializePlayer(initialPlayer);
    }

    protected override void OnSelectedPlayerChanged(PlayerRosterEntry? player)
    {
    }
}
