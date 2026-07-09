using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;

namespace StarResonanceDps.App.ViewModels.WidgetSettings;

public sealed partial class MetricTimelineWidgetSettingsViewModel : ObservableObject, IDisposable
{
    private readonly ObservableCollection<MetricTimelineAggregationIntervalOption> _availableAggregationIntervals = [];
    private MetricTimelineWidgetSettingsConfig _lastSaved;
    private bool _isLoading;

    [ObservableProperty]
    private int _aggregationIntervalSeconds = WidgetConfigDefaults.DefaultMetricTimelineAggregationIntervalSeconds;

    public MetricTimelineWidgetSettingsViewModel(MetricTimelineWidgetSettingsConfig? config)
    {
        AvailableAggregationIntervals = new ReadOnlyObservableCollection<MetricTimelineAggregationIntervalOption>(_availableAggregationIntervals);
        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;

        RebuildAggregationIntervals();
        _lastSaved = WidgetConfigDefaults.CloneNormalizedMetricTimeline(config);
        Load(_lastSaved);
    }

    public event Action<MetricTimelineWidgetSettingsConfig>? PreviewChanged;

    public ReadOnlyObservableCollection<MetricTimelineAggregationIntervalOption> AvailableAggregationIntervals { get; }

    public bool HasUnsavedChanges => !SettingsEqual(CreateConfig(), _lastSaved);

    public void Dispose()
    {
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;
    }

    public MetricTimelineWidgetSettingsConfig CreateConfig()
    {
        return WidgetConfigDefaults.CloneNormalizedMetricTimeline(new MetricTimelineWidgetSettingsConfig
        {
            AggregationIntervalSeconds = AggregationIntervalSeconds
        });
    }

    public void ResetToDefaults()
    {
        Load(WidgetConfigDefaults.CreateMetricTimelineSettings());
        OnPropertyChanged(nameof(HasUnsavedChanges));
        RaisePreviewChanged();
    }

    public void RestoreSavedPreview()
    {
        PreviewChanged?.Invoke(_lastSaved.Clone());
    }

    public void MarkSaved(MetricTimelineWidgetSettingsConfig config)
    {
        _lastSaved = WidgetConfigDefaults.CloneNormalizedMetricTimeline(config);
        Load(_lastSaved);
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private void Load(MetricTimelineWidgetSettingsConfig? config)
    {
        var normalized = WidgetConfigDefaults.CloneNormalizedMetricTimeline(config);

        _isLoading = true;
        try
        {
            AggregationIntervalSeconds = normalized.AggregationIntervalSeconds;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void RebuildAggregationIntervals()
    {
        _availableAggregationIntervals.Clear();
        foreach (var seconds in WidgetConfigDefaults.MetricTimelineAggregationIntervals)
        {
            _availableAggregationIntervals.Add(new MetricTimelineAggregationIntervalOption(
                seconds,
                LocalizationManager.Instance.Format("Settings_Timeline_AggregationInterval_Option", seconds)));
        }
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        RebuildAggregationIntervals();
    }

    private void RaisePreviewChanged()
    {
        PreviewChanged?.Invoke(CreateConfig());
    }

    private static bool SettingsEqual(
        MetricTimelineWidgetSettingsConfig left,
        MetricTimelineWidgetSettingsConfig right)
    {
        return left.AggregationIntervalSeconds == right.AggregationIntervalSeconds;
    }

    partial void OnAggregationIntervalSecondsChanged(int value)
    {
        if (_isLoading)
        {
            return;
        }

        OnPropertyChanged(nameof(HasUnsavedChanges));
        RaisePreviewChanged();
    }
}

public sealed class MetricTimelineAggregationIntervalOption
{
    public MetricTimelineAggregationIntervalOption(int seconds, string displayName)
    {
        Seconds = seconds;
        DisplayName = displayName;
    }

    public int Seconds { get; }

    public string DisplayName { get; }
}
