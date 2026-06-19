using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Media;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Services;
using StarResonanceDps.App.ViewModels.WidgetSettings;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.ViewModels;

public sealed partial class WidgetSettingsViewModel : ViewModelBase
{
    private readonly WidgetStateManager _stateManager = WidgetStateManager.Instance;
    private readonly WidgetKind _kind;
    private WidgetThemeConfig _lastSavedTheme;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private double _windowOpacity = 100;

    public WidgetSettingsViewModel(WidgetKind kind, string displayName)
    {
        _kind = kind;
        DisplayName = displayName;
        WindowColors = new ColorPaletteViewModel(WidgetConfigDefaults.CreateDefaultWindowColors(), WidgetConfigDefaults.MaxPaletteColorCount);
        TextColors = new ColorPaletteViewModel(WidgetConfigDefaults.CreateDefaultTextColors(), WidgetConfigDefaults.MaxPaletteColorCount);
        WindowColors.PaletteChanged += (_, _) => OnPropertyChanged(nameof(HasUnsavedChanges));
        TextColors.PaletteChanged += (_, _) => OnPropertyChanged(nameof(HasUnsavedChanges));

        var config = _stateManager.GetWidgetSnapshot(kind);
        _lastSavedTheme = WidgetConfigDefaults.CloneNormalizedTheme(config.Theme);
        LoadFromTheme(config.Theme);

        if (IsMeterWidgetKind(kind))
        {
            MeterSettings = new MeterWidgetSettingsViewModel(config.Meter);
            MeterSettings.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(MeterWidgetSettingsViewModel.HasUnsavedChanges))
                {
                    OnPropertyChanged(nameof(HasUnsavedChanges));
                }
            };
        }
    }

    public string WindowTitle => $"{DisplayName}の設定";

    public bool IsMeterWidget => IsMeterWidgetKind(_kind);

    public ColorPaletteViewModel WindowColors { get; }

    public ColorPaletteViewModel TextColors { get; }

    public MeterWidgetSettingsViewModel? MeterSettings { get; }

    public bool HasUnsavedChanges => !ThemeEquals(CreateTheme(), _lastSavedTheme)
        || (MeterSettings?.HasUnsavedChanges ?? false);

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

    public WidgetThemeConfig SaveSettings()
    {
        var config = _stateManager.GetWidgetSnapshot(_kind);
        var theme = CreateTheme();
        config.Theme = theme;

        if (MeterSettings is not null)
        {
            config.Meter = MeterSettings.CreateConfig();
        }

        _stateManager.SaveWidget(_kind, config);

        _lastSavedTheme = theme.Clone();
        MeterSettings?.MarkSaved(config.Meter);
        OnPropertyChanged(nameof(HasUnsavedChanges));
        return theme;
    }

    public void ResetToDefaults()
    {
        LoadFromTheme(WidgetConfigDefaults.CreateTheme());
        MeterSettings?.ResetToDefaults();
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

    private WidgetThemeConfig CreateTheme()
    {
        var theme = new WidgetThemeConfig
        {
            WindowColorIndex = WindowColors.SelectedIndex,
            TextColorIndex = TextColors.SelectedIndex,
            WindowOpacity = Math.Clamp(
                (int)Math.Round(WindowOpacity, MidpointRounding.AwayFromZero),
                WidgetConfigDefaults.MinWindowOpacity,
                WidgetConfigDefaults.MaxWindowOpacity),
            WindowColors = [.. WindowColors.GetHexColors()],
            TextColors = [.. TextColors.GetHexColors()]
        };

        WidgetConfigDefaults.NormalizeTheme(theme);
        return theme;
    }

    private void LoadFromTheme(WidgetThemeConfig theme)
    {
        var normalized = WidgetConfigDefaults.CloneNormalizedTheme(theme);

        WindowColors.Load(normalized.WindowColors, normalized.WindowColorIndex);
        TextColors.Load(normalized.TextColors, normalized.TextColorIndex);
        WindowOpacity = normalized.WindowOpacity;
    }

    private static bool ThemeEquals(WidgetThemeConfig left, WidgetThemeConfig right)
    {
        WidgetConfigDefaults.NormalizeTheme(left);
        WidgetConfigDefaults.NormalizeTheme(right);

        return left.WindowColorIndex == right.WindowColorIndex
            && left.TextColorIndex == right.TextColorIndex
            && left.WindowOpacity == right.WindowOpacity
            && left.WindowColors.SequenceEqual(right.WindowColors, StringComparer.OrdinalIgnoreCase)
            && left.TextColors.SequenceEqual(right.TextColors, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsMeterWidgetKind(WidgetKind kind)
    {
        return kind is WidgetKind.DpsMeter
            or WidgetKind.HpsMeter
            or WidgetKind.DtpsMeter;
    }

    partial void OnWindowOpacityChanged(double value)
    {
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }
}
