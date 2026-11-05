namespace StarResonanceDps.PluginSdk;

/// <summary>
/// One widget a plugin adds (<see cref="IPluginWidgetProvider"/>).
///
/// <para>
/// Localized texts follow the rules of <see cref="PluginNotificationDefinition"/>.
/// The host checks the definitions when the plugin starts and stops the plugin when any of these rules is broken:
/// <see cref="Id"/> follows the plugin ID rules (1 to 128 ASCII letters, digits, <c>.</c>, <c>-</c> or <c>_</c>) and is unique in the plugin (without case);
/// <see cref="Names"/> has at least one text; the default size is finite and above 0;
/// each section and setting is valid, and setting IDs are unique among all sections of the widget (without case).
/// </para>
/// </summary>
public sealed class PluginWidgetDefinition
{
    /// <summary>The widget ID. The user's settings, position and state are saved under this ID, so do not change it between versions.</summary>
    public required string Id { get; init; }

    /// <summary>The widget name, shown on the card and as the window title.</summary>
    public required IReadOnlyDictionary<string, string> Names { get; init; }

    /// <summary>The window width until the user resizes it. A size below the window's minimum is raised to the minimum.</summary>
    public required double DefaultWidth { get; init; }

    /// <summary>The window height until the user resizes it.</summary>
    public required double DefaultHeight { get; init; }

    /// <summary>
    /// Whether the window has a footer row. When <c>true</c>, <see cref="PluginWidgetContent.Footer"/> is required, and the settings window shows
    /// the common "hide the footer while pinned" setting; when <c>false</c>, the footer must be <c>null</c>.
    /// </summary>
    public bool HasFooter { get; init; }

    /// <summary>
    /// Whether the host scrolls the body vertically with the window's own scroll bar (as the host's lists do).
    /// When <c>false</c>, the body gets the whole content area and handles its size itself.
    /// </summary>
    public bool ScrollsVertically { get; init; }

    /// <summary>
    /// The widget's own settings sections, shown in the settings window between the window settings and the theme.
    /// Read the values with <see cref="IPluginWidgetContext.Settings"/>.
    /// </summary>
    public IReadOnlyList<PluginWidgetSettingSection> SettingSections { get; init; } = [];
}

/// <summary>A section of a widget's own settings, with its button in the settings window's navigation.</summary>
public sealed class PluginWidgetSettingSection
{
    /// <summary>The section title, shown on the navigation button and above the section (at least one text).</summary>
    public required IReadOnlyDictionary<string, string> Names { get; init; }

    /// <summary>The navigation icon: one of the icon geometries in <see cref="PluginResourceKeys.Icons"/> (for example <see cref="PluginResourceKeys.Icons.Sliders"/>).</summary>
    public required string Icon { get; init; }

    /// <summary>How the icon is drawn: <see cref="PluginResourceKeys.Icons.StrokeStyle"/> or <see cref="PluginResourceKeys.Icons.FillStyle"/>.</summary>
    public required string IconStyle { get; init; }

    /// <summary>The rows of the section, in order (at least one).</summary>
    public required IReadOnlyList<PluginSettingDefinition> Settings { get; init; }
}
