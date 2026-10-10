using System.Globalization;
using System.Windows.Threading;
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
    private bool _isDisposed;

    /// <summary>表示する行。窓を開いたときに作り、更新では行の文字だけを入れ替える。</summary>
    public PlayerMetricSummaryEntry Summary { get; } = new();

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

        if (SelectedCharacterId is not { } characterId)
        {
            ApplySnapshot(MeterSnapshotProvider.GetPlayerMetricSummary(_kind, 0), _configManager.GetSettingsSnapshot().NumberDisplayFormatIndex);
            return;
        }

        var playerIdentity = MeterSnapshotProvider.GetPlayerIdentity(characterId);
        if (playerIdentity is not null)
        {
            SetHeaderText(playerIdentity.Name, playerIdentity.UserId, playerIdentity.IsNpc, playerIdentity.ProfessionId);
        }

        var numberDisplayFormatIndex = _configManager.GetSettingsSnapshot().NumberDisplayFormatIndex;
        var snapshot = MeterSnapshotProvider.GetPlayerMetricSummary(_kind, characterId);
        ApplySnapshot(snapshot, numberDisplayFormatIndex);
    }

    /// <summary>行の文字を組み直して入れる。行の部品は作り直さない(同じ文字なら何も起きない)。</summary>
    private void ApplySnapshot(
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

        var summary = Summary;
        summary.ValueLines[0].Show(FormatLine("Metric_TotalValue", localization.GetString(valueNameKey), FormatValue(snapshot.TotalValue, numberDisplayFormatIndex)));
        summary.ValueLines[1].Show(FormatLine("Metric_TotalPerSecond", localization.GetString(perSecondKey),
            string.Format(
                CultureInfo.CurrentCulture,
                "{0} ({1})",
                FormatValue(snapshot.ValuePerSecondActive, numberDisplayFormatIndex),
                FormatValue(snapshot.ValuePerSecond, numberDisplayFormatIndex))));
        summary.ValueLines[2].Show(FormatLine(extraTotalKey, FormatValue(snapshot.ExtraTotalValue, numberDisplayFormatIndex)));
        summary.ValueLines[3].Show(FormatLine("Metric_TotalHits", snapshot.HitsCount.ToString(CultureInfo.CurrentCulture)));

        summary.RateLines[0].Show(FormatLine("Metric_TotalCritRate", FormatPercent(snapshot.CritRate)));
        summary.RateLines[1].Show(FormatLine("Metric_TotalLuckyRate", FormatPercent(snapshot.LuckyRate)));
        summary.RateLines[2].Show(FormatLine("Metric_TotalCrits", snapshot.CritCount.ToString(CultureInfo.CurrentCulture)));
        summary.RateLines[PlayerMetricSummaryEntry.ImmuneLineIndex].Show(snapshot.ShowsImmuneCount
            ? FormatLine("Metric_TotalImmunes", snapshot.ImmuneCount.ToString(CultureInfo.CurrentCulture))
            : null);

        summary.DistributionLines[0].Show(FormatLine(normalValueKey, FormatValue(snapshot.NormalValue, numberDisplayFormatIndex)));
        summary.DistributionLines[1].Show(FormatLine(critValueKey, FormatValue(snapshot.CritValue, numberDisplayFormatIndex)));
        summary.DistributionLines[2].Show(FormatLine(luckyValueKey, FormatValue(snapshot.LuckyValue, numberDisplayFormatIndex)));

        summary.CastLines[0].Show(FormatLine("Metric_TotalLuckyStrikes", snapshot.LuckyCount.ToString(CultureInfo.CurrentCulture)));
        summary.CastLines[1].Show(FormatLine(averageValueKey, FormatValue(snapshot.AverageValue, numberDisplayFormatIndex)));
        summary.CastLines[2].Show(FormatLine("Metric_TotalCasts", snapshot.CastsCount.ToString(CultureInfo.CurrentCulture)));
        summary.CastLines[PlayerMetricSummaryEntry.CastsPerMinuteLineIndex].Show(
            snapshot.CastsPerMinute is { } castsPerMinute && snapshot.CastsPerSecond is { } castsPerSecond
                ? FormatLine(
                    "Metric_CastsPerMinute",
                    string.Format(
                        CultureInfo.CurrentCulture,
                        "{0} ({1})",
                        FormatDecimal(castsPerMinute),
                        FormatDecimal(castsPerSecond)))
                : null);
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
