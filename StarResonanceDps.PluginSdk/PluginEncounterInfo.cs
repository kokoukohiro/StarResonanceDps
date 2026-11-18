namespace StarResonanceDps.PluginSdk;

/// <summary>
/// The values of an encounter other than its totals (<see cref="IPluginEncounterRecord.Info"/> and <see cref="PluginHistoryEntry.Info"/>).
///
/// <para>
/// What the host records in an encounter: nothing while <see cref="PluginEncounterSnapshot.IsAggregationStopped"/> is <c>true</c>;
/// healing, hate and break only from the start of the combat clock (<see cref="PluginEncounterSnapshot.StartUtc"/>).
/// In a benchmark encounter (<see cref="BenchmarkSeconds"/> above 0), only the events from <see cref="PluginEncounterSnapshot.StartUtc"/>
/// until <see cref="BenchmarkSeconds"/> later, and among them only the local player's events and the events that belong to no player
/// (casts, announcements and actions of enemies). With <see cref="BenchmarkFirstTargetOnly"/>, damage, hate and break count only against
/// the first enemy the local player damaged (<see cref="PluginEncounterSnapshot.BenchmarkFirstTargetEntityUuid"/>),
/// and hate and break before that enemy is decided do not count.
/// </para>
/// </summary>
/// <param name="CreatedUtc">
/// When the host created the encounter (UTC). The combat clock usually starts later; with a dungeon clock it can start slightly earlier
/// (<see cref="PluginEncounterSnapshot.StartUtc"/>).
/// The saved history is sorted by this time, newest first.
/// </param>
/// <param name="SceneId">
/// The scene (map) ID, or <c>null</c> when unknown. Use <see cref="IPluginNameService.GetSceneName"/> with <see cref="DungeonDifficulty"/> for its name.
/// </param>
/// <param name="DungeonDifficulty">The difficulty (master stage) the dungeon sent, or 0.</param>
/// <param name="ChannelLine">The channel number, or <c>null</c> in a place without channels.</param>
/// <param name="Server">
/// The game server. The host writes it when it saves the encounter, so it is <see cref="PluginGameServer.Unknown"/> for a live encounter
/// before that; read <see cref="IPluginGameDataService.CurrentServer"/> instead.
/// </param>
/// <param name="SeasonId">
/// The season ID, or <c>null</c> when unknown. The host writes it when it saves the encounter, so it is <c>null</c> for a live encounter
/// before that; read <see cref="PluginSessionState.SeasonId"/> instead.
/// </param>
/// <param name="IsWipe">The host closed the encounter at a party wipe (the general setting "Auto Reset by Progress").</param>
/// <param name="BenchmarkSeconds">
/// The benchmark duration in seconds when this is a benchmark encounter, also while the benchmark waits for the first event; otherwise 0.
/// </param>
/// <param name="BenchmarkFirstTargetOnly">
/// Whether the benchmark counts only the first enemy attacked; <c>false</c> when this is not a benchmark encounter.
/// <c>null</c> for an encounter read from the saved history, because the host does not save it.
/// </param>
/// <param name="AutoPauseSeconds">
/// The seconds of the general setting "Auto Pause" when the host created the encounter: the combat clock pauses this long after the last combat event.
/// 0 when it does not pause, in a benchmark encounter, and in an encounter with a dungeon clock (<see cref="PluginEncounterSnapshot.ClockOrigin"/>).
/// </param>
public sealed record PluginEncounterInfo(
    DateTime CreatedUtc,
    int? SceneId,
    int DungeonDifficulty,
    int? ChannelLine,
    PluginGameServer Server,
    int? SeasonId,
    bool IsWipe,
    int BenchmarkSeconds,
    bool? BenchmarkFirstTargetOnly,
    int AutoPauseSeconds);
