namespace StarResonanceDps.PluginSdk;

/// <summary>
/// One player's build: root buffs, talents, equipment, skill levels, factors and module effects.
/// From <see cref="IPluginGameDataService.GetPlayerBuild"/> (live values, the same sources as the host's player widgets)
/// or <see cref="IPluginEncounterRecord.GetPlayerBuild"/> (the values saved with a history encounter).
///
/// <para>
/// Each list is <c>null</c> when the host has not received it (or did not save it), and empty when it was received empty.
/// Records compare lists by reference, so compare the contents yourself.
/// </para>
/// </summary>
/// <param name="CharacterId">The character ID.</param>
/// <param name="RootBuffIds">
/// The root buffs on the player (talents, Deep-Slumber Psychoscope node and advanced node effects, factors and module effects), in ascending order.
/// <c>null</c> until the host has received all of the player's buffs.
/// </param>
/// <param name="TalentNodeIds">
/// The talent tree nodes taken for the current class, in ascending order. Only the local player; <c>null</c> for other players.
/// </param>
/// <param name="LastUsedReplaceableSkills">
/// For the talents that replace a skill without adding a buff: the original skill ID mapped to the skill ID used last (the original or the replacement).
/// <c>null</c> while none of them has been used.
/// </param>
/// <param name="Equipment">The equipped items, or <c>null</c> when not received.</param>
/// <param name="SkillLevels">
/// The skill levels, or <c>null</c> when not received.
/// For the local player, the learned skills of the current class, the role skills and the selected imagines;
/// for other players, the list the game sends for them (imagines: only the two equipped);
/// for a party member who is not nearby, the last list the party sync sent.
/// </param>
/// <param name="Factors">
/// The factors on the active Deep-Slumber Psychoscope type of the current season. Only the local player; <c>null</c> for other players.
/// </param>
/// <param name="ModuleEffects">
/// The effects of the equipped modules; <see cref="PluginModuleEffect.LinkCount"/> is summed over the equipped modules.
/// Only the local player; <c>null</c> for other players.
/// </param>
public sealed record PluginPlayerBuild(
    long CharacterId,
    IReadOnlyList<int>? RootBuffIds,
    IReadOnlyList<int>? TalentNodeIds,
    IReadOnlyDictionary<int, int>? LastUsedReplaceableSkills,
    PluginEquipment? Equipment,
    IReadOnlyList<PluginSkillLevel>? SkillLevels,
    IReadOnlyList<PluginSeasonFactor>? Factors,
    IReadOnlyList<PluginModuleEffect>? ModuleEffects);

/// <summary>
/// A player's equipped items.
/// </summary>
/// <param name="IsReadable"><c>false</c> when the game sent the equipment in a form the host cannot read; <see cref="Items"/> is then empty.</param>
/// <param name="Items">The equipped items.</param>
public sealed record PluginEquipment(bool IsReadable, IReadOnlyList<PluginEquipmentItem> Items);

/// <summary>
/// One equipped item.
/// </summary>
/// <param name="Slot">The equipment slot number.</param>
/// <param name="EquipmentId">The equipment (item) ID. Use <see cref="IPluginNameService.GetItemName"/> for its name.</param>
public sealed record PluginEquipmentItem(int Slot, int EquipmentId);

/// <summary>
/// One skill and its level.
/// </summary>
/// <param name="SkillId">The skill ID. Use <see cref="IPluginNameService.GetSkillName"/> for its name.</param>
/// <param name="Level">The skill level.</param>
/// <param name="RemodelLevel">The skill grade (the host shows it as G1, G2 and so on).</param>
public sealed record PluginSkillLevel(int SkillId, int Level, int RemodelLevel);

/// <summary>
/// One factor on a Deep-Slumber Psychoscope type.
/// </summary>
/// <param name="SlotId">The factor slot number.</param>
/// <param name="ItemId">The factor's item ID. Use <see cref="IPluginNameService.GetItemName"/> for its name.</param>
public sealed record PluginSeasonFactor(int SlotId, int ItemId);
