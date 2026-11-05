using System.Windows;

namespace StarResonanceDps.PluginSdk;

/// <summary>
/// Implemented by plugins that open their own window from the plugin list.
/// The entry type implements this interface next to <see cref="IStarResonancePlugin"/>.
/// </summary>
public interface IPluginWindowProvider
{
    /// <summary>
    /// Creates the content of a new plugin window.
    /// When the returned element implements <see cref="IDisposable"/>, the host disposes it after the window closes;
    /// release subscriptions to host events there.
    /// </summary>
    FrameworkElement CreateContent();

    /// <summary>
    /// The initial dimensions of the plugin window, or <c>null</c> to use the host defaults.
    /// </summary>
    PluginWindowOptions? GetWindowOptions() => null;
}
