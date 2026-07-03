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

public sealed class ClassColorSettingsConfig
{
    public Dictionary<string, int> ClassColorIndexes { get; set; } = AppConfigDefaults.CreateDefaultClassColorIndexes();

    public Dictionary<string, List<string>> ClassColorPalettes { get; set; } = AppConfigDefaults.CreateDefaultClassColorPalettes();

    public ClassColorSettingsConfig Clone()
    {
        return new ClassColorSettingsConfig
        {
            ClassColorIndexes = ClassColorIndexes is null
                ? AppConfigDefaults.CreateDefaultClassColorIndexes()
                : new Dictionary<string, int>(ClassColorIndexes, StringComparer.OrdinalIgnoreCase),
            ClassColorPalettes = ClassColorPalettes is null
                ? AppConfigDefaults.CreateDefaultClassColorPalettes()
                : ClassColorPalettes.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value is null ? new List<string>() : new List<string>(pair.Value),
                    StringComparer.OrdinalIgnoreCase)
        };
    }
}

public sealed class SettingsConfig
{
    public int LanguageIndex { get; set; }
    public int NumberDisplayFormatIndex { get; set; }
    public int PlayerNameDisplayModeIndex { get; set; }
    public int WindowColorIndex { get; set; }
    public List<string> WindowColors { get; set; } = AppConfigDefaults.CreateDefaultWindowColors();
    public ClassColorSettingsConfig ClassColors { get; set; } = AppConfigDefaults.CreateClassColorSettings();

    public SettingsConfig Clone()
    {
        return new SettingsConfig
        {
            LanguageIndex = LanguageIndex,
            NumberDisplayFormatIndex = NumberDisplayFormatIndex,
            PlayerNameDisplayModeIndex = PlayerNameDisplayModeIndex,
            WindowColorIndex = WindowColorIndex,
            WindowColors = WindowColors is null ? AppConfigDefaults.CreateDefaultWindowColors() : [.. WindowColors],
            ClassColors = ClassColors?.Clone() ?? AppConfigDefaults.CreateClassColorSettings()
        };
    }
}
