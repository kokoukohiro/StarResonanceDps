namespace StarResonanceDps.PluginSdk;

/// <summary>
/// Displays a host-styled informational message for a plugin.
/// </summary>
public interface IPluginMessageService
{
    /// <summary>
    /// Displays a message with a primary summary.
    /// </summary>
    void Show(string title, string message);

    /// <summary>
    /// Displays a message with a primary summary and optional detailed content.
    /// </summary>
    void Show(string title, string message, string? detail);
}
