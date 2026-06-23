namespace StarResonanceDps.PluginSdk;

public sealed class PluginDescriptor
{
    public PluginDescriptor(string id, int apiVersion)
    {
        Id = id;
        ApiVersion = apiVersion;
    }

    public string Id { get; }

    public int ApiVersion { get; }
}
