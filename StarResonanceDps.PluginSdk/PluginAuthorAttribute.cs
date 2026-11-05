namespace StarResonanceDps.PluginSdk;

/// <summary>
/// The author shown in the plugin list. Required.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
public sealed class PluginAuthorAttribute : Attribute
{
    public PluginAuthorAttribute(string author)
    {
        Author = author ?? string.Empty;
    }

    public string Author { get; }
}
