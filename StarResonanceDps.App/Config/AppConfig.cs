namespace StarResonanceDps.App.Config;

public sealed class AppConfig
{
    public WindowBounds? StartUpState { get; set; }

    public SettingsConfig Settings { get; set; } = AppConfigDefaults.CreateSettings();
}

public sealed class WindowBounds
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

public sealed class SettingsConfig
{
    public int NetworkAdapterIndex { get; set; }
    public int LanguageIndex { get; set; }
    public int NumberDisplayFormatIndex { get; set; }
    public int WindowColorIndex { get; set; }
    public int TextColorIndex { get; set; }

    public SettingsConfig Clone()
    {
        return new SettingsConfig
        {
            NetworkAdapterIndex = NetworkAdapterIndex,
            LanguageIndex = LanguageIndex,
            NumberDisplayFormatIndex = NumberDisplayFormatIndex,
            WindowColorIndex = WindowColorIndex,
            TextColorIndex = TextColorIndex
        };
    }
}
