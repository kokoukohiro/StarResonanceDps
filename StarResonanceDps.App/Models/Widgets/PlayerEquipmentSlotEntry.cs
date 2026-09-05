using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.CombatRuntime.DataTypes;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.Models.Widgets;

public sealed class PlayerEquipmentBreakthroughOption
{
    internal PlayerEquipmentBreakthroughOption(
        int level,
        int gearScore)
    {
        Level = level;
        GearScore = gearScore;
    }

    public int Level { get; }

    public int GearScore { get; }

    public string DisplayText => $"{Level} [GS: {GearScore}]";

}

public sealed partial class PlayerEquipmentSlotEntry : ObservableObject
{
    private static readonly (string SlotName, int SlotId)[] GearSlots =
    [
        ("Weapon", 200),
        ("Head", 201),
        ("Chest", 202),
        ("Hands", 203),
        ("Boots", 204),
        ("Earring", 205),
        ("Necklace", 206),
        ("Ring", 207),
        ("Bracelet (L)", 208),
        ("Bracelet (R)", 209),
        ("Charm", 210)
    ];

    private readonly ReadOnlyCollection<PlayerEquipmentBreakthroughOption> _breakthroughOptions;

    private PlayerEquipmentSlotEntry(
        string slotName,
        int slotId,
        int unresolvedEquipmentId,
        Item? item,
        Equip? equipment,
        IReadOnlyList<PlayerEquipmentBreakthroughOption> breakthroughOptions,
        bool showUnknownSpecWarning)
    {
        SlotName = slotName;
        SlotId = slotId;
        UnresolvedEquipmentId = unresolvedEquipmentId;
        Item = item;
        Equipment = equipment;
        ShowUnknownSpecWarning = showUnknownSpecWarning;

        var baseOption = new PlayerEquipmentBreakthroughOption(
            0,
            equipment?.EquipGs ?? 0);
        var options = new[] { baseOption }
            .Concat(breakthroughOptions)
            .ToArray();

        _breakthroughOptions = Array.AsReadOnly(options);
        SelectedBreakthrough = baseOption;
    }

    public string SlotName { get; }

    public int SlotId { get; }

    public int UnresolvedEquipmentId { get; }

    public Item? Item { get; }

    public Equip? Equipment { get; }

    public bool HasKnownItem => Item is not null && Equipment is not null;

    public bool HasBreakthroughs => _breakthroughOptions.Count > 1;

    public bool ShowQualityWarning => HasKnownItem && Item!.Quality < 4;

    public bool ShowUnknownSpecWarning { get; }

    public string SlotText => $"Slot ({SlotId}): {SlotName}";

    public string UnknownItemText => UnresolvedEquipmentId > 0
        ? $"Item ({UnresolvedEquipmentId}): <UNKNOWN>"
        : "Item: <UNKNOWN>";

    public string ItemText => HasKnownItem
        ? $"Item ({Equipment!.Id}): {Item!.Name}"
        : string.Empty;

    public string GearScoreText => HasKnownItem
        ? $"GS: {Equipment!.EquipGs}"
        : string.Empty;

    public ReadOnlyCollection<PlayerEquipmentBreakthroughOption> BreakthroughOptions => _breakthroughOptions;

    [ObservableProperty]
    private PlayerEquipmentBreakthroughOption _selectedBreakthrough = null!;

    public static IReadOnlyList<PlayerEquipmentSlotEntry> CreateAll(
        PlayerEquipmentData equipmentData,
        int subProfessionId)
    {
        ArgumentNullException.ThrowIfNull(equipmentData);

        return GearSlots
            .Select(slot => Create(slot.SlotName, slot.SlotId, equipmentData, subProfessionId))
            .ToArray();
    }

    private static PlayerEquipmentSlotEntry Create(
        string slotName,
        int slotId,
        PlayerEquipmentData equipmentData,
        int subProfessionId)
    {
        var matchingEquipment = equipmentData.Items
            .FirstOrDefault(item => item.Slot == slotId);
        var hasMatchingEquipment = equipmentData.Items
            .Any(item => item.Slot == slotId);

        if (!hasMatchingEquipment)
        {
            return new PlayerEquipmentSlotEntry(
                slotName,
                slotId,
                0,
                null,
                null,
                Array.Empty<PlayerEquipmentBreakthroughOption>(),
                false);
        }

        if (!HelperMethods.DataTables.Equips.Data.TryGetValue(
                matchingEquipment.EquipmentId.ToString(),
                out var equipmentDataEntry))
        {
            return new PlayerEquipmentSlotEntry(
                slotName,
                slotId,
                matchingEquipment.EquipmentId,
                null,
                null,
                Array.Empty<PlayerEquipmentBreakthroughOption>(),
                false);
        }

        if (!HelperMethods.DataTables.Items.Data.TryGetValue(
                matchingEquipment.EquipmentId.ToString(),
                out var itemDataEntry))
        {
            return new PlayerEquipmentSlotEntry(
                slotName,
                slotId,
                0,
                null,
                null,
                Array.Empty<PlayerEquipmentBreakthroughOption>(),
                false);
        }

        var breakthroughs = HelperMethods.DataTables.EquipBreakThroughs.Data
            .Where(entry => entry.Value.EquipId == matchingEquipment.EquipmentId)
            .Select(entry => CreateBreakthroughOption(entry.Value))
            .ToArray();

        return new PlayerEquipmentSlotEntry(
            slotName,
            slotId,
            0,
            itemDataEntry,
            equipmentDataEntry,
            breakthroughs,
            subProfessionId == 0 && itemDataEntry.Quality == 5);
    }

    private static PlayerEquipmentBreakthroughOption CreateBreakthroughOption(EquipBreakThrough breakthrough)
    {
        return new PlayerEquipmentBreakthroughOption(
            breakthrough.BreakThroughTime,
            breakthrough.EquipGs);
    }
}
