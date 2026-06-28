namespace StarResonanceDps.PluginSdk;

/// <summary>
/// Defines the initial dimensions used when the host opens a plugin window.
/// These values are not persisted after the user resizes the window.
/// </summary>
public sealed record PluginWindowOptions
{
    public PluginWindowOptions(
        double width,
        double height,
        double minWidth,
        double minHeight)
    {
        ValidateDimension(width, nameof(width));
        ValidateDimension(height, nameof(height));
        ValidateDimension(minWidth, nameof(minWidth));
        ValidateDimension(minHeight, nameof(minHeight));

        if (width < minWidth)
        {
            throw new ArgumentOutOfRangeException(
                nameof(width),
                "The initial width must be greater than or equal to the minimum width.");
        }

        if (height < minHeight)
        {
            throw new ArgumentOutOfRangeException(
                nameof(height),
                "The initial height must be greater than or equal to the minimum height.");
        }

        Width = width;
        Height = height;
        MinWidth = minWidth;
        MinHeight = minHeight;
    }

    public double Width { get; }

    public double Height { get; }

    public double MinWidth { get; }

    public double MinHeight { get; }

    private static void ValidateDimension(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Window dimensions must be finite values greater than zero.");
        }
    }
}
