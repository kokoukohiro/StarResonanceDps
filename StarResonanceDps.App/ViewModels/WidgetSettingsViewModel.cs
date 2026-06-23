using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Media;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.ViewModels.WidgetSettings;
using StarResonanceDps.App.Models.Widgets;

namespace StarResonanceDps.App.ViewModels;

public sealed partial class WidgetSettingsViewModel : ViewModelBase, IDisposable
{
    private readonly WidgetStateManager _stateManager = WidgetStateManager.Instance;
    private readonly WidgetKind _kind;
    private readonly string _displayNameResourceKey;
    private WidgetThemeConfig _lastSavedTheme;
    private bool _isLoadingTheme;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private double _windowOpacity = 50;

    public WidgetSettingsViewModel(WidgetKind kind, string displayNameResourceKey)
    {
        _kind = kind;
        _displayNameResourceKey = displayNameResourceKey;
        DisplayName = LocalizationManager.Instance.GetString(displayNameResourceKey);
        WindowColors = new ColorPaletteViewModel(
            WidgetConfigDefaults.CreateDefaultWindowColors(),
            WidgetConfigDefaults.MaxPaletteColorCount);
        WindowColors.PaletteChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasUnsavedChanges));

            if (!_isLoadingTheme)
            {
                RaiseThemePreviewChanged();
            }
        };

        var config = _stateManager.GetWidgetSnapshot(kind);
        _lastSavedTheme = WidgetConfigDefaults.CloneNormalizedTheme(config.Theme);
        LoadFromTheme(config.Theme, raisePreview: false);

        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;

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

    public event Action<WidgetThemeConfig>? ThemePreviewChanged;

    public void Dispose()
    {
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        DisplayName = LocalizationManager.Instance.GetString(_displayNameResourceKey);
        OnPropertyChanged(nameof(WindowTitle));
    }

    public string WindowTitle => LocalizationManager.Instance.Format("Window_WidgetSettings_Title", LocalizationManager.Instance.GetString(_displayNameResourceKey));

    public bool IsMeterWidget => IsMeterWidgetKind(_kind);

    public ColorPaletteViewModel WindowColors { get; }

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
        LoadFromTheme(WidgetConfigDefaults.CreateTheme(), raisePreview: true);
        MeterSettings?.ResetToDefaults();
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    public void RestoreSavedThemePreview()
    {
        ThemePreviewChanged?.Invoke(_lastSavedTheme.Clone());
    }

    public Color GetSelectedWindowColor()
    {
        return WindowColors.SelectedColor;
    }

    public void ApplyWindowColor(Color color)
    {
        WindowColors.AddOrSelect(color);
    }

    private WidgetThemeConfig CreateTheme()
    {
        var theme = new WidgetThemeConfig
        {
            WindowColorIndex = WindowColors.SelectedIndex,
            WindowOpacity = Math.Clamp(
                (int)Math.Round(WindowOpacity, MidpointRounding.AwayFromZero),
                WidgetConfigDefaults.MinWindowOpacity,
                WidgetConfigDefaults.MaxWindowOpacity),
            WindowColors = [.. WindowColors.GetHexColors()]
        };

        WidgetConfigDefaults.NormalizeTheme(theme);
        return theme;
    }

    private void LoadFromTheme(WidgetThemeConfig theme, bool raisePreview)
    {
        var normalized = WidgetConfigDefaults.CloneNormalizedTheme(theme);

        _isLoadingTheme = true;
        try
        {
            WindowColors.Load(normalized.WindowColors, normalized.WindowColorIndex);
            WindowOpacity = normalized.WindowOpacity;
        }
        finally
        {
            _isLoadingTheme = false;
        }

        if (raisePreview)
        {
            RaiseThemePreviewChanged();
        }
    }

    private void RaiseThemePreviewChanged()
    {
        ThemePreviewChanged?.Invoke(CreateTheme());
    }

    private static bool ThemeEquals(WidgetThemeConfig left, WidgetThemeConfig right)
    {
        WidgetConfigDefaults.NormalizeTheme(left);
        WidgetConfigDefaults.NormalizeTheme(right);

        return left.WindowColorIndex == right.WindowColorIndex
            && left.WindowOpacity == right.WindowOpacity
            && left.WindowColors.SequenceEqual(right.WindowColors, StringComparer.OrdinalIgnoreCase);
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

        if (!_isLoadingTheme)
        {
            RaiseThemePreviewChanged();
        }
    }
}
