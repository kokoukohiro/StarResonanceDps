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
            >= 1_000_000_000d => $"{FormatScaled(value / 1_000_000_000d)}B",
            >= 1_000_000d => $"{FormatScaled(value / 1_000_000d)}M",
            >= 1_000d => $"{FormatScaled(value / 1_000d)}K",
            _ => FormatValue(value)
        };
    }

    private static string FormatWan(double value)
    {
        return value switch
        {
            >= 1_000_000_000_000d => $"{FormatScaled(value / 1_000_000_000_000d)}兆",
            >= 100_000_000d => $"{FormatScaled(value / 100_000_000d)}億",
            >= 10_000d => $"{FormatScaled(value / 10_000d)}万",
            _ => FormatValue(value)
        };
    }

    /// <summary>単位を付けて短縮した値。小数点以下2位で固定する(1.20K のように末尾の0も出す)。</summary>
    private static string FormatScaled(double value)
    {
        return value.ToString("F2", CultureInfo.CurrentCulture);
    }

    /// <summary>単位を付けずそのまま出す値。整数に小数を足さない。</summary>
    private static string FormatValue(double value)
    {
        return value.ToString("0.##", CultureInfo.CurrentCulture);
    }
}
