namespace StarResonanceDps.PluginSdk;

/// <summary>
/// Provides game state that only the host can receive from the game connection.
/// Values are read-only snapshots identified by game IDs.
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
}
