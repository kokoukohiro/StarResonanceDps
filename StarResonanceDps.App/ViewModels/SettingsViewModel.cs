using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Media;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Services;

namespace StarResonanceDps.App.ViewModels;

public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly ConfigManager _configManager = ConfigManager.Instance;
    private SettingsConfig _lastSavedSettings;

    [ObservableProperty]
    private int _networkAdapterIndex;

    [ObservableProperty]
    private int _languageIndex;

    [ObservableProperty]
    private int _numberDisplayFormatIndex;

    public SettingsViewModel()
    {
        WindowColors = new ColorPaletteViewModel(AppConfigDefaults.CreateDefaultWindowColors(), AppConfigDefaults.MaxPaletteColorCount);
        TextColors = new ColorPaletteViewModel(AppConfigDefaults.CreateDefaultTextColors(), AppConfigDefaults.MaxPaletteColorCount);
        WindowColors.PaletteChanged += (_, _) => OnPropertyChanged(nameof(HasUnsavedChanges));
        TextColors.PaletteChanged += (_, _) => OnPropertyChanged(nameof(HasUnsavedChanges));

        var settings = _configManager.GetSettingsSnapshot();
        _lastSavedSettings = settings.Clone();
        LoadFromSettings(settings);
    }

    public ColorPaletteViewModel WindowColors { get; }

    public ColorPaletteViewModel TextColors { get; }

    public bool HasUnsavedChanges => !SettingsEquals(CreateSettings(), _lastSavedSettings);

    [RelayCommand]
    private void Save()
    {
        SaveSettings();
    }

    [RelayCommand]
    private void Reset()
    {
        ResetToDefaults();
    }

    public void SaveSettings()
    {
        var settings = CreateSettings();
        _configManager.SaveSettings(settings);
        _lastSavedSettings = settings.Clone();
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    public void ResetToDefaults()
    {
        LoadFromSettings(AppConfigDefaults.CreateSettings());
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    public Color GetSelectedWindowColor()
    {
        return WindowColors.SelectedColor;
    }

    public Color GetSelectedTextColor()
    {
        return TextColors.SelectedColor;
    }

    public void ApplyWindowColor(Color color)
    {
        WindowColors.AddOrSelect(color);
        TextColors.AddOrSelect(ColorUtilities.GetReadableTextColor(color));
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    public void ApplyTextColor(Color color)
    {
        TextColors.AddOrSelect(color);
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private SettingsConfig CreateSettings()
    {
        var settings = new SettingsConfig
        {
            NetworkAdapterIndex = NetworkAdapterIndex,
            LanguageIndex = LanguageIndex,
            NumberDisplayFormatIndex = NumberDisplayFormatIndex,
            WindowColorIndex = WindowColors.SelectedIndex,
            TextColorIndex = TextColors.SelectedIndex,
            WindowColors = [.. WindowColors.GetHexColors()],
            TextColors = [.. TextColors.GetHexColors()]
        };

        AppConfigDefaults.NormalizeSettings(settings);
        return settings;
    }

    private void LoadFromSettings(SettingsConfig settings)
    {
        AppConfigDefaults.NormalizeSettings(settings);

        NetworkAdapterIndex = settings.NetworkAdapterIndex;
        LanguageIndex = settings.LanguageIndex;
        NumberDisplayFormatIndex = settings.NumberDisplayFormatIndex;
        WindowColors.Load(settings.WindowColors, settings.WindowColorIndex);
        TextColors.Load(settings.TextColors, settings.TextColorIndex);
    }

    private static bool SettingsEquals(SettingsConfig left, SettingsConfig right)
    {
        AppConfigDefaults.NormalizeSettings(left);
        AppConfigDefaults.NormalizeSettings(right);

        return left.NetworkAdapterIndex == right.NetworkAdapterIndex
            && left.LanguageIndex == right.LanguageIndex
            && left.NumberDisplayFormatIndex == right.NumberDisplayFormatIndex
            && left.WindowColorIndex == right.WindowColorIndex
            && left.TextColorIndex == right.TextColorIndex
            && left.WindowColors.SequenceEqual(right.WindowColors, StringComparer.OrdinalIgnoreCase)
            && left.TextColors.SequenceEqual(right.TextColors, StringComparer.OrdinalIgnoreCase);
    }

    partial void OnNetworkAdapterIndexChanged(int value)
    {
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    partial void OnLanguageIndexChanged(int value)
    {
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    partial void OnNumberDisplayFormatIndexChanged(int value)
    {
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }
}
