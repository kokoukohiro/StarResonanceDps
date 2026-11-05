namespace StarResonanceDps.PluginSdk;

/// <summary>
/// Implemented by plugins that add widgets (the small windows over the game) to the host's widget list.
/// The entry type implements this interface next to <see cref="IStarResonancePlugin"/>.
///
/// <para>
/// The host calls <see cref="GetWidgetDefinitions"/> once, right after <see cref="IStarResonancePlugin.Initialize"/> returns,
/// and adds one card per definition after the host's own cards. Each widget opens one window at a time.
/// The host owns the card, the window frame (semi-transparent black by default), the title, the common settings (window, theme),
/// the saved position, size and state, and the settings window with its preview. The plugin gives the body, the footer when the definition has one,
/// header buttons, and the definitions of its own settings.
/// </para>
///
/// <para>
/// When a definition is invalid (see <see cref="PluginWidgetDefinition"/>), the host stops the plugin and shows it as an error in the plugin list.
/// When the plugin is stopped, the host closes its widget windows and removes its cards; the saved settings are kept.
/// </para>
/// </summary>
public interface IPluginWidgetProvider
{
    /// <summary>The widgets this plugin adds, in the order their cards are listed.</summary>
    IReadOnlyList<PluginWidgetDefinition> GetWidgetDefinitions();

    /// <summary>
    /// Creates the content of a widget window. Called on the UI thread each time the window opens.
    /// When <see cref="PluginWidgetContent.Body"/> or <see cref="PluginWidgetContent.Footer"/> implements <see cref="IDisposable"/>,
    /// the host disposes it after the window closes; release subscriptions to host events there.
    /// </summary>
    PluginWidgetContent CreateWidget(string widgetId, IPluginWidgetContext context);
}
