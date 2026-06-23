using System.Windows;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Services;

namespace StarResonanceDps.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        var settings = ConfigManager.Instance.GetSettingsSnapshot();
        LocalizationManager.Instance.ApplyLanguageIndex(settings.LanguageIndex);
        ThemeManager.Instance.ApplyGlobalTheme(settings);
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        PluginManager.Instance.ShutdownAll();
        base.OnExit(e);
    }
}
