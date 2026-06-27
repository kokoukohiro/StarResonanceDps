using System.Globalization;
using StarResonanceDps.App.Localization;
using StarResonanceDps.PluginSdk;

namespace StarResonanceDps.App.Services;

internal sealed class PluginLocalizationService : IPluginLocalizationService
{
    private PluginLocalizationService()
    {
        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;
    }

    public static PluginLocalizationService Instance { get; } = new();

    public CultureInfo CurrentCulture => LocalizationManager.Instance.CurrentCulture;

    public event EventHandler? CultureChanged;

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        CultureChanged?.Invoke(this, EventArgs.Empty);
    }
}
