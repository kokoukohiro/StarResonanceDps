using System.Windows;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Services;

namespace StarResonanceDps.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        ThemeManager.Instance.ApplyGlobalTheme(ConfigManager.Instance.GetSettingsSnapshot());
        base.OnStartup(e);
    }
}
