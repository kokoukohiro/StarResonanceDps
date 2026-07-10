namespace StarResonanceDps.App.Models.Widgets;

public sealed class PlayerMetricSummaryEntry(
    IReadOnlyList<string> valueLines,
    IReadOnlyList<string> rateLines,
    IReadOnlyList<string> distributionLines,
    IReadOnlyList<string> castLines)
{
    public IReadOnlyList<string> ValueLines { get; } = valueLines;

    public IReadOnlyList<string> RateLines { get; } = rateLines;

    public IReadOnlyList<string> DistributionLines { get; } = distributionLines;

    public IReadOnlyList<string> CastLines { get; } = castLines;
}
