namespace StarResonanceDps.App.Config;

public static class AppConfigDefaults
{
    public static AppConfig Create()
    {
        return new AppConfig
        {
            StartUpState = null,
            Settings = CreateSettings()
        };
    }

    public static SettingsConfig CreateSettings()
    {
        return new SettingsConfig
        {
            NetworkAdapterIndex = 0,
            LanguageIndex = 0,
            NumberDisplayFormatIndex = 0,
            WindowColorIndex = 0,
            TextColorIndex = 0
        };
    }

    public static void Normalize(AppConfig config)
    {
        config.Settings ??= CreateSettings();
        NormalizeSettings(config.Settings);
    }

    public static SettingsConfig CloneNormalizedSettings(SettingsConfig settings)
    {
        var normalized = settings.Clone();
        NormalizeSettings(normalized);
        return normalized;
    }

    public static void NormalizeSettings(SettingsConfig settings)
    {
        settings.NetworkAdapterIndex = Clamp(settings.NetworkAdapterIndex, 0, 1);
        settings.LanguageIndex = Clamp(settings.LanguageIndex, 0, 4);
        settings.NumberDisplayFormatIndex = Clamp(settings.NumberDisplayFormatIndex, 0, 1);
        settings.WindowColorIndex = Clamp(settings.WindowColorIndex, 0, 4);
        settings.TextColorIndex = Clamp(settings.TextColorIndex, 0, 4);
    }

    private static int Clamp(int value, int min, int max)
    {
        return Math.Min(Math.Max(value, min), max);
    }
}
