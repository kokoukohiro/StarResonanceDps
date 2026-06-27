namespace StarResonanceDps.PluginSdk;

/// <summary>
/// Displays a host-styled informational message for a plugin.
/// </summary>
public interface IPluginMessageService
{
    void Show(string title, string message);
}
