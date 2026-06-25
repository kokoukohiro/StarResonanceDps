using System.Collections.ObjectModel;

namespace StarResonanceDps.App.Services;

public sealed class PluginInfo
{
    public PluginInfo(string id, IReadOnlyDictionary<string, string> displayNames)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(displayNames);

        Id = id;
        DisplayNames = new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(displayNames, StringComparer.OrdinalIgnoreCase));
    }

    public string Id { get; }

    public IReadOnlyDictionary<string, string> DisplayNames { get; }
}
