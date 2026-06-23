namespace StarResonanceDps.PluginSdk;

public interface IPluginLogger
{
    void Info(string message);

    void Warning(string message);

    void Error(string message, Exception? exception = null);
}
