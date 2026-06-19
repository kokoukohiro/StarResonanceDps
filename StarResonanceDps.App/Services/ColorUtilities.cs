using System.Globalization;
using System.Windows.Media;

namespace StarResonanceDps.App.Services;

public static class ColorUtilities
{
    public static Color GetReadableTextColor(Color background)
    {
        return IsLight(background) ? Colors.Black : Colors.White;
    }

    public static bool IsLight(Color color)
    {
        return GetRelativeLuminance(color) > 0.5;
    }

    public static double GetRelativeLuminance(Color color)
    {
        static double ToLinear(byte channel)
        {
            var value = channel / 255.0;
            return value <= 0.03928
                ? value / 12.92
                : Math.Pow((value + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * ToLinear(color.R)
            + 0.7152 * ToLinear(color.G)
            + 0.0722 * ToLinear(color.B);
    }

    public static string ToHex(Color color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    public static bool TryParseHex(string? raw, out Color color)
    {
        color = Colors.White;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var text = raw.Trim();
        if (text.StartsWith("#", StringComparison.Ordinal))
        {
            text = text[1..];
        }

        if (text.Length != 6)
        {
            return false;
        }

        foreach (var ch in text)
        {
            if (!Uri.IsHexDigit(ch))
            {
                return false;
            }
        }

        if (!byte.TryParse(text.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r)
            || !byte.TryParse(text.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g)
            || !byte.TryParse(text.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
        {
            return false;
        }

        color = Color.FromRgb(r, g, b);
        return true;
    }
}
