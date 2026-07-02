using System.Windows;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.Services;
using StarResonanceDps.Core.Logging;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        ManagerTraceOutput.Configure();
        var settings = ConfigManager.Instance.GetSettingsSnapshot();
        LocalizationManager.Instance.ApplyLanguageIndex(settings.LanguageIndex);
        ThemeManager.Instance.ApplyGlobalTheme(settings);
        NetworkAdapterSession.Instance.Initialize();
        base.OnStartup(e);
        CombatRuntimeHost.Instance.Initialize();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        CombatRuntimeHost.Instance.Shutdown();
        PluginManager.Instance.ShutdownAll();
        base.OnExit(e);
    }
}
