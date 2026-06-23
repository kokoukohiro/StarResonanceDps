using System.IO;

namespace StarResonanceDps.App.Config;

public static class AppDataPaths
{
    public static string BaseDirectory => AppContext.BaseDirectory;

    public static string DataDirectory => Path.Combine(BaseDirectory, "Data");

    public static string AppSettingsPath => Path.Combine(DataDirectory, "appsettings.json");

    public static string WidgetStatePath => Path.Combine(DataDirectory, "widgetstate.json");

    public static string PluginDataDirectory => Path.Combine(DataDirectory, "PluginData");

    public static string GetLegacyAppSettingsPath()
    {
        return Path.Combine(BaseDirectory, "appsettings.json");
    }

    public static string GetLegacyWidgetStatePath()
    {
        return Path.Combine(BaseDirectory, "widgetstate.json");
    }
}
