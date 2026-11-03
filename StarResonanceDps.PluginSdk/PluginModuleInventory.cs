namespace StarResonanceDps.PluginSdk;

/// <summary>
/// The local player's modules received from the game, identified by game IDs only.
/// Names, tables and images belong to the plugin.
/// </summary>
/// <param name="Modules">Every module the player owns, ordered by UUID.</param>
/// <param name="EquippedUuidBySlot">Module slot number to the UUID of the module equipped in it.</param>
public sealed record PluginModuleInventory(
    IReadOnlyList<PluginModuleItem> Modules,
    IReadOnlyDictionary<int, long> EquippedUuidBySlot);

/// <summary>
/// One module.
/// </summary>
/// <param name="Uuid">The item UUID.</param>
/// <param name="ConfigId">The item ID.</param>
/// <param name="Quality">The item quality.</param>
/// <param name="Effects">The module effects in the game's order.</param>
public sealed record PluginModuleItem(
    long Uuid,
    int ConfigId,
    int Quality,
    IReadOnlyList<PluginModuleEffect> Effects);

/// <summary>
/// One effect of a module.
/// </summary>
/// <param name="EffectId">The module effect ID.</param>
/// <param name="LinkCount">The link count, or <c>null</c> when the game has not sent it.</param>
public sealed record PluginModuleEffect(int EffectId, int? LinkCount);
