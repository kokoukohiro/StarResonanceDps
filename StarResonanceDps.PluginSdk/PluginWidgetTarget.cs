namespace StarResonanceDps.PluginSdk;

/// <summary>
/// One target a window can open for (<see cref="IPluginMultiWidgetProvider.GetWidgetTargets"/>).
///
/// <para>
/// Localized texts follow the rules of <see cref="PluginNotificationDefinition"/>.
/// The host checks the list each time it reads it and stops the plugin when any of these rules is broken:
/// <see cref="Id"/> follows the ID rules (1 to 128 ASCII letters, digits, <c>.</c>, <c>-</c> or <c>_</c>) and is unique in the list (without case);
/// <see cref="Names"/> has at least one text.
/// </para>
/// </summary>
public sealed class PluginWidgetTarget
{
    /// <summary>
    /// The target ID. It is passed to <see cref="IPluginMultiWidgetProvider.CreateWidget"/>, and open windows are saved with it.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>The target name, shown in the drop-down list and in the window title after the widget's name.</summary>
    public required IReadOnlyDictionary<string, string> Names { get; init; }

    /// <summary>
    /// Whether the card selects this target until the user chooses one, for example the local player (<see cref="PluginPlayer.IsSelf"/>).
    /// A target that appears with this value later replaces the selection unless the user has chosen one. When several targets have it, the first one is used.
    /// </summary>
    public bool IsDefault { get; init; }
}
