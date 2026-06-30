namespace StarResonanceDps.App.Config;

public sealed class AppConfig
{
    public WindowBounds? StartUpState { get; set; }

    public SettingsConfig Settings { get; set; } = AppConfigDefaults.CreateSettings();

    public ColorPickerConfig ColorPicker { get; set; } = AppConfigDefaults.CreateColorPicker();
}

public sealed class WindowBounds
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

public sealed class ColorPickerConfig
{
    public List<string> RecentColors { get; set; } = [];

    public ColorPickerConfig Clone()
    {
        return new ColorPickerConfig
        {
            RecentColors = RecentColors is null ? [] : [.. RecentColors]
        };
    }
}

public sealed class SettingsConfig
{
    public int LanguageIndex { get; set; }
    public int NumberDisplayFormatIndex { get; set; }
    public int WindowColorIndex { get; set; }
    public List<string> WindowColors { get; set; } = AppConfigDefaults.CreateDefaultWindowColors();

    public SettingsConfig Clone()
    {
        return new SettingsConfig
        {
            LanguageIndex = LanguageIndex,
            NumberDisplayFormatIndex = NumberDisplayFormatIndex,
            WindowColorIndex = WindowColorIndex,
            WindowColors = WindowColors is null ? AppConfigDefaults.CreateDefaultWindowColors() : [.. WindowColors]
        };
    }
}
