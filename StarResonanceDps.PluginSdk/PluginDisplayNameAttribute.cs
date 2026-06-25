using System;

namespace StarResonanceDps.PluginSdk;

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
public sealed class PluginDisplayNameAttribute : Attribute
{
    public PluginDisplayNameAttribute(string cultureName, string displayName)
    {
        CultureName = cultureName ?? string.Empty;
        DisplayName = displayName ?? string.Empty;
    }

    public string CultureName { get; }

    public string DisplayName { get; }
}
