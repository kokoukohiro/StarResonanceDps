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
    bool IsMidLaunch);

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
