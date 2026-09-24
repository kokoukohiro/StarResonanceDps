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

        if (WidgetConfigDefaults.SupportsBuffCardSettings(kind))
        {
            BuffCardSettings = new BuffCardWidgetSettingsViewModel(config.BuffCard);
            BuffCardSettings.PropertyChanged += BuffCardSettings_PropertyChanged;
            BuffCardSettings.PreviewChanged += BuffCardSettings_PreviewChanged;
        }

        if (WidgetConfigDefaults.SupportsTakenDamageLogSettings(kind))
        {
            TakenDamageLogSettings = new TakenDamageLogWidgetSettingsViewModel(config.TakenDamageLog);
            TakenDamageLogSettings.PropertyChanged += TakenDamageLogSettings_PropertyChanged;
            TakenDamageLogSettings.PreviewChanged += TakenDamageLogSettings_PreviewChanged;
        }

        if (WidgetConfigDefaults.SupportsBuffListSettings(kind))
        {
            BuffListSettings = new BuffListWidgetSettingsViewModel(kind, config.BuffList);
            BuffListSettings.PropertyChanged += BuffListSettings_PropertyChanged;
            BuffListSettings.PreviewChanged += BuffListSettings_PreviewChanged;
        }

        if (WidgetConfigDefaults.SupportsElementColorSettings(kind))
        {
            ElementColorSettings = new ElementColorWidgetSettingsViewModel(kind, config.ElementColor);
            ElementColorSettings.PropertyChanged += ElementColorSettings_PropertyChanged;
            ElementColorSettings.PreviewChanged += ElementColorSettings_PreviewChanged;
        }

        if (WidgetConfigDefaults.SupportsSkillDetailSettings(kind))
        {
            SkillDetailSettings = new SkillDetailWidgetSettingsViewModel(config.SkillDetail);
            SkillDetailSettings.PropertyChanged += SkillDetailSettings_PropertyChanged;
            SkillDetailSettings.PreviewChanged += SkillDetailSettings_PreviewChanged;
        }

        if (WidgetConfigDefaults.SupportsPlayerStatusSettings(kind))
        {
            PlayerStatusSettings = new PlayerStatusWidgetSettingsViewModel(config.PlayerStatus);
            PlayerStatusSettings.PropertyChanged += PlayerStatusSettings_PropertyChanged;
            PlayerStatusSettings.PreviewChanged += PlayerStatusSettings_PreviewChanged;
        }

        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;
    }

    public event Action<WidgetThemeConfig>? ThemePreviewChanged;

    public event Action<MeterWidgetSettingsConfig>? MeterPreviewChanged;

    public event Action<MetricTimelineWidgetSettingsConfig>? MetricTimelinePreviewChanged;

    public event Action<BuffCardWidgetSettingsConfig>? BuffCardPreviewChanged;

    public event Action<TakenDamageLogWidgetSettingsConfig>? TakenDamageLogPreviewChanged;

    public event Action<BuffListWidgetSettingsConfig>? BuffListPreviewChanged;

    public event Action<ElementColorWidgetSettingsConfig>? ElementColorPreviewChanged;

    public event Action<SkillDetailWidgetSettingsConfig>? SkillDetailPreviewChanged;

    public event Action<PlayerStatusWidgetSettingsConfig>? PlayerStatusPreviewChanged;

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

        if (BuffCardSettings is not null)
        {
            BuffCardSettings.PropertyChanged -= BuffCardSettings_PropertyChanged;
            BuffCardSettings.PreviewChanged -= BuffCardSettings_PreviewChanged;
            BuffCardSettings.Dispose();
        }

        if (TakenDamageLogSettings is not null)
        {
            TakenDamageLogSettings.PropertyChanged -= TakenDamageLogSettings_PropertyChanged;
            TakenDamageLogSettings.PreviewChanged -= TakenDamageLogSettings_PreviewChanged;
            TakenDamageLogSettings.Dispose();
        }

        if (BuffListSettings is not null)
        {
            BuffListSettings.PropertyChanged -= BuffListSettings_PropertyChanged;
            BuffListSettings.PreviewChanged -= BuffListSettings_PreviewChanged;
            BuffListSettings.Dispose();
        }

        if (ElementColorSettings is not null)
        {
            ElementColorSettings.PropertyChanged -= ElementColorSettings_PropertyChanged;
            ElementColorSettings.PreviewChanged -= ElementColorSettings_PreviewChanged;
            ElementColorSettings.Dispose();
        }

        if (SkillDetailSettings is not null)
        {
            SkillDetailSettings.PropertyChanged -= SkillDetailSettings_PropertyChanged;
            SkillDetailSettings.PreviewChanged -= SkillDetailSettings_PreviewChanged;
            SkillDetailSettings.Dispose();
        }

        if (PlayerStatusSettings is not null)
        {
            PlayerStatusSettings.PropertyChanged -= PlayerStatusSettings_PropertyChanged;
            PlayerStatusSettings.PreviewChanged -= PlayerStatusSettings_PreviewChanged;
            PlayerStatusSettings.Dispose();
        }
    }

    private void SkillDetailSettings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SkillDetailWidgetSettingsViewModel.HasUnsavedChanges))
        {
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }
    }

    private void SkillDetailSettings_PreviewChanged(SkillDetailWidgetSettingsConfig config)
    {
        SkillDetailPreviewChanged?.Invoke(config);
    }

    private void PlayerStatusSettings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlayerStatusWidgetSettingsViewModel.HasUnsavedChanges))
        {
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }
    }

    private void PlayerStatusSettings_PreviewChanged(PlayerStatusWidgetSettingsConfig config)
    {
        PlayerStatusPreviewChanged?.Invoke(config);
    }

    private void TakenDamageLogSettings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TakenDamageLogWidgetSettingsViewModel.HasUnsavedChanges))
        {
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }
    }

    private void TakenDamageLogSettings_PreviewChanged(TakenDamageLogWidgetSettingsConfig config)
    {
        TakenDamageLogPreviewChanged?.Invoke(config);
    }

    private void BuffListSettings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BuffListWidgetSettingsViewModel.HasUnsavedChanges))
        {
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }
    }

    private void BuffListSettings_PreviewChanged(BuffListWidgetSettingsConfig config)
    {
        BuffListPreviewChanged?.Invoke(config);
    }

    private void ElementColorSettings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ElementColorWidgetSettingsViewModel.HasUnsavedChanges))
        {
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }
    }

    private void ElementColorSettings_PreviewChanged(ElementColorWidgetSettingsConfig config)
    {
        ElementColorPreviewChanged?.Invoke(config);
    }

    private void BuffCardSettings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BuffCardWidgetSettingsViewModel.HasUnsavedChanges))
        {
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }
    }

    private void BuffCardSettings_PreviewChanged(BuffCardWidgetSettingsConfig config)
    {
        BuffCardPreviewChanged?.Invoke(config);
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

    public BuffCardWidgetSettingsViewModel? BuffCardSettings { get; }

    public TakenDamageLogWidgetSettingsViewModel? TakenDamageLogSettings { get; }

    public BuffListWidgetSettingsViewModel? BuffListSettings { get; }

    public ElementColorWidgetSettingsViewModel? ElementColorSettings { get; }

    public SkillDetailWidgetSettingsViewModel? SkillDetailSettings { get; }

    public PlayerStatusWidgetSettingsViewModel? PlayerStatusSettings { get; }

    public bool HasMeterSettings => MeterSettings is not null;

    /// <summary>属性カラーの節を出すか。表示の節とは別なので <c>HasDisplaySettings</c> には入れない。</summary>
    public bool HasElementColorSettings => ElementColorSettings is not null;

    public bool HasMeterDisplaySettings => HasMeterSettings;

    public bool HasMetricTimelineDisplaySettings => MetricTimelineSettings is not null;

    public bool HasBuffCardDisplaySettings => BuffCardSettings is not null;

    public bool HasTakenDamageLogDisplaySettings => TakenDamageLogSettings is not null;

    public bool HasBuffListDisplaySettings => BuffListSettings is not null;

    public bool HasSkillDetailDisplaySettings => SkillDetailSettings is not null;

    public bool HasPlayerStatusDisplaySettings => PlayerStatusSettings is not null;

    public bool HasDisplaySettings => HasMeterDisplaySettings
        || HasMetricTimelineDisplaySettings
        || HasBuffCardDisplaySettings
        || HasTakenDamageLogDisplaySettings
        || HasBuffListDisplaySettings
        || HasSkillDetailDisplaySettings
        || HasPlayerStatusDisplaySettings;

    public bool HasUnsavedChanges => !ThemeEquals(CreateTheme(), _lastSavedTheme)
        || (MeterSettings?.HasUnsavedChanges ?? false)
        || (MetricTimelineSettings?.HasUnsavedChanges ?? false)
        || (BuffCardSettings?.HasUnsavedChanges ?? false)
        || (TakenDamageLogSettings?.HasUnsavedChanges ?? false)
        || (BuffListSettings?.HasUnsavedChanges ?? false)
        || (ElementColorSettings?.HasUnsavedChanges ?? false)
        || (SkillDetailSettings?.HasUnsavedChanges ?? false)
        || (PlayerStatusSettings?.HasUnsavedChanges ?? false);

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

        if (BuffCardSettings is not null)
        {
            config.BuffCard = BuffCardSettings.CreateConfig();
        }

        if (TakenDamageLogSettings is not null)
        {
            config.TakenDamageLog = TakenDamageLogSettings.CreateConfig();
        }

        if (BuffListSettings is not null)
        {
            config.BuffList = BuffListSettings.CreateConfig();
        }

        if (ElementColorSettings is not null)
        {
            config.ElementColor = ElementColorSettings.CreateConfig();
        }

        if (PlayerStatusSettings is not null)
        {
            config.PlayerStatus = PlayerStatusSettings.CreateConfig();
        }

        if (SkillDetailSettings is not null)
        {
            config.SkillDetail = SkillDetailSettings.CreateConfig();
        }

        _stateManager.SaveWidget(_kind, config);

        _lastSavedTheme = theme.Clone();
        MeterSettings?.MarkSaved(config.Meter);
        MetricTimelineSettings?.MarkSaved(config.MetricTimeline);
        BuffCardSettings?.MarkSaved(config.BuffCard);
        TakenDamageLogSettings?.MarkSaved(config.TakenDamageLog);
        BuffListSettings?.MarkSaved(config.BuffList);
        ElementColorSettings?.MarkSaved(config.ElementColor);
        SkillDetailSettings?.MarkSaved(config.SkillDetail);
        PlayerStatusSettings?.MarkSaved(config.PlayerStatus);
        OnPropertyChanged(nameof(HasUnsavedChanges));
        return config.Clone();
    }

    public void ResetToDefaults()
    {
        LoadFromTheme(WidgetConfigDefaults.CreateTheme(), raisePreview: true);
        MeterSettings?.ResetToDefaults();
        MetricTimelineSettings?.ResetToDefaults();
        BuffCardSettings?.ResetToDefaults();
        TakenDamageLogSettings?.ResetToDefaults();
        BuffListSettings?.ResetToDefaults();
        ElementColorSettings?.ResetToDefaults();
        SkillDetailSettings?.ResetToDefaults();
        PlayerStatusSettings?.ResetToDefaults();
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    public void RestoreSavedPreviews()
    {
        ThemePreviewChanged?.Invoke(_lastSavedTheme.Clone());
        MeterSettings?.RestoreSavedPreview();
        MetricTimelineSettings?.RestoreSavedPreview();
        BuffCardSettings?.RestoreSavedPreview();
        TakenDamageLogSettings?.RestoreSavedPreview();
        BuffListSettings?.RestoreSavedPreview();
        ElementColorSettings?.RestoreSavedPreview();
        SkillDetailSettings?.RestoreSavedPreview();
        PlayerStatusSettings?.RestoreSavedPreview();
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

    /// <summary>ピン留め中にヘッダーを隠す。</summary>
    [ObservableProperty]
    private bool _hideHeaderWhenInactive = true;

    /// <summary>ピン留め中フッターを隠す。</summary>
    [ObservableProperty]
    private bool _hideFooterWhenInactive;

    /// <summary>ウィンドウの枠とフォーカスの設定。<b>全ウィジェットで出す。</b></summary>
    public bool HasWindowSettings => true;

    /// <summary>「ピン留め中フッターを隠す」を出すか。フッターを持つウィジェットだけ。</summary>
    public bool HasFooterSetting => WidgetConfigDefaults.HasFooter(_kind);

    /// <summary>スイッチの右に出す ON / OFF。</summary>
    public string HideHeaderWhenInactiveStateText => GetSwitchStateText(HideHeaderWhenInactive);

    public string HideFooterWhenInactiveStateText => GetSwitchStateText(HideFooterWhenInactive);

    private static string GetSwitchStateText(bool isOn)
    {
        return LocalizationManager.Instance.GetString(isOn ? "Settings_Switch_On" : "Settings_Switch_Off");
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
            BackgroundImageAverageColorSourcePath = BackgroundImageAverageColorSourcePath,
            HideHeaderWhenInactive = HideHeaderWhenInactive,
            HideFooterWhenInactive = HideFooterWhenInactive
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
            HideHeaderWhenInactive = normalized.HideHeaderWhenInactive;
            HideFooterWhenInactive = normalized.HideFooterWhenInactive;
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
            && left.HideHeaderWhenInactive == right.HideHeaderWhenInactive
            && left.HideFooterWhenInactive == right.HideFooterWhenInactive
            && left.WindowColors.SequenceEqual(right.WindowColors, StringComparer.OrdinalIgnoreCase);
    }

    partial void OnHideHeaderWhenInactiveChanged(bool value)
    {
        OnPropertyChanged(nameof(HideHeaderWhenInactiveStateText));
        NotifyWindowDisplaySettingChanged();
    }

    partial void OnHideFooterWhenInactiveChanged(bool value)
    {
        OnPropertyChanged(nameof(HideFooterWhenInactiveStateText));
        NotifyWindowDisplaySettingChanged();
    }

    /// <summary>スイッチを触った瞬間にプレビューへ反映する。既存のテーマ設定と同じ経路。</summary>
    private void NotifyWindowDisplaySettingChanged()
    {
        OnPropertyChanged(nameof(HasUnsavedChanges));

        if (!_isLoadingTheme)
        {
            RaiseThemePreviewChanged();
        }
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
