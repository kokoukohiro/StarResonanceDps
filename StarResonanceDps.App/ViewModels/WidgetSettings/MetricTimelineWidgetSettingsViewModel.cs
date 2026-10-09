using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;

namespace StarResonanceDps.App.ViewModels.WidgetSettings;

/// <summary>
/// 推移グラフ(DPS / HPS)の設定。表示設定(描画間隔・横軸の長さ)と、線の色(グラフカラー)。
/// グラフカラーの形はメーターのクラスカラーと同じ(クラスごとの色・フィルター。不透明度は持たない)で、アイコンの欄には折れ線の見本を出す。
/// 行の並びと既定は読み替え先のメーター(<see cref="WidgetConfigDefaults.GetMetricTimelineColorDefaultSource"/>)と同じ。
/// </summary>
public sealed partial class MetricTimelineWidgetSettingsViewModel : ObservableObject, IDisposable
{
    private readonly WidgetKind _kind;
    private readonly WidgetKind _colorDefaultSource;
    private readonly ObservableCollection<MetricTimelineSecondsOption> _availableAggregationIntervals = [];
    private readonly ObservableCollection<MetricTimelineSecondsOption> _availableVisibleSeconds = [];
    private readonly Dictionary<string, MeterClassColorItemViewModel> _classColorItemsByKey = new(StringComparer.OrdinalIgnoreCase);
    private MetricTimelineWidgetSettingsConfig _lastSaved;
    private bool _isLoading;

    [ObservableProperty]
    private int _aggregationIntervalSeconds = WidgetConfigDefaults.DefaultMetricTimelineAggregationIntervalSeconds;

    [ObservableProperty]
    private int _visibleSeconds = WidgetConfigDefaults.DefaultMetricTimelineVisibleSeconds;

    [ObservableProperty]
    private bool _classColorFilterEnabled;

    [ObservableProperty]
    private double _classColorFilterStrength = WidgetConfigDefaults.DefaultClassColorFilterStrength;

    public MetricTimelineWidgetSettingsViewModel(WidgetKind kind, MetricTimelineWidgetSettingsConfig? config)
    {
        _kind = kind;
        _colorDefaultSource = WidgetConfigDefaults.GetMetricTimelineColorDefaultSource(kind);
        AvailableAggregationIntervals = new ReadOnlyObservableCollection<MetricTimelineSecondsOption>(_availableAggregationIntervals);
        AvailableVisibleSeconds = new ReadOnlyObservableCollection<MetricTimelineSecondsOption>(_availableVisibleSeconds);

        var items = new ObservableCollection<MeterClassColorItemViewModel>();
        var classColorKeys = WidgetConfigDefaults.GetClassColorKeys(_colorDefaultSource);
        for (var index = 0; index < classColorKeys.Count; index++)
        {
            var key = classColorKeys[index];
            var colors = new ColorPaletteViewModel(
                WidgetConfigDefaults.CreateDefaultClassColors(_colorDefaultSource, key),
                WidgetConfigDefaults.MaxPaletteColorCount);
            colors.PaletteChanged += Colors_PaletteChanged;

            var item = new MeterClassColorItemViewModel(key, colors, index == classColorKeys.Count - 1);
            _classColorItemsByKey.Add(key, item);
            items.Add(item);
        }

        ClassColorItems = new ReadOnlyObservableCollection<MeterClassColorItemViewModel>(items);

        ClassColorFilterColors = new ColorPaletteViewModel(
            WidgetConfigDefaults.CreateDefaultClassColorFilterColors(_colorDefaultSource),
            WidgetConfigDefaults.MaxPaletteColorCount);
        ClassColorFilterColors.PaletteChanged += Colors_PaletteChanged;

        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;

        RebuildSecondsOptions();
        _lastSaved = WidgetConfigDefaults.CloneNormalizedMetricTimeline(_kind, config);
        Load(_lastSaved);
    }

    public event Action<MetricTimelineWidgetSettingsConfig>? PreviewChanged;

    public ReadOnlyObservableCollection<MetricTimelineSecondsOption> AvailableAggregationIntervals { get; }

    /// <summary>横軸の長さの選択肢。</summary>
    public ReadOnlyObservableCollection<MetricTimelineSecondsOption> AvailableVisibleSeconds { get; }

    /// <summary>グラフカラーの行。クラスごとに色見本(最大5枠)と、選んでいる枠を持つ。</summary>
    public ReadOnlyObservableCollection<MeterClassColorItemViewModel> ClassColorItems { get; }

    /// <summary>フィルター色のパレット。クラスカラーと同じ枠を使う。</summary>
    public ColorPaletteViewModel ClassColorFilterColors { get; }

    public string GraphColorSectionTitle => LocalizationManager.Instance.GetString("Settings_Section_GraphColors_Title");

    /// <summary>フィルターの色と強さを出すか。スイッチがオフのときは隠す。</summary>
    public bool ShowsClassColorFilterOptions => ClassColorFilterEnabled;

    /// <summary>スイッチの右に出す ON / OFF。</summary>
    public string ClassColorFilterStateText => LocalizationManager.Instance.GetString(
        ClassColorFilterEnabled ? "Settings_Switch_On" : "Settings_Switch_Off");

    public bool HasUnsavedChanges => !SettingsEqual(CreateConfig(), _lastSaved);

    public void Dispose()
    {
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;

        foreach (var item in ClassColorItems)
        {
            item.Colors.PaletteChanged -= Colors_PaletteChanged;
        }

        ClassColorFilterColors.PaletteChanged -= Colors_PaletteChanged;
    }

    public MetricTimelineWidgetSettingsConfig CreateConfig()
    {
        var config = new MetricTimelineWidgetSettingsConfig
        {
            AggregationIntervalSeconds = AggregationIntervalSeconds,
            VisibleSeconds = VisibleSeconds,
            ClassColorFilterEnabled = ClassColorFilterEnabled,
            ClassColorFilterColors = [.. ClassColorFilterColors.GetHexColors()],
            ClassColorFilterColorIndex = ClassColorFilterColors.SelectedIndex,
            ClassColorFilterStrength = Math.Clamp(
                (int)Math.Round(ClassColorFilterStrength, MidpointRounding.AwayFromZero),
                WidgetConfigDefaults.MinClassColorFilterStrength,
                WidgetConfigDefaults.MaxClassColorFilterStrength),
            ClassColorIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            ClassColorPalettes = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        };

        foreach (var item in ClassColorItems)
        {
            config.ClassColorIndexes[item.Key] = item.Colors.SelectedIndex;
            config.ClassColorPalettes[item.Key] = [.. item.Colors.GetHexColors()];
        }

        return WidgetConfigDefaults.CloneNormalizedMetricTimeline(_kind, config);
    }

    public void ResetToDefaults()
    {
        Load(WidgetConfigDefaults.CreateMetricTimelineSettings(_kind));
        OnPropertyChanged(nameof(HasUnsavedChanges));
        RaisePreviewChanged();
    }

    public void RestoreSavedPreview()
    {
        PreviewChanged?.Invoke(_lastSaved.Clone());
    }

    public void MarkSaved(MetricTimelineWidgetSettingsConfig config)
    {
        _lastSaved = WidgetConfigDefaults.CloneNormalizedMetricTimeline(_kind, config);
        Load(_lastSaved);
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    public Color GetSelectedClassColor(string key)
    {
        return _classColorItemsByKey[key].Colors.SelectedColor;
    }

    public void ApplyClassColor(string key, Color color)
    {
        _classColorItemsByKey[key].Colors.AddOrSelect(color);
    }

    public Color GetSelectedClassColorFilterColor()
    {
        return ClassColorFilterColors.SelectedColor;
    }

    public void ApplyClassColorFilterColor(Color color)
    {
        ClassColorFilterColors.AddOrSelect(color);
    }

    private void Load(MetricTimelineWidgetSettingsConfig? config)
    {
        var normalized = WidgetConfigDefaults.CloneNormalizedMetricTimeline(_kind, config);

        _isLoading = true;
        try
        {
            AggregationIntervalSeconds = normalized.AggregationIntervalSeconds;
            VisibleSeconds = normalized.VisibleSeconds;

            foreach (var item in ClassColorItems)
            {
                item.Colors.Load(normalized.ClassColorPalettes[item.Key], normalized.ClassColorIndexes[item.Key]);
            }

            ClassColorFilterColors.Load(normalized.ClassColorFilterColors, normalized.ClassColorFilterColorIndex);
            ClassColorFilterEnabled = normalized.ClassColorFilterEnabled ?? false;
            ClassColorFilterStrength = normalized.ClassColorFilterStrength;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void RebuildSecondsOptions()
    {
        _availableAggregationIntervals.Clear();
        foreach (var seconds in WidgetConfigDefaults.MetricTimelineAggregationIntervals)
        {
            _availableAggregationIntervals.Add(new MetricTimelineSecondsOption(
                seconds,
                LocalizationManager.Instance.Format("Settings_Timeline_AggregationInterval_Option", seconds)));
        }

        _availableVisibleSeconds.Clear();
        foreach (var seconds in WidgetConfigDefaults.MetricTimelineVisibleSecondsChoices)
        {
            _availableVisibleSeconds.Add(new MetricTimelineSecondsOption(
                seconds,
                LocalizationManager.Instance.Format("Settings_Timeline_VisibleSeconds_Option", seconds)));
        }
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        RebuildSecondsOptions();

        foreach (var item in ClassColorItems)
        {
            item.RefreshDisplayName();
        }

        OnPropertyChanged(nameof(GraphColorSectionTitle));
        OnPropertyChanged(nameof(ClassColorFilterStateText));
    }

    private void RaisePreviewChanged()
    {
        PreviewChanged?.Invoke(CreateConfig());
    }

    private bool SettingsEqual(
        MetricTimelineWidgetSettingsConfig left,
        MetricTimelineWidgetSettingsConfig right)
    {
        if (left.AggregationIntervalSeconds != right.AggregationIntervalSeconds
            || left.VisibleSeconds != right.VisibleSeconds
            || left.ClassColorFilterEnabled != right.ClassColorFilterEnabled
            || left.ClassColorFilterColorIndex != right.ClassColorFilterColorIndex
            || left.ClassColorFilterStrength != right.ClassColorFilterStrength
            || !(left.ClassColorFilterColors ?? []).SequenceEqual(
                right.ClassColorFilterColors ?? [],
                StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        foreach (var key in WidgetConfigDefaults.GetClassColorKeys(_colorDefaultSource))
        {
            if (left.ClassColorIndexes[key] != right.ClassColorIndexes[key]
                || !left.ClassColorPalettes[key].SequenceEqual(right.ClassColorPalettes[key], StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private void Colors_PaletteChanged(object? sender, EventArgs e)
    {
        NotifyChanged();
    }

    private void NotifyChanged()
    {
        if (_isLoading)
        {
            return;
        }

        OnPropertyChanged(nameof(HasUnsavedChanges));
        RaisePreviewChanged();
    }

    partial void OnAggregationIntervalSecondsChanged(int value)
    {
        NotifyChanged();
    }

    partial void OnVisibleSecondsChanged(int value)
    {
        NotifyChanged();
    }

    partial void OnClassColorFilterEnabledChanged(bool value)
    {
        // オフの間は「フィルターカラー」「フィルターの強さ」の行ごと消す。
        OnPropertyChanged(nameof(ShowsClassColorFilterOptions));
        OnPropertyChanged(nameof(ClassColorFilterStateText));
        NotifyChanged();
    }

    partial void OnClassColorFilterStrengthChanged(double value)
    {
        NotifyChanged();
    }
}

/// <summary>秒数の選択肢(描画間隔・横軸の長さ)。</summary>
public sealed class MetricTimelineSecondsOption
{
    public MetricTimelineSecondsOption(int seconds, string displayName)
    {
        Seconds = seconds;
        DisplayName = displayName;
    }

    public int Seconds { get; }

    public string DisplayName { get; }
}
