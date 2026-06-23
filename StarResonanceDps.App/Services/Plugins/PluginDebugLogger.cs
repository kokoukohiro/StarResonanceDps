using System.Diagnostics;
using StarResonanceDps.PluginSdk;

namespace StarResonanceDps.App.Services;

internal sealed class PluginDebugLogger : IPluginLogger
{
    private readonly string _pluginId;

    public PluginDebugLogger(string pluginId)
    {
        _pluginId = pluginId;
    }

    public void Info(string message)
    {
        Write("INFO", message, null);
    }

    public void Warning(string message)
    {
        Write("WARN", message, null);
    }

    public void Error(string message, Exception? exception = null)
    {
        Write("ERROR", message, exception);
    }

    private void Write(string level, string message, Exception? exception)
    {
        var suffix = exception is null ? string.Empty : $"{Environment.NewLine}{exception}";
        Debug.WriteLine($"[{level}] [Plugin:{_pluginId}] {message}{suffix}");
    }
}
