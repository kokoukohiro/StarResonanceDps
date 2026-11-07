using System;

namespace StarResonanceDps.PluginSdk;

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
public sealed class PluginRegistrationAttribute : Attribute
{
    public PluginRegistrationAttribute(Type entryType, int apiVersion)
    {
        EntryType = entryType ?? throw new ArgumentNullException(nameof(entryType));
        ApiVersion = apiVersion;
    }

    public Type EntryType { get; }

    public int ApiVersion { get; }
}
