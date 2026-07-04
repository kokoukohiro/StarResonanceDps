using System;

namespace StarResonanceDps.App.Views.Widgets;

public interface IWidgetVerticalScrollContent
{
    event EventHandler? VerticalScrollMetricsChanged;

    WidgetVerticalScrollMetrics GetVerticalScrollMetrics();

    void SetVerticalScrollOffset(double verticalOffset);
}

public readonly record struct WidgetVerticalScrollMetrics(
    double Maximum,
    double ViewportSize,
    double Value,
    double LargeChange,
    double SmallChange);
