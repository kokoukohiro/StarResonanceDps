using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.ComponentModel;
using System.Windows.Media;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.Services;
using StarResonanceDps.App.ViewModels.WidgetSettings;

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

    [ObservableProperty]
    private string? _backgroundImagePath;

    [ObservableProperty]
    private string? _backgroundImageAverageColor;

    [ObservableProperty]
    private string? _backgroundImageAverageColorSourcePath;

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

        if (WidgetConfigDefaults.SupportsMeterSettings(kind))
        {
            MeterSettings = new MeterWidgetSettingsViewModel(kind, config.Meter);
            MeterSettings.PropertyChanged += MeterSettings_PropertyChanged;
            MeterSettings.PreviewChanged += MeterSettings_PreviewChanged;
        }

        if (WidgetConfigDefaults.SupportsMetricTimelineSettings(kind))
        {
            MetricTimelineSettings = new MetricTimelineWidgetSettingsViewModel(config.MetricTimeline);
            MetricTimelineSettings.PropertyChanged += MetricTimelineSettings_PropertyChanged;
            MetricTimelineSettings.PreviewChanged += MetricTimelineSettings_PreviewChanged;
        }

        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;
    }

    public event Action<WidgetThemeConfig>? ThemePreviewChanged;

    public event Action<MeterWidgetSettingsConfig>? MeterPreviewChanged;

    public event Action<MetricTimelineWidgetSettingsConfig>? MetricTimelinePreviewChanged;

    public void Dispose()
    {
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;

        if (MeterSettings is not null)
        {
            MeterSettings.PropertyChanged -= MeterSettings_PropertyChanged;
            MeterSettings.PreviewChanged -= MeterSettings_PreviewChanged;
            MeterSettings.Dispose();
        }

        if (MetricTimelineSettings is not null)
        {
            MetricTimelineSettings.PropertyChanged -= MetricTimelineSettings_PropertyChanged;
            MetricTimelineSettings.PreviewChanged -= MetricTimelineSettings_PreviewChanged;
            MetricTimelineSettings.Dispose();
        }
    }

    private void MeterSettings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MeterWidgetSettingsViewModel.HasUnsavedChanges))
        {
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }
    }

    private void MeterSettings_PreviewChanged(MeterWidgetSettingsConfig config)
    {
        MeterPreviewChanged?.Invoke(config);
    }

    private void MetricTimelineSettings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MetricTimelineWidgetSettingsViewModel.HasUnsavedChanges))
        {
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }
    }

    private void MetricTimelineSettings_PreviewChanged(MetricTimelineWidgetSettingsConfig config)
    {
        MetricTimelinePreviewChanged?.Invoke(config);
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        DisplayName = LocalizationManager.Instance.GetString(_displayNameResourceKey);
        OnPropertyChanged(nameof(WindowTitle));
    }

    public string WindowTitle => LocalizationManager.Instance.Format("Window_WidgetSettings_Title", LocalizationManager.Instance.GetString(_displayNameResourceKey));

    public ColorPaletteViewModel WindowColors { get; }

    public MeterWidgetSettingsViewModel? MeterSettings { get; }

    public MetricTimelineWidgetSettingsViewModel? MetricTimelineSettings { get; }

    public bool HasMeterSettings => MeterSettings is not null;

    public bool HasMeterDisplaySettings => HasMeterSettings;

    public bool HasMetricTimelineDisplaySettings => MetricTimelineSettings is not null;

    public bool HasDisplaySettings => HasMeterDisplaySettings || HasMetricTimelineDisplaySettings;

    public bool HasUnsavedChanges => !ThemeEquals(CreateTheme(), _lastSavedTheme)
        || (MeterSettings?.HasUnsavedChanges ?? false)
        || (MetricTimelineSettings?.HasUnsavedChanges ?? false);

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

    public WidgetConfig SaveSettings()
    {
        var config = _stateManager.GetWidgetSnapshot(_kind);
        var theme = CreateTheme();
        config.Theme = theme;
        if (MeterSettings is not null)
        {
            config.Meter = MeterSettings.CreateConfig();
        }

        if (MetricTimelineSettings is not null)
        {
            config.MetricTimeline = MetricTimelineSettings.CreateConfig();
        }

        _stateManager.SaveWidget(_kind, config);

        _lastSavedTheme = theme.Clone();
        MeterSettings?.MarkSaved(config.Meter);
        MetricTimelineSettings?.MarkSaved(config.MetricTimeline);
        OnPropertyChanged(nameof(HasUnsavedChanges));
        return config.Clone();
    }

    public void ResetToDefaults()
    {
        LoadFromTheme(WidgetConfigDefaults.CreateTheme(), raisePreview: true);
        MeterSettings?.ResetToDefaults();
        MetricTimelineSettings?.ResetToDefaults();
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    public void RestoreSavedPreviews()
    {
        ThemePreviewChanged?.Invoke(_lastSavedTheme.Clone());
        MeterSettings?.RestoreSavedPreview();
        MetricTimelineSettings?.RestoreSavedPreview();
    }

    public Color GetSelectedWindowColor()
    {
        return WindowColors.SelectedColor;
    }

    public void ApplyWindowColor(Color color)
    {
        var previousLoadingState = _isLoadingTheme;
        _isLoadingTheme = true;

        try
        {
            WindowColors.AddOrSelect(color);
            BackgroundImagePath = null;
            BackgroundImageAverageColor = null;
            BackgroundImageAverageColorSourcePath = null;
        }
        finally
        {
            _isLoadingTheme = previousLoadingState;
        }

        if (!previousLoadingState)
        {
            RaiseThemePreviewChanged();
        }
    }

    public void SetBackgroundImagePath(string? path)
    {
        var normalizedPath = string.IsNullOrWhiteSpace(path)
            ? null
            : path.Trim();
        var previousLoadingState = _isLoadingTheme;
        _isLoadingTheme = true;

        try
        {
            BackgroundImagePath = normalizedPath;
            BackgroundImageAverageColorSourcePath = normalizedPath;
            BackgroundImageAverageColor = normalizedPath is not null
                && BackgroundImageColorAnalyzer.TryCalculateAverageColor(normalizedPath, out var averageColor)
                    ? ColorUtilities.ToHex(averageColor)
                    : null;
        }
        finally
        {
            _isLoadingTheme = previousLoadingState;
        }

        OnPropertyChanged(nameof(HasUnsavedChanges));
        if (!previousLoadingState)
        {
            RaiseThemePreviewChanged();
        }
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
            WindowColors = [.. WindowColors.GetHexColors()],
            BackgroundImagePath = string.IsNullOrWhiteSpace(BackgroundImagePath)
                ? null
                : BackgroundImagePath.Trim(),
            BackgroundImageAverageColor = BackgroundImageAverageColor,
            BackgroundImageAverageColorSourcePath = BackgroundImageAverageColorSourcePath
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
            BackgroundImagePath = normalized.BackgroundImagePath;
            BackgroundImageAverageColor = normalized.BackgroundImageAverageColor;
            BackgroundImageAverageColorSourcePath = normalized.BackgroundImageAverageColorSourcePath;
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
            && string.Equals(left.BackgroundImagePath, right.BackgroundImagePath, StringComparison.OrdinalIgnoreCase)
            && string.Equals(left.BackgroundImageAverageColor, right.BackgroundImageAverageColor, StringComparison.OrdinalIgnoreCase)
            && string.Equals(left.BackgroundImageAverageColorSourcePath, right.BackgroundImageAverageColorSourcePath, StringComparison.OrdinalIgnoreCase)
            && left.WindowColors.SequenceEqual(right.WindowColors, StringComparer.OrdinalIgnoreCase);
    }

    partial void OnWindowOpacityChanged(double value)
    {
        OnPropertyChanged(nameof(HasUnsavedChanges));

        if (!_isLoadingTheme)
        {
            RaiseThemePreviewChanged();
        }
    }

    partial void OnBackgroundImagePathChanged(string? value)
    {
        NotifyBackgroundImageChanged();
    }

    partial void OnBackgroundImageAverageColorChanged(string? value)
    {
        NotifyBackgroundImageChanged();
    }

    partial void OnBackgroundImageAverageColorSourcePathChanged(string? value)
    {
        NotifyBackgroundImageChanged();
    }

    private void NotifyBackgroundImageChanged()
    {
        OnPropertyChanged(nameof(HasUnsavedChanges));

        if (!_isLoadingTheme)
        {
            RaiseThemePreviewChanged();
        }
    }
}
