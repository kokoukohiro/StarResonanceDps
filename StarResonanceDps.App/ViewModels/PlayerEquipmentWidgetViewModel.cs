using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.ViewModels;

public sealed partial class PlayerEquipmentWidgetViewModel : PlayerWidgetWindowViewModel
{
    private readonly ObservableCollection<PlayerEquipmentSlotEntry> _equipmentSlots = [];
    private PlayerEquipmentData? _lastEquipmentData;

    [ObservableProperty]
    private bool _showUnknownAttributes;

    [ObservableProperty]
    private PlayerEquipmentDataState _equipmentDataState = PlayerEquipmentDataState.Missing;

    public PlayerEquipmentWidgetViewModel(
        WidgetListItemViewModel equipmentWidget,
        long? requestedCharacterId,
        PlayerRosterEntry? initialPlayer)
        : base(equipmentWidget, requestedCharacterId)
    {
        EquipmentSlots = new ReadOnlyObservableCollection<PlayerEquipmentSlotEntry>(_equipmentSlots);
        InitializePlayer(initialPlayer);
    }

    public WidgetListItemViewModel EquipmentWidget => PlayerWidget;

    public ReadOnlyObservableCollection<PlayerEquipmentSlotEntry> EquipmentSlots { get; }

    public bool HasEquipmentData => EquipmentDataState == PlayerEquipmentDataState.Available;

    public bool HasInvalidEquipmentData => EquipmentDataState == PlayerEquipmentDataState.Invalid;

    public bool HasNoEquipmentData => EquipmentDataState == PlayerEquipmentDataState.Missing;

    partial void OnEquipmentDataStateChanged(PlayerEquipmentDataState value)
    {
        OnPropertyChanged(nameof(HasEquipmentData));
        OnPropertyChanged(nameof(HasInvalidEquipmentData));
        OnPropertyChanged(nameof(HasNoEquipmentData));
    }

    protected override void OnSelectedPlayerChanged(PlayerRosterEntry? player)
    {
        var equipmentData = player?.EquipmentData;
        var dataState = equipmentData?.State ?? PlayerEquipmentDataState.Missing;
        var subProfessionId = player?.SubProfessionId ?? 0;

        if (Equals(_lastEquipmentData, equipmentData)
            && EquipmentDataState == dataState)
        {
            return;
        }

        _lastEquipmentData = equipmentData;
        EquipmentDataState = dataState;

        _equipmentSlots.Clear();
        if (equipmentData?.State != PlayerEquipmentDataState.Available)
        {
            return;
        }

        foreach (var slot in PlayerEquipmentSlotEntry.CreateAll(equipmentData, subProfessionId))
        {
            _equipmentSlots.Add(slot);
        }
    }
}
