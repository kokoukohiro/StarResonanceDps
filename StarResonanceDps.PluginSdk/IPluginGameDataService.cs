namespace StarResonanceDps.PluginSdk;

/// <summary>
/// Provides game state that only the host can receive from the game connection.
/// Values are read-only snapshots identified by game IDs.
///
/// <para>
/// Properties and methods can be read from any thread. Events are raised on the UI thread.
/// <see cref="PlayersChanged"/>, <see cref="EntitiesChanged"/> and <see cref="SessionChanged"/> are combined into one call per UI update.
/// Buffs and encounter totals have no change event; read them again when needed, as the host's widgets do.
/// </para>
/// </summary>
public interface IPluginGameDataService
{
    /// <summary>
    /// The game server of the client currently being captured.
    /// </summary>
    PluginGameServer CurrentServer { get; }

    /// <summary>
    /// The local player's modules, or <c>null</c> while the host has not received them
    /// (for example, the capture started after login and no map change has happened yet).
    /// </summary>
    PluginModuleInventory? SelfModules { get; }

    /// <summary>
    /// Raised on the UI thread after <see cref="SelfModules"/> has changed.
    /// </summary>
    event EventHandler? SelfModulesChanged;

    /// <summary>
    /// The connection and map state.
    /// </summary>
    PluginSessionState Session { get; }

    /// <summary>
    /// Raised after <see cref="Session"/> has changed.
    /// </summary>
    event EventHandler? SessionChanged;

    /// <summary>
    /// The host's player list: the local player, party members, nearby players and the players of the encounter shown in the meters.
    /// </summary>
    IReadOnlyList<PluginPlayer> Players { get; }

    /// <summary>
    /// Raised after <see cref="Players"/> has changed (including HP changes).
    /// </summary>
    event EventHandler? PlayersChanged;

    /// <summary>
    /// The entities around the player (the host's entity list).
    /// </summary>
    IReadOnlyList<PluginEntity> Entities { get; }

    /// <summary>
    /// Raised after <see cref="Entities"/> has changed (including HP changes).
    /// </summary>
    event EventHandler? EntitiesChanged;

    /// <summary>
    /// The buffs on a player, in the order they were applied. Empty when the host has no buffs for the player,
    /// and for another player who is not around.
    /// Always live, even while a history encounter is open.
    /// </summary>
    IReadOnlyList<PluginBuff> GetPlayerBuffs(long characterId);

    /// <summary>
    /// The buffs on an entity (for example <see cref="PluginEntity.EntityUuid"/>), in the order they were applied.
    /// Empty when the entity is no longer around.
    /// Always live, even while a history encounter is open.
    /// </summary>
    IReadOnlyList<PluginBuff> GetEntityBuffs(long entityUuid);

    /// <summary>
    /// The totals of the encounter the host's meters show: the opened history encounter, the previous encounter the meters keep showing,
    /// or the current one.
    /// </summary>
    PluginEncounterSnapshot GetEncounter();

    /// <summary>
    /// A player's skill breakdown in the encounter <see cref="GetEncounter"/> reads, largest first.
    /// </summary>
    IReadOnlyList<PluginSkillTotals> GetSkillTotals(long characterId, PluginCombatKind kind);

    /// <summary>
    /// Raised after a new encounter has started (a reset, a map change, a dungeon phase or a wipe).
    /// </summary>
    event EventHandler? EncounterStarted;

    /// <summary>
    /// Raised after an encounter with combat records has closed, with its final result.
    /// The closed encounter cannot be read later, so keep what you need from the result.
    /// </summary>
    event EventHandler<PluginEncounterResult>? EncounterClosed;

    /// <summary>
    /// Raised after the player has moved to another map. <see cref="Players"/> and <see cref="Entities"/> are rebuilt for the new map.
    /// </summary>
    event EventHandler? MapEntered;

    /// <summary>
    /// Raised after the player has returned to the login screen. The host has cleared what it received for the character.
    /// </summary>
    event EventHandler? LoggedOut;
}
