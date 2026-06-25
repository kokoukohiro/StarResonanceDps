using System;

namespace StarResonanceDps.PluginSdk;

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
public sealed class PluginRegistrationAttribute : Attribute
{
    public PluginRegistrationAttribute(Type entryType, string id, int apiVersion)
    {
        EntryType = entryType ?? throw new ArgumentNullException(nameof(entryType));
        Id = id ?? string.Empty;
        ApiVersion = apiVersion;
    }

    public Type EntryType { get; }

    public string Id { get; }

    public int ApiVersion { get; }
}
