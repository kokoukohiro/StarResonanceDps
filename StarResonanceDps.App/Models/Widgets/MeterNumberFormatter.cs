using System.Globalization;

namespace StarResonanceDps.App.Models.Widgets;

public static class MeterNumberFormatter
{
    public static string Format(double value, int formatIndex)
    {
        var normalized = double.IsFinite(value)
            ? Math.Max(value, 0d)
            : 0d;

        return formatIndex == 1
            ? FormatWan(normalized)
            : FormatKmb(normalized);
    }

    private static string FormatKmb(double value)
    {
        return value switch
        {
            >= 1_000_000_000d => $"{FormatValue(value / 1_000_000_000d)}B",
            >= 1_000_000d => $"{FormatValue(value / 1_000_000d)}M",
            >= 1_000d => $"{FormatValue(value / 1_000d)}K",
            _ => FormatValue(value)
        };
    }

    private static string FormatWan(double value)
    {
        return value switch
        {
            >= 1_000_000_000_000d => $"{FormatValue(value / 1_000_000_000_000d)}兆",
            >= 100_000_000d => $"{FormatValue(value / 100_000_000d)}億",
            >= 10_000d => $"{FormatValue(value / 10_000d)}万",
            _ => FormatValue(value)
        };
    }

    private static string FormatValue(double value)
    {
        return value.ToString("0.##", CultureInfo.CurrentCulture);
    }
}
