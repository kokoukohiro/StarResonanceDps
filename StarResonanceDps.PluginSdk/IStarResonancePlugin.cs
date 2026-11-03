using System.Windows;

namespace StarResonanceDps.PluginSdk;

public interface IStarResonancePlugin
{
    void Initialize(IPluginContext context);

    /// <summary>
    /// Creates the content of a new plugin window.
    /// When the returned element implements <see cref="IDisposable"/>, the host disposes it after the window closes;
    /// release subscriptions to host events there.
    /// </summary>
    FrameworkElement CreateContent();

    void Shutdown();
}
