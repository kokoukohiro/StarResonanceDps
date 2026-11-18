namespace StarResonanceDps.PluginSdk;

/// <summary>
/// Provides game state that only the host can receive from the game connection.
/// Values are read-only snapshots identified by game IDs.
///
/// <para>
/// Properties and methods can be read from any thread. Events are raised on the UI thread.
/// <see cref="SessionChanged"/>, <see cref="PlayersChanged"/>, <see cref="EntitiesChanged"/>, <see cref="SelfModulesChanged"/>,
/// <see cref="ShownEncounterChanged"/> and <see cref="AggregationSettingsChanged"/>
/// are combined into one call per UI update; the other events are raised once for each occurrence.
/// Buffs, encounter totals, <see cref="GetPlayer"/> and <see cref="GetPlayerBuild"/> have no change event; read them again when needed, as the host's widgets do.
/// The moments buffs are applied and removed come with <see cref="IPluginContext.CombatEvents"/>.
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
    /// It is also <c>null</c> after logging out, and after the capture restarts because the network adapter or game executable setting changed,
    /// until the next map change.
    /// </summary>
    PluginModuleInventory? SelfModules { get; }

    /// <summary>
    /// Raised on the UI thread after <see cref="SelfModules"/> has been replaced: each time the player enters a map (even when nothing changed),
    /// when modules are obtained, dismantled or equipped, and when it becomes <c>null</c>.
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
    /// The players of the list the Player List widget is built from: the local player, party members, nearby players,
    /// and the players with records (damage, healing, damage taken or timeline records) in the encounter the meters show.
    /// The widget's filter and name sorting are not applied. The order is the local player, party members (by party number),
    /// players who are neither nearby nor in the party, players in combat, and the others.
    /// </summary>
    IReadOnlyList<PluginPlayer> Players { get; }

    /// <summary>
    /// Raised after <see cref="Players"/> has changed (including HP changes).
    /// </summary>
    event EventHandler? PlayersChanged;

    /// <summary>
    /// The monsters around the player, from the list the Entity List widget is built from. The widget's filter and name sorting are not applied.
    /// Monsters in combat come first, then the others; within each group, bosses, elites, normal monsters and the rest.
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
    /// One player by character ID, with the same values as the host's player widgets:
    /// a player of <see cref="Players"/> has the same row; another player has the last values after leaving the list
    /// (<see cref="PluginPlayer.IsNearby"/> is <c>false</c>), or the values received from a namecard or chat.
    /// <c>null</c> when the host has none of them; they are discarded on logout.
    /// The name follows the host's display settings, as in <see cref="Players"/>.
    /// It has no change event: the values of a player who is not in <see cref="Players"/> change only with namecards and chat.
    /// </summary>
    PluginPlayer? GetPlayer(long characterId);

    /// <summary>
    /// The build of the player <see cref="GetPlayer"/> returns, or <c>null</c> when it returns <c>null</c>.
    /// Each call makes a new snapshot; it has no change event.
    /// While a history encounter is open in the host, a player who is only in that encounter has the values saved with it,
    /// and the others have live values (the same as the host's player widgets).
    /// </summary>
    PluginPlayerBuild? GetPlayerBuild(long characterId);

    /// <summary>
    /// The totals of the encounter the host's meters show: the opened history encounter, the previous encounter the meters keep showing,
    /// or the current one. The same as <c>GetShownEncounter()?.GetTotals()</c>; when the host has no encounter yet,
    /// an empty snapshot whose <see cref="PluginEncounterSnapshot.EncounterKey"/> is 0.
    /// </summary>
    PluginEncounterSnapshot GetEncounter();

    /// <summary>
    /// A player's skill breakdown in the encounter <see cref="GetEncounter"/> reads, largest first.
    /// Empty when the player has no total of that kind in the encounter.
    /// Throws <see cref="ArgumentOutOfRangeException"/> for a value that is not a <see cref="PluginCombatKind"/>.
    /// </summary>
    IReadOnlyList<PluginSkillTotals> GetSkillTotals(long characterId, PluginCombatKind kind);

    /// <summary>
    /// The encounter <see cref="GetEncounter"/> reads (the opened history encounter, the previous encounter the meters keep showing,
    /// or the current one), as an object that keeps reading that encounter. <c>null</c> when the host has no encounter yet.
    /// <see cref="ShownEncounterChanged"/> is raised when it changes.
    /// </summary>
    IPluginEncounterRecord? GetShownEncounter();

    /// <summary>
    /// The current live encounter, also while a history encounter is open and while the meters keep showing the previous encounter.
    /// <c>null</c> when the host has no encounter yet. <see cref="EncounterStarted"/> is raised when the host creates the next one.
    /// </summary>
    IPluginEncounterRecord? GetLiveEncounter();

    /// <summary>
    /// The host's aggregation settings (the "Aggregation" section of the general settings).
    /// </summary>
    PluginAggregationSettings AggregationSettings { get; }

    /// <summary>
    /// Raised after <see cref="AggregationSettings"/> has changed.
    /// </summary>
    event EventHandler? AggregationSettingsChanged;

    /// <summary>
    /// Raised after the host has created a new encounter: on a map change (possibly several times for one map change),
    /// at the start of a dungeon, at a split of the progress-based reset (a boss room or a wipe), on a manual reset,
    /// when a benchmark starts or stops, on logout, and at a split requested by a plugin (<see cref="IPluginEncounterControlService"/>).
    /// The combat clock usually starts later; read <see cref="PluginEncounterSnapshot.StartUtc"/>.
    /// </summary>
    event EventHandler? EncounterStarted;

    /// <summary>
    /// Raised when the host closes an encounter that has records and creates the next one, with the closed encounter's final result.
    /// Not raised for the last encounter when the app exits.
    /// Keep what you need from the result: <see cref="GetEncounter"/> reads the closed encounter only while the meters keep showing it.
    /// The host saves the encounter right after this; <see cref="EncounterSaved"/> follows with its number in the saved history,
    /// and <see cref="IPluginHistoryService.LoadAsync"/> reads it again.
    /// Data stored with <see cref="IPluginHistoryService.TrySetEncounterData"/> in this handler may or may not be saved with the encounter;
    /// store the final values with <see cref="IPluginHistoryService.SetSavedEncounterDataAsync"/> after <see cref="EncounterSaved"/>.
    /// </summary>
    event EventHandler<PluginEncounterResult>? EncounterClosed;

    /// <summary>
    /// Raised once for each encounter the host saves to the history, after <see cref="EncounterClosed"/> of the same encounter
    /// and before the next <see cref="EncounterStarted"/>. An encounter without records is not saved, so nothing is raised for it.
    /// Not raised for the last encounter when the app exits: the host stops the plugins right after saving it.
    /// </summary>
    event EventHandler<PluginEncounterSaved>? EncounterSaved;

    /// <summary>
    /// Raised after the encounter <see cref="GetEncounter"/> and <see cref="GetShownEncounter"/> read has changed:
    /// a history encounter was opened (by the user or with <see cref="IPluginHistoryService.OpenAsync"/>), the host returned to the live encounter
    /// (also automatically), the host started showing a new encounter, or it stopped showing the previous encounter it kept.
    /// Opening a history encounter reads it again, so opening the same one again also raises it.
    /// Not raised when the host creates a new encounter while the meters keep showing the previous one, and not when opening failed.
    /// </summary>
    event EventHandler? ShownEncounterChanged;

    /// <summary>
    /// Raised after the player has moved to another map or channel. Not raised for the first map after the app starts or after logging in again.
    /// <see cref="Players"/> and <see cref="Entities"/> are rebuilt for the new map.
    /// </summary>
    event EventHandler? MapEntered;

    /// <summary>
    /// Raised after the player has returned to the login screen. The host has cleared what it received for the character.
    /// </summary>
    event EventHandler? LoggedOut;
}
