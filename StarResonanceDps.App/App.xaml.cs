using System.Windows;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.Services;

namespace StarResonanceDps.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        var settings = ConfigManager.Instance.GetSettingsSnapshot();
        LocalizationManager.Instance.ApplyLanguageIndex(settings.LanguageIndex);
        ThemeManager.Instance.ApplyGlobalTheme(settings);
        NetworkAdapterSession.Instance.Initialize();
        base.OnStartup(e);
        PlayerRosterCaptureService.Instance.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        PlayerRosterCaptureService.Instance.Stop();
        PluginManager.Instance.ShutdownAll();
        base.OnExit(e);
    }
}
