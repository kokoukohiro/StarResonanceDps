using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.Controls;

public sealed class MetricTimelineChart : FrameworkElement
{
    public static readonly DependencyProperty PointsProperty = DependencyProperty.Register(
        nameof(Points),
        typeof(IReadOnlyList<MetricTimelinePoint>),
        typeof(MetricTimelineChart),
        new FrameworkPropertyMetadata(Array.Empty<MetricTimelinePoint>(), FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LineBrushProperty = DependencyProperty.Register(
        nameof(LineBrush),
        typeof(Brush),
        typeof(MetricTimelineChart),
        new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty GridBrushProperty = DependencyProperty.Register(
        nameof(GridBrush),
        typeof(Brush),
        typeof(MetricTimelineChart),
        new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TextBrushProperty = DependencyProperty.Register(
        nameof(TextBrush),
        typeof(Brush),
        typeof(MetricTimelineChart),
        new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public IReadOnlyList<MetricTimelinePoint> Points
    {
        get => (IReadOnlyList<MetricTimelinePoint>)GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    public Brush LineBrush
    {
        get => (Brush)GetValue(LineBrushProperty);
        set => SetValue(LineBrushProperty, value);
    }

    public Brush GridBrush
    {
        get => (Brush)GetValue(GridBrushProperty);
        set => SetValue(GridBrushProperty, value);
    }

    public Brush TextBrush
    {
        get => (Brush)GetValue(TextBrushProperty);
        set => SetValue(TextBrushProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        return new Size(1d, 1d);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        drawingContext.DrawRectangle(Brushes.Transparent, null, bounds);

        const double leftPadding = 44d;
        const double topPadding = 8d;
        const double rightPadding = 14d;
        const double bottomPadding = 24d;

        var plotBounds = new Rect(
            leftPadding,
            topPadding,
            Math.Max(bounds.Width - leftPadding - rightPadding, 0d),
            Math.Max(bounds.Height - topPadding - bottomPadding, 0d));
        if (plotBounds.Width <= 0 || plotBounds.Height <= 0)
        {
            return;
        }

        var points = Points ?? Array.Empty<MetricTimelinePoint>();
        var linePen = new Pen(LineBrush, 1.5d)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        if (linePen.CanFreeze)
        {
            linePen.Freeze();
        }

        var gridPen = new Pen(GridBrush, 1d);
        if (gridPen.CanFreeze)
        {
            gridPen.Freeze();
        }

        var axisPen = new Pen(TextBrush, 1d);
        if (axisPen.CanFreeze)
        {
            axisPen.Freeze();
        }

        // 点は区切りの中央に置き、軸の右端は最後の区切りの終わりにする。
        var maxX = points.Count == 0
            ? 1d
            : Math.Max(points.Max(static point => point.EndSeconds), 1d);
        var maxY = points.Count == 0
            ? 1d
            : Math.Max(points.Max(static point => point.ValuePerSecond), 1d);

        DrawGridAndAxes(drawingContext, plotBounds, maxX, maxY, gridPen, axisPen);

        if (points.Count == 0)
        {
            return;
        }

        var barBrush = CreateBarBrush(LineBrush);
        var markerBrush = TextBrush;
        var geometry = new StreamGeometry();
        var orderedPoints = points
            .OrderBy(static point => point.StartSeconds)
            .ToArray();

        using (var context = geometry.Open())
        {
            var hasStartedFigure = false;
            for (var index = 0; index < orderedPoints.Length; index++)
            {
                var point = orderedPoints[index];
                var normalizedX = maxX <= 0d ? 0d : GetCenterSeconds(point) / maxX;
                var normalizedY = maxY <= 0d ? 0d : point.ValuePerSecond / maxY;
                var x = plotBounds.Left + normalizedX * plotBounds.Width;
                var y = plotBounds.Bottom - normalizedY * plotBounds.Height;
                var dataPoint = new Point(x, y);

                var previousX = index == 0
                    ? plotBounds.Left
                    : plotBounds.Left + (Math.Max(GetCenterSeconds(orderedPoints[index - 1]), 0d) / maxX) * plotBounds.Width;
                var nextX = index == orderedPoints.Length - 1
                    ? plotBounds.Right
                    : plotBounds.Left + (Math.Max(GetCenterSeconds(orderedPoints[index + 1]), 0d) / maxX) * plotBounds.Width;
                var availableWidth = Math.Max(Math.Min(x - previousX, nextX - x), 0d);
                var barWidth = Math.Clamp(availableWidth * 0.6d, 2d, 12d);
                var barRect = new Rect(
                    x - barWidth * 0.5d,
                    y,
                    barWidth,
                    Math.Max(plotBounds.Bottom - y, 1d));
                drawingContext.DrawRectangle(barBrush, null, barRect);

                if (!hasStartedFigure)
                {
                    context.BeginFigure(dataPoint, false, false);
                    hasStartedFigure = true;
                }
                else
                {
                    context.LineTo(dataPoint, true, false);
                }

                drawingContext.DrawEllipse(markerBrush, null, dataPoint, 2d, 2d);
            }
        }

        if (geometry.CanFreeze)
        {
            geometry.Freeze();
        }

        drawingContext.DrawGeometry(null, linePen, geometry);
    }

    private static double GetCenterSeconds(MetricTimelinePoint point)
    {
        return (point.StartSeconds + point.EndSeconds) * 0.5d;
    }

    private static Brush CreateBarBrush(Brush source)
    {
        if (source is SolidColorBrush solidBrush)
        {
            var barBrush = new SolidColorBrush(solidBrush.Color)
            {
                Opacity = Math.Min(Math.Max(solidBrush.Opacity * 0.28d, 0.18d), 0.4d)
            };
            if (barBrush.CanFreeze)
            {
                barBrush.Freeze();
            }

            return barBrush;
        }

        return new SolidColorBrush(Color.FromArgb(64, 255, 255, 255));
    }

    private void DrawGridAndAxes(
        DrawingContext drawingContext,
        Rect plotBounds,
        double maxX,
        double maxY,
        Pen gridPen,
        Pen axisPen)
    {
        const int verticalGridCount = 4;
        const int horizontalGridCount = 4;

        for (var index = 0; index <= horizontalGridCount; index++)
        {
            var progress = index / (double)horizontalGridCount;
            var y = plotBounds.Top + plotBounds.Height * progress;
            drawingContext.DrawLine(gridPen, new Point(plotBounds.Left, y), new Point(plotBounds.Right, y));

            var value = maxY * (1d - progress);
            DrawText(
                drawingContext,
                FormatNumber(value),
                new Point(0d, y - 8d));
        }

        for (var index = 0; index <= verticalGridCount; index++)
        {
            var progress = index / (double)verticalGridCount;
            var x = plotBounds.Left + plotBounds.Width * progress;
            drawingContext.DrawLine(gridPen, new Point(x, plotBounds.Top), new Point(x, plotBounds.Bottom));

            var value = maxX * progress;
            DrawText(
                drawingContext,
                $"{value:F0}s",
                new Point(x - 10d, plotBounds.Bottom + 2d));
        }

        drawingContext.DrawLine(axisPen, new Point(plotBounds.Left, plotBounds.Top), new Point(plotBounds.Left, plotBounds.Bottom));
        drawingContext.DrawLine(axisPen, new Point(plotBounds.Left, plotBounds.Bottom), new Point(plotBounds.Right, plotBounds.Bottom));
    }

    private void DrawText(DrawingContext drawingContext, string text, Point origin)
    {
        var dpiScale = VisualTreeHelper.GetDpi(this);
        var formatted = new FormattedText(
            text,
            System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            10d,
            TextBrush,
            dpiScale.PixelsPerDip);
        drawingContext.DrawText(formatted, origin);
    }

    private static string FormatNumber(double value)
    {
        return value switch
        {
            >= 1_000_000_000d => $"{value / 1_000_000_000d:0.##}B",
            >= 1_000_000d => $"{value / 1_000_000d:0.##}M",
            >= 1_000d => $"{value / 1_000d:0.##}K",
            _ => $"{value:0}"
        };
    }
}
