// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using System.Windows.Media;

namespace StarResonanceDps.App.Controls;

/// <summary>バフ稼働率のレールを引く相手(ビュー)。</summary>
public interface IBuffUptimeRailSource
{
    /// <summary>行のバー(できている分)。レールはバーの高さの中央に、バーと同じ左右で引く。</summary>
    IEnumerable<FrameworkElement> GetRailElements();

    /// <summary>行の見えている範囲(縦のスクロールの窓)。この外の行には引かない。</summary>
    FrameworkElement? RailViewport { get; }

    double HorizontalOffset { get; }

    double VisibleSeconds { get; }

    /// <summary>その人が周りにいなかった区間(戦闘の経過の秒、[始まり, 終わり] を交互)。この間は引かない。</summary>
    IReadOnlyList<double> AbsentSpans { get; }
}

/// <summary>
/// バフ稼働率の行のレール。推移グラフの軸と同じく窓の枠の層(<c>WidgetWindow.FrameOverlayHost</c>)へ差し込み、分割線と同じく窓の不透明度で合成する。
/// 線は画素にそろえる。行の位置は縦のスクロールで動くので、配置が変わるたびに位置を見て、変わっていれば描き直す。
/// 枠の層は中身の層の切り取りを受けないので、見えている範囲の外の行は自分で除く。
/// </summary>
public sealed class BuffUptimeRailLayer : FrameworkElement
{
    public static readonly DependencyProperty BrushProperty = DependencyProperty.Register(
        nameof(Brush),
        typeof(Brush),
        typeof(BuffUptimeRailLayer),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LineThicknessProperty = DependencyProperty.Register(
        nameof(LineThickness),
        typeof(double),
        typeof(BuffUptimeRailLayer),
        new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.AffectsRender));

    private readonly List<Rect> _placements = [];
    private readonly List<Rect> _renderedPlacements = [];
    private IBuffUptimeRailSource? _source;

    public BuffUptimeRailLayer()
    {
        IsHitTestVisible = false;
        LayoutUpdated += RailLayer_LayoutUpdated;
    }

    public Brush? Brush
    {
        get => (Brush?)GetValue(BrushProperty);
        set => SetValue(BrushProperty, value);
    }

    public double LineThickness
    {
        get => (double)GetValue(LineThicknessProperty);
        set => SetValue(LineThicknessProperty, value);
    }

    /// <summary>引く相手。置くのはビュー。</summary>
    public IBuffUptimeRailSource? Source
    {
        get => _source;
        set
        {
            _source = value;
            InvalidateVisual();
        }
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        CollectPlacements(_renderedPlacements);
        if (Source is not { } source || Brush is null || _renderedPlacements.Count == 0)
        {
            return;
        }

        var pen = new Pen(Brush, LineThickness);
        if (pen.CanFreeze)
        {
            pen.Freeze();
        }

        var offset = source.HorizontalOffset;
        var visibleSeconds = source.VisibleSeconds;
        var absent = source.AbsentSpans;
        var halfThickness = pen.Thickness / 2d;
        var guidelines = new GuidelineSet();
        foreach (var rect in _renderedPlacements)
        {
            guidelines.GuidelinesY.Add(rect.Top + rect.Height / 2d + halfThickness);
        }

        drawingContext.PushGuidelineSet(guidelines);
        foreach (var rect in _renderedPlacements)
        {
            var y = rect.Top + rect.Height / 2d;
            var cursor = offset;
            var end = offset + visibleSeconds;
            for (var index = 0; index + 1 < absent.Count && cursor < end; index += 2)
            {
                if (absent[index + 1] <= cursor)
                {
                    continue;
                }

                DrawSegment(drawingContext, pen, rect, y, offset, visibleSeconds, cursor, Math.Min(absent[index], end));
                cursor = Math.Max(cursor, absent[index + 1]);
            }

            DrawSegment(drawingContext, pen, rect, y, offset, visibleSeconds, cursor, end);
        }

        drawingContext.Pop();
    }

    private static void DrawSegment(DrawingContext drawingContext, Pen pen, Rect rect, double y, double offset, double visibleSeconds, double startSeconds, double endSeconds)
    {
        if (endSeconds <= startSeconds)
        {
            return;
        }

        var left = Math.Max(MetricTimeAxis.ToX(rect.Left, rect.Width, offset, visibleSeconds, startSeconds), rect.Left);
        var right = Math.Min(MetricTimeAxis.ToX(rect.Left, rect.Width, offset, visibleSeconds, endSeconds), rect.Right);
        if (right > left)
        {
            drawingContext.DrawLine(pen, new Point(left, y), new Point(right, y));
        }
    }

    /// <summary>見えている範囲の中の行のバーの、この要素から見た四角。</summary>
    private void CollectPlacements(List<Rect> target)
    {
        target.Clear();
        if (Source?.RailViewport is not { } viewport || viewport.FindCommonVisualAncestor(this) is null)
        {
            return;
        }

        var viewportRect = viewport.TransformToVisual(this).TransformBounds(new Rect(viewport.RenderSize));
        foreach (var element in Source.GetRailElements())
        {
            if (!element.IsVisible || element.FindCommonVisualAncestor(this) is null)
            {
                continue;
            }

            var rect = element.TransformToVisual(this).TransformBounds(new Rect(element.RenderSize));
            var centerY = rect.Top + rect.Height / 2d;
            if (centerY >= viewportRect.Top && centerY <= viewportRect.Bottom && rect.Width > 0d)
            {
                target.Add(rect);
            }
        }
    }

    private void RailLayer_LayoutUpdated(object? sender, EventArgs e)
    {
        if (Source is null)
        {
            return;
        }

        CollectPlacements(_placements);
        if (_placements.SequenceEqual(_renderedPlacements))
        {
            return;
        }

        InvalidateVisual();
    }
}
