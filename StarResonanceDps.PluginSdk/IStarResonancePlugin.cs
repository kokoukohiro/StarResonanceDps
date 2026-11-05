namespace StarResonanceDps.PluginSdk;

/// <summary>
/// The entry type of a plugin. The host creates it once when the app starts, calls <see cref="Initialize"/>,
/// and calls <see cref="Shutdown"/> when the app exits or when the host stops the plugin.
/// What the plugin adds to the app is declared by also implementing <see cref="IPluginWindowProvider"/>,
/// <see cref="IPluginWidgetProvider"/> and <see cref="IPluginNotificationProvider"/>. A plugin that implements none of them runs in the background only.
/// </summary>
public interface IStarResonancePlugin
{
    void Initialize(IPluginContext context);

    void Shutdown();
}
