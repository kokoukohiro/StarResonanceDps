namespace StarResonanceDps.PluginSdk;

/// <summary>
/// Optional extension point for plugins that need their own initial window dimensions.
/// Plugins that do not implement this interface use the host window defaults.
/// </summary>
public interface IPluginWindowOptionsProvider
{
    PluginWindowOptions GetWindowOptions();
}
