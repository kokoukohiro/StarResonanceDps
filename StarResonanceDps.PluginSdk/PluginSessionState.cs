namespace StarResonanceDps.PluginSdk;

/// <summary>
/// The connection and map state the host has received from the game.
/// Values the host has not received yet are <c>null</c>.
/// </summary>
/// <param name="SelfCharacterId">The local player's character ID.</param>
/// <param name="SceneId">The current scene (map) ID. Use <see cref="IPluginNameService.GetSceneName"/> for its name.</param>
/// <param name="SeasonId">The current season ID.</param>
/// <param name="DungeonState">Whether the player is in a dungeon, and whether the dungeon has finished.</param>
/// <param name="IsMidLaunch">
/// The capture started while the game was already running and the host has not received the full state yet.
/// Some values stay unknown until the next map change.
/// </param>
public sealed record PluginSessionState(
    long? SelfCharacterId,
    int? SceneId,
    int? SeasonId,
    PluginDungeonState DungeonState,
    bool IsMidLaunch)
{
    /// <summary>
    /// The UUID of the local player's target enemy (the enemy the host's Target Info widget shows): the enemy the local player last used a skill on.
    /// <c>null</c> when there is none. It becomes <c>null</c> when that enemy leaves the surroundings, and comes back when the local player next uses a skill on it.
    /// A skill without a target does not change it. The game's lock-on and other players' targets are not sent by the game.
    /// </summary>
    public long? SelfTargetEntityUuid { get; init; }

    /// <summary>The state of the last entry of <see cref="DungeonFlowChanges"/>, or <c>null</c> when it is empty.</summary>
    public PluginDungeonFlowState? DungeonFlowState { get; init; }

    /// <summary>
    /// The dungeon progress states received since entering the current map, oldest first. A state received again in a row is not added.
    /// It becomes empty when changing maps and on logout, but not when the capture restarts (the same as <see cref="DungeonState"/>).
    /// </summary>
    public IReadOnlyList<PluginDungeonFlowChange> DungeonFlowChanges { get; init; } = Array.Empty<PluginDungeonFlowChange>();

    /// <summary>
    /// The game's dungeon timer of the current dungeon, or <c>null</c> when the dungeon has sent none
    /// (some dungeons send no timer) and outside dungeons. Cleared at the end of the dungeon, on a map change and on logout.
    /// </summary>
    public PluginDungeonTimer? DungeonTimer { get; init; }
}

/// <summary>
/// The game's dungeon timer (<see cref="PluginSessionState.DungeonTimer"/>): the values the game sent.
/// The game stops the timer at some points of a dungeon, for example from entering a boss room until the boss fight starts, and when the boss is defeated.
/// </summary>
/// <param name="StartServerUnixSeconds">The start time the game sent, in Unix seconds of the server's clock.</param>
/// <param name="StartUtc">
/// <see cref="StartServerUnixSeconds"/> on this PC's clock (UTC), corrected by the difference between this PC's clock and the server's;
/// <c>null</c> until the host has received the server's time.
/// </param>
/// <param name="LimitSeconds">The time limit the game sent, in seconds.</param>
/// <param name="CountsDown"><c>true</c> when the game counts the time down to the limit, <c>false</c> when it counts up.</param>
/// <param name="PausedTotalSeconds">The total seconds the timer has been stopped, counting only the stops that have ended.</param>
/// <param name="PausedAtSeconds">
/// While the timer is stopped, the time it shows (seconds since the start, not counting earlier stops); <c>null</c> while it runs.
/// </param>
public sealed record PluginDungeonTimer(
    long StartServerUnixSeconds,
    DateTime? StartUtc,
    int LimitSeconds,
    bool CountsDown,
    int PausedTotalSeconds,
    int? PausedAtSeconds);

/// <summary>
/// The dungeon state of the current map.
/// </summary>
public enum PluginDungeonState
{
    /// <summary>Not in a dungeon (the open world, a town or the home).</summary>
    NotInDungeon = 0,

    /// <summary>In a dungeon that has not finished.</summary>
    InDungeon,

    /// <summary>The dungeon has finished (the end, the result screen or the vote).</summary>
    Finished
}

/// <summary>
/// A dungeon progress state, the game's value as it is.
/// </summary>
public enum PluginDungeonFlowState
{
    /// <summary>The game's initial value.</summary>
    None = 0,

    /// <summary>The dungeon map has been entered.</summary>
    Active,

    /// <summary>Before the start.</summary>
    Ready,

    /// <summary>The dungeon has started.</summary>
    Playing,

    /// <summary>The dungeon has ended.</summary>
    End,

    /// <summary>The result screen.</summary>
    Settlement,

    /// <summary>The vote.</summary>
    Vote
}

/// <summary>
/// One change of the dungeon progress state.
/// </summary>
/// <param name="State">The new state.</param>
/// <param name="ArrivalUtc">When the host received it (UTC), on the same clock as <see cref="PluginPacketMessage.ArrivalTimeUtc"/>.</param>
public sealed record PluginDungeonFlowChange(PluginDungeonFlowState State, DateTime ArrivalUtc);
