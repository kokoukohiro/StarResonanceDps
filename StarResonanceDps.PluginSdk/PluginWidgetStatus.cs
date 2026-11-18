namespace StarResonanceDps.PluginSdk;

/// <summary>
/// What a plugin widget cannot show (<see cref="IPluginWidgetContext.SetStatusReasons"/>). The window shows the reasons with the status circle
/// next to the title and its tooltip, the same as the host's widgets: green with no reason, red with <see cref="MapOrRelogin"/> or <see cref="Unreadable"/>,
/// yellow with the others.
/// </summary>
[Flags]
public enum PluginWidgetStatusReasons
{
    /// <summary>Nothing is missing.</summary>
    None = 0,

    /// <summary>Missing until the player moves to another map or logs in again.</summary>
    MapOrRelogin = 1,

    /// <summary>Missing until the player opens the party screen in the game.</summary>
    PartyScreen = 2,

    /// <summary>Missing until the player clicks the other player in the game.</summary>
    PlayerClick = 4,

    /// <summary>Missing until the player comes near the other player again.</summary>
    PlayerApproach = 8,

    /// <summary>Missing until the player comes near the enemy again.</summary>
    EnemyApproach = 16,

    /// <summary>The saved data could not be read.</summary>
    Unreadable = 32,
}

/// <summary>What the host checks by itself for the status circle of a plugin widget (<see cref="PluginWidgetDefinition.StatusRule"/>).</summary>
public enum PluginWidgetStatusRule
{
    /// <summary>The host checks nothing. Only <see cref="IPluginWidgetContext.SetStatusReasons"/> decides the circle.</summary>
    None = 0,

    /// <summary>
    /// For a widget that shows the encounter the host shows (live or from the history), like the Timeline:
    /// while live, the host adds <see cref="PluginWidgetStatusReasons.MapOrRelogin"/> when it started reading the game connection in the middle;
    /// while an encounter from the history is open, it adds <see cref="PluginWidgetStatusReasons.Unreadable"/> when the encounter could not be read.
    /// </summary>
    Encounter = 1,

    /// <summary>
    /// For a widget that shows only live data: the host adds <see cref="PluginWidgetStatusReasons.MapOrRelogin"/>
    /// when it started reading the game connection in the middle.
    /// </summary>
    Live = 2,
}
