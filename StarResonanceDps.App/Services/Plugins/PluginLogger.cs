using Serilog;
using StarResonanceDps.PluginSdk;

namespace StarResonanceDps.App.Services;

internal sealed class PluginLogger : IPluginLogger
{
    private readonly string _pluginId;

    public PluginLogger(string pluginId)
    {
        _pluginId = pluginId;
    }

    public void Info(string message)
    {
        Log.Information("[Plugin:{PluginId}] {Message}", _pluginId, message);
    }

    public void Warning(string message)
    {
        Log.Warning("[Plugin:{PluginId}] {Message}", _pluginId, message);
    }

    public void Error(string message, Exception? exception = null)
    {
        Log.Error(exception, "[Plugin:{PluginId}] {Message}", _pluginId, message);
    }
}
