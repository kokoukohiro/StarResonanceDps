using System.Globalization;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.ViewModels;

public sealed partial class PlayerMetricSummaryWidgetViewModel : PlayerWidgetWindowViewModel, IDisposable
{
    private readonly MeterSnapshotKind _kind;
    private readonly ConfigManager _configManager = ConfigManager.Instance;
    private readonly DispatcherTimer _refreshTimer;
    private bool _isBenchmarkUiFrozen;
    private bool _isDisposed;

    [ObservableProperty]
    private PlayerMetricSummaryEntry? _summary;

    [ObservableProperty]
    private string _noDataText = string.Empty;

    [ObservableProperty]
    private bool _hasMetricData;

    public PlayerMetricSummaryWidgetViewModel(
        WidgetListItemViewModel playerWidget,
        long? requestedCharacterId,
        PlayerRosterEntry? initialPlayer,
        MeterSnapshotKind kind)
        : base(playerWidget, requestedCharacterId)
    {
        _kind = kind;
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _refreshTimer.Tick += RefreshTimer_Tick;
        _configManager.SettingsPreviewChanged += ConfigManager_SettingsPreviewChanged;
        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;
        InitializePlayer(initialPlayer);
        Refresh();
        _refreshTimer.Start();
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _refreshTimer.Stop();
        _refreshTimer.Tick -= RefreshTimer_Tick;
        _configManager.SettingsPreviewChanged -= ConfigManager_SettingsPreviewChanged;
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;
    }

    protected override void OnSelectedPlayerChanged(PlayerRosterEntry? player)
    {
        Refresh();
    }

    private void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        Refresh();
    }

    private void ConfigManager_SettingsPreviewChanged(object? sender, EventArgs e)
    {
        Refresh();
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        Refresh();
    }

    private void Refresh()
    {
        if (_isDisposed)
        {
            return;
        }

        var benchmarkState = MeterSnapshotProvider.GetBenchmarkState();
        if (benchmarkState.IsCompleted && _isBenchmarkUiFrozen)
        {
            return;
        }

        _isBenchmarkUiFrozen = benchmarkState.IsCompleted;

        NoDataText = LocalizationManager.Instance.GetString("Widget_NoMetricData");

        if (SelectedCharacterId is not { } characterId)
        {
            Summary = CreateEntry(MeterSnapshotProvider.GetPlayerMetricSummary(_kind, 0), _configManager.GetSettingsSnapshot().NumberDisplayFormatIndex);
            HasMetricData = false;
            return;
        }

        var playerIdentity = MeterSnapshotProvider.GetPlayerIdentity(characterId);
        if (playerIdentity is not null)
        {
            SetHeaderText(playerIdentity.Name, playerIdentity.UserId, playerIdentity.IsNpc, playerIdentity.ProfessionId);
        }

        var numberDisplayFormatIndex = _configManager.GetSettingsSnapshot().NumberDisplayFormatIndex;
        var snapshot = MeterSnapshotProvider.GetPlayerMetricSummary(_kind, characterId);
        Summary = CreateEntry(snapshot, numberDisplayFormatIndex);
        HasMetricData = snapshot.TotalValue > 0UL
            || snapshot.ExtraTotalValue > 0UL
            || snapshot.HitsCount > 0UL
            || snapshot.CastsCount > 0UL;
    }

    private PlayerMetricSummaryEntry CreateEntry(
        PlayerMetricSummarySnapshot snapshot,
        int numberDisplayFormatIndex)
    {
        var localization = LocalizationManager.Instance;
        var isDamage = _kind == MeterSnapshotKind.Damage;
        var valueNameKey = isDamage ? "Metric_Damage" : "Metric_Healing";
        var perSecondKey = isDamage ? "Metric_Dps" : "Metric_Hps";
        var extraTotalKey = isDamage ? "Metric_TotalShieldBreak" : "Metric_TotalOverheal";
        var normalValueKey = isDamage ? "Metric_TotalNormalDamage" : "Metric_TotalNormalHealing";
        var critValueKey = isDamage ? "Metric_TotalCritDamage" : "Metric_TotalCritHealing";
        var luckyValueKey = isDamage ? "Metric_TotalLuckyDamage" : "Metric_TotalLuckyHealing";
        var averageValueKey = isDamage ? "Metric_TotalAverageDamage" : "Metric_TotalAverageHealing";

        var valueLines = new List<string>
        {
            FormatLine("Metric_TotalValue", localization.GetString(valueNameKey), FormatValue(snapshot.TotalValue, numberDisplayFormatIndex)),
            FormatLine("Metric_TotalPerSecond", localization.GetString(perSecondKey),
                string.Format(
                    CultureInfo.CurrentCulture,
                    "{0} ({1})",
                    FormatValue(snapshot.ValuePerSecondActive, numberDisplayFormatIndex),
                    FormatValue(snapshot.ValuePerSecond, numberDisplayFormatIndex))),
            FormatLine(extraTotalKey, FormatValue(snapshot.ExtraTotalValue, numberDisplayFormatIndex)),
            FormatLine("Metric_TotalHits", snapshot.HitsCount.ToString(CultureInfo.CurrentCulture))
        };

        var rateLines = new List<string>
        {
            FormatLine("Metric_TotalCritRate", FormatPercent(snapshot.CritRate)),
            FormatLine("Metric_TotalLuckyRate", FormatPercent(snapshot.LuckyRate)),
            FormatLine("Metric_TotalCrits", snapshot.CritCount.ToString(CultureInfo.CurrentCulture))
        };

        if (snapshot.ShowsImmuneCount)
        {
            rateLines.Add(FormatLine("Metric_TotalImmunes", snapshot.ImmuneCount.ToString(CultureInfo.CurrentCulture)));
        }

        var distributionLines = new List<string>
        {
            FormatLine(normalValueKey, FormatValue(snapshot.NormalValue, numberDisplayFormatIndex)),
            FormatLine(critValueKey, FormatValue(snapshot.CritValue, numberDisplayFormatIndex)),
            FormatLine(luckyValueKey, FormatValue(snapshot.LuckyValue, numberDisplayFormatIndex))
        };

        var castLines = new List<string>
        {
            FormatLine("Metric_TotalLuckyStrikes", snapshot.LuckyCount.ToString(CultureInfo.CurrentCulture)),
            FormatLine(averageValueKey, FormatValue(snapshot.AverageValue, numberDisplayFormatIndex)),
            FormatLine("Metric_TotalCasts", snapshot.CastsCount.ToString(CultureInfo.CurrentCulture))
        };

        if (snapshot.CastsPerMinute is { } castsPerMinute
            && snapshot.CastsPerSecond is { } castsPerSecond)
        {
            castLines.Add(FormatLine(
                "Metric_CastsPerMinute",
                string.Format(
                    CultureInfo.CurrentCulture,
                    "{0} ({1})",
                    FormatDecimal(castsPerMinute),
                    FormatDecimal(castsPerSecond))));
        }

        return new PlayerMetricSummaryEntry(valueLines, rateLines, distributionLines, castLines);
    }

    private static string FormatLine(string labelKey, string value)
    {
        return string.Format(
            CultureInfo.CurrentCulture,
            LocalizationManager.Instance.GetString("Metric_LabelValueFormat"),
            LocalizationManager.Instance.GetString(labelKey),
            value);
    }

    private static string FormatLine(string labelKey, string labelValue, string value)
    {
        return string.Format(
            CultureInfo.CurrentCulture,
            LocalizationManager.Instance.GetString(labelKey),
            labelValue,
            value);
    }

    private static string FormatValue(double value, int numberDisplayFormatIndex)
    {
        return MeterNumberFormatter.Format(value, numberDisplayFormatIndex);
    }

    private static string FormatPercent(double value)
    {
        return value.ToString("F2", CultureInfo.CurrentCulture) + "%";
    }

    private static string FormatDecimal(double value)
    {
        return value.ToString("F2", CultureInfo.CurrentCulture);
    }
}
