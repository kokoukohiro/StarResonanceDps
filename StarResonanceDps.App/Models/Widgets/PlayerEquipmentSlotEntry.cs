using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.CombatRuntime.DataTypes;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.Models.Widgets;

public sealed record PlayerEquipmentAttributeEntry(
    string Name,
    int Minimum,
    int Maximum,
    int NumberFormat)
{
    public string RangeText
    {
        get
        {
            var minimum = (float)Minimum;
            var maximum = (float)Maximum;
            var symbol = string.Empty;

            if (NumberFormat == 1)
            {
                minimum = MathF.Round(minimum / 100.0f, 0);
                maximum = MathF.Round(maximum / 100.0f, 0);
                symbol = "%";
            }

            return $"({minimum}{symbol} - {maximum}{symbol})";
        }
    }
}

public sealed class PlayerEquipmentBreakthroughOption
{
    internal PlayerEquipmentBreakthroughOption(
        int level,
        int gearScore,
        IReadOnlyList<PlayerEquipmentAttributeEntry> basicAttributes,
        IReadOnlyList<PlayerEquipmentAttributeEntry> advancedAttributes)
    {
        Level = level;
        GearScore = gearScore;
        BasicAttributes = basicAttributes;
        AdvancedAttributes = advancedAttributes;
    }

    public int Level { get; }

    public int GearScore { get; }

    public string DisplayText => $"{Level} [GS: {GearScore}]";

    internal IReadOnlyList<PlayerEquipmentAttributeEntry> BasicAttributes { get; }

    internal IReadOnlyList<PlayerEquipmentAttributeEntry> AdvancedAttributes { get; }
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
        IReadOnlyList<PlayerEquipmentAttributeEntry> basicAttributes,
        IReadOnlyList<PlayerEquipmentAttributeEntry> advancedAttributes,
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
            equipment?.EquipGs ?? 0,
            basicAttributes,
            advancedAttributes);
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

    public ObservableCollection<PlayerEquipmentAttributeEntry> BasicAttributes { get; } = [];

    public ObservableCollection<PlayerEquipmentAttributeEntry> AdvancedAttributes { get; } = [];

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

    partial void OnSelectedBreakthroughChanged(PlayerEquipmentBreakthroughOption value)
    {
        ReplaceAttributes(BasicAttributes, value.BasicAttributes);
        ReplaceAttributes(AdvancedAttributes, value.AdvancedAttributes);
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
                Array.Empty<PlayerEquipmentAttributeEntry>(),
                Array.Empty<PlayerEquipmentAttributeEntry>(),
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
                Array.Empty<PlayerEquipmentAttributeEntry>(),
                Array.Empty<PlayerEquipmentAttributeEntry>(),
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
                Array.Empty<PlayerEquipmentAttributeEntry>(),
                Array.Empty<PlayerEquipmentAttributeEntry>(),
                Array.Empty<PlayerEquipmentBreakthroughOption>(),
                false);
        }

        var baseAttributes = ResolveAttributes(equipmentDataEntry, subProfessionId);
        var breakthroughs = HelperMethods.DataTables.EquipBreakThroughs.Data
            .Where(entry => entry.Value.EquipId == matchingEquipment.EquipmentId)
            .Select(entry => CreateBreakthroughOption(entry.Value, equipmentDataEntry, subProfessionId))
            .ToArray();

        return new PlayerEquipmentSlotEntry(
            slotName,
            slotId,
            0,
            itemDataEntry,
            equipmentDataEntry,
            baseAttributes.BasicAttributes,
            baseAttributes.AdvancedAttributes,
            breakthroughs,
            subProfessionId == 0 && itemDataEntry.Quality == 5);
    }

    private static PlayerEquipmentBreakthroughOption CreateBreakthroughOption(
        EquipBreakThrough breakthrough,
        Equip baseEquipment,
        int subProfessionId)
    {
        var equipment = new Equip
        {
            Id = breakthrough.EquipId,
            EquipPart = baseEquipment.EquipPart,
            EquipProfession = baseEquipment.EquipProfession,
            EquipGs = breakthrough.EquipGs,
            BasicAttrLibId = breakthrough.BasicAttrLibId,
            AdvancedAttrLibId = breakthrough.AdvancedAttrLibId
        };
        var attributes = ResolveAttributes(equipment, subProfessionId);

        return new PlayerEquipmentBreakthroughOption(
            breakthrough.BreakThroughTime,
            breakthrough.EquipGs,
            attributes.BasicAttributes,
            attributes.AdvancedAttributes);
    }

    private static PlayerEquipmentAttributeSet ResolveAttributes(Equip equipment, int subProfessionId)
    {
        var basicAttributes = BuildAttributeList(
            BuildAttributesFromLibraries(equipment.BasicAttrLibId, equipment.EquipPart, subProfessionId));
        var advancedAttributes = BuildAttributeList(
            BuildAttributesFromLibraries(equipment.AdvancedAttrLibId, equipment.EquipPart, subProfessionId));

        return new PlayerEquipmentAttributeSet(basicAttributes, advancedAttributes);
    }

    private static Dictionary<int, EquipAttrLib> BuildAttributesFromLibraries(
        IReadOnlyList<int> libraryIds,
        int equipmentPart,
        int subProfessionId)
    {
        Dictionary<int, EquipAttrLib> attributes = [];
        var libraryVersion = libraryIds.Count > 0 ? libraryIds[0] : 1;

        for (var index = 1; index < libraryIds.Count; index++)
        {
            var libraryId = libraryIds[index];

            if (libraryVersion == 1)
            {
                var matched = HelperMethods.DataTables.EquipAttrLibs.Data
                    .Where(entry => entry.Value.AttrLibId == libraryId
                        && entry.Value.AllowPart.Contains(equipmentPart))
                    .FirstOrDefault();

                if (matched.Value is not null)
                {
                    attributes.Add(libraryId, matched.Value);
                }

                continue;
            }

            if (libraryVersion == 2)
            {
                var matched = HelperMethods.DataTables.EquipAttrSchoolLibs.Data
                    .Where(entry => entry.Value.AttrLibId == libraryId
                        && entry.Value.AllowPart.Contains(equipmentPart)
                        && (subProfessionId <= 0
                            || entry.Value.TalentSchoolId.Contains(
                                Professions.GetTalentIdFromSubProfessionId(subProfessionId))))
                    .FirstOrDefault();

                if (matched.Value is not null)
                {
                    attributes.Add(libraryId, matched.Value.ToEquipAttrLib());
                }
            }
        }

        return attributes;
    }

    private static IReadOnlyList<PlayerEquipmentAttributeEntry> BuildAttributeList(
        IReadOnlyDictionary<int, EquipAttrLib> attributes)
    {
        List<PlayerEquipmentAttributeEntry> result = [];

        foreach (var attribute in attributes)
        {
            for (var index = 0; index < attribute.Value.AttrEffect.Count; index++)
            {
                var effect = attribute.Value.AttrEffect[index];
                var attributeType = effect.Count > 0 ? effect[0] : 1;

                if (attributeType == 1)
                {
                    foreach (var fightAttribute in HelperMethods.DataTables.FightAttrs.Data)
                    {
                        if (fightAttribute.Value.AttrAdd == effect[1])
                        {
                            var values = attribute.Value.AttrEffectConfig[index];
                            result.Add(new PlayerEquipmentAttributeEntry(
                                fightAttribute.Value.OfficialName,
                                values[0],
                                values[1],
                                fightAttribute.Value.AttrNumType));
                        }
                    }

                    continue;
                }

                if (attributeType != 3
                    || !HelperMethods.DataTables.Buffs.Data.TryGetValue(effect[1].ToString(), out var buff))
                {
                    continue;
                }

                var effectValues = attribute.Value.AttrEffectConfig[index];

                // 説明文は AttrDescription からしか作らない。実測(2026-09-01)で
                // type=3 の450件すべてが TipsDescription を持ち、すべて引けている。
                if (buff.TipsDescription > 0
                    && HelperMethods.DataTables.AttrDescriptions.Data.TryGetValue(
                        buff.TipsDescription.ToString(),
                        out var description))
                {
                    var text = description.DescriptionDecisionResolve(
                        [effectValues[0], effectValues[1]],
                        out var formats);

                    if (formats.Count > 0)
                    {
                        result.Add(new PlayerEquipmentAttributeEntry(
                            text,
                            effectValues[0],
                            effectValues[1],
                            formats.First()));
                    }
                }
            }
        }

        return result;
    }

    private static void ReplaceAttributes(
        Collection<PlayerEquipmentAttributeEntry> target,
        IReadOnlyList<PlayerEquipmentAttributeEntry> source)
    {
        target.Clear();

        foreach (var item in source)
        {
            target.Add(item);
        }
    }

    private sealed record PlayerEquipmentAttributeSet(
        IReadOnlyList<PlayerEquipmentAttributeEntry> BasicAttributes,
        IReadOnlyList<PlayerEquipmentAttributeEntry> AdvancedAttributes);
}
