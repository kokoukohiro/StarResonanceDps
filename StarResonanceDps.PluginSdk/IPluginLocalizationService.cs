using System.Globalization;

namespace StarResonanceDps.PluginSdk;

/// <summary>
/// Provides the host application's current UI culture and notifies plugins when it changes.
/// Plugins own their translated resources and use this service only as the culture source.
/// </summary>
public interface IPluginLocalizationService
{
    CultureInfo CurrentCulture { get; }

    event EventHandler? CultureChanged;
}
