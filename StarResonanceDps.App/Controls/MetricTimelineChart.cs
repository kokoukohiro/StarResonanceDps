using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.Controls;

/// <summary>
/// 推移グラフ。横は <see cref="VisibleSeconds"/> 秒分を描く領域の幅に固定し、それより長い分は
/// <see cref="HorizontalOffset"/>(窓の横のスクロールバー、<c>IWidgetHorizontalScrollContent</c> 経由)で横にずらして見る。
/// 縦軸の数字は左に固定する。
///
/// <para>
/// 軸の数字は <c>Text.WidgetWindow</c> の TextBlock。グラフの線は <see cref="LineBrush"/>(その人のクラスのグラフカラー)で描き、
/// 文字と同じ影を線の層にだけ掛ける(文字は自分のスタイルの影を持つので、この要素に掛けると文字の影が二重になる)。
/// 格子と軸の線は分割線と同じく窓の枠の層に描く(<see cref="MetricTimelineGridLayer"/>、位置はこの要素の描く領域から求める)。
/// </para>
///
/// <para>
/// 右端を見ている間は最新を追いかける。左へ戻すと追いかけるのをやめ、右端へ戻すと再開する。
/// 区切りの数が減ったとき(別の回・描画間隔の変更)も右端へ戻す。
/// </para>
/// </summary>
public sealed class MetricTimelineChart : FrameworkElement
{
    /// <summary>横軸の数字の区間の数の上限。数字の間隔(<see cref="GridStepSeconds"/>)はこれを超えない切りのいい秒にする。</summary>
    private const int MaxSecondLabelIntervals = 4;

    /// <summary>横軸の数字の間隔の候補(秒)。<see cref="VisibleSeconds"/> を <see cref="MaxSecondLabelIntervals"/> 区間以下に分ける最小のものを使う。</summary>
    private static readonly double[] SecondLabelStepCandidates = [5d, 10d, 15d, 30d, 60d, 120d, 300d, 600d];

    private const int HorizontalGridCount = 2;

    /// <summary>縦軸の数字の右端と描く領域の左端の間。</summary>
    private const double ValueLabelGap = 4d;

    /// <summary>描く領域の下端と横軸の数字の上端の間。</summary>
    private const double SecondLabelGap = 2d;
    private const double AxisFontSize = 10d;

    public static readonly DependencyProperty PointsProperty = DependencyProperty.Register(
        nameof(Points),
        typeof(IReadOnlyList<MetricTimelinePoint>),
        typeof(MetricTimelineChart),
        new FrameworkPropertyMetadata(Array.Empty<MetricTimelinePoint>(), OnPointsChanged));

    /// <summary>見ている窓の始まり(戦闘の秒)。0〜<see cref="ScrollableSeconds"/> に収める。</summary>
    public static readonly DependencyProperty HorizontalOffsetProperty = DependencyProperty.Register(
        nameof(HorizontalOffset),
        typeof(double),
        typeof(MetricTimelineChart),
        new FrameworkPropertyMetadata(
            0d,
            OnHorizontalOffsetChanged,
            CoerceHorizontalOffset));

    /// <summary>描く領域の幅に入れる秒数(設定「横軸の長さ」)。スクロールバーの見えている幅もこれ。</summary>
    public static readonly DependencyProperty VisibleSecondsProperty = DependencyProperty.Register(
        nameof(VisibleSeconds),
        typeof(double),
        typeof(MetricTimelineChart),
        new FrameworkPropertyMetadata(60d, OnVisibleSecondsChanged),
        static value => value is double seconds && seconds > 0d && !double.IsInfinity(seconds));

    /// <summary>グラフの線の色。null の間は線を描かない。</summary>
    public static readonly DependencyProperty LineBrushProperty = DependencyProperty.Register(
        nameof(LineBrush),
        typeof(Brush),
        typeof(MetricTimelineChart),
        new FrameworkPropertyMetadata(null, OnLineBrushChanged));

    private readonly VisualCollection _children;
    private readonly LineLayer _lineLayer;
    private readonly TextBlock[] _valueLabels;
    private readonly TextBlock[] _secondLabels;
    private readonly List<double> _secondLabelSeconds = [];
    private MetricTimelinePoint[] _orderedPoints = [];
    private double _maxSeconds;
    private double _maxValuePerSecond;
    private int _lastPointCount;
    private bool _isFollowingEnd = true;
    private bool _isSettingOffset;

    public MetricTimelineChart()
    {
        _lineLayer = new LineLayer(this);
        _lineLayer.SetResourceReference(EffectProperty, "Effect.WidgetWindowTextShadow");
        _children = new VisualCollection(this) { _lineLayer };
        _valueLabels = CreateLabels(HorizontalGridCount + 1);
        _secondLabels = CreateLabels(MaxSecondLabelIntervals + 1);
        UpdateLabels();
    }

    public IReadOnlyList<MetricTimelinePoint> Points
    {
        get => (IReadOnlyList<MetricTimelinePoint>)GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    public double HorizontalOffset
    {
        get => (double)GetValue(HorizontalOffsetProperty);
        set => SetValue(HorizontalOffsetProperty, value);
    }

    public double VisibleSeconds
    {
        get => (double)GetValue(VisibleSecondsProperty);
        set => SetValue(VisibleSecondsProperty, value);
    }

    public Brush? LineBrush
    {
        get => (Brush?)GetValue(LineBrushProperty);
        set => SetValue(LineBrushProperty, value);
    }

    /// <summary>横軸の数字の間隔。戦闘の秒に結び付けるので、スクロールすると数字も動く。スクロールバーの1段の移動もこれ。</summary>
    public double GridStepSeconds
    {
        get
        {
            var visibleSeconds = VisibleSeconds;
            foreach (var step in SecondLabelStepCandidates)
            {
                if (visibleSeconds / step <= MaxSecondLabelIntervals)
                {
                    return step;
                }
            }

            return Math.Ceiling(visibleSeconds / MaxSecondLabelIntervals);
        }
    }

    /// <summary>ずらせる秒数(最後の区切りの終わり − <see cref="VisibleSeconds"/>、足りなければ 0)。スクロールバーの最大値で、0 ならバーを出さない。</summary>
    public double ScrollableSeconds { get; private set; }

    /// <summary>枠の層に置いた格子。描き直しを頼む相手(置くのはビュー)。</summary>
    public MetricTimelineGridLayer? GridLayer { get; set; }

    /// <summary><see cref="ScrollableSeconds"/> か <see cref="HorizontalOffset"/> が変わった(スクロールバーを合わせる合図)。</summary>
    public event EventHandler? ScrollMetricsChanged;

    protected override int VisualChildrenCount => _children.Count;

    protected override Visual GetVisualChild(int index)
    {
        return _children[index];
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var unbounded = new Size(double.PositiveInfinity, double.PositiveInfinity);
        foreach (var label in _valueLabels)
        {
            label.Measure(unbounded);
        }

        foreach (var label in _secondLabels)
        {
            label.Measure(unbounded);
        }

        _lineLayer.Measure(availableSize);
        return new Size(1d, 1d);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _lineLayer.Arrange(new Rect(finalSize));

        var plotBounds = GetPlotBounds(finalSize);
        var hasPlot = plotBounds.Width > 0 && plotBounds.Height > 0;

        // 縦軸の数字は右寄せ(右端を描く領域の左端の手前にそろえる)。
        for (var index = 0; index < _valueLabels.Length; index++)
        {
            var label = _valueLabels[index];
            var y = plotBounds.Top + plotBounds.Height * index / HorizontalGridCount;
            label.Arrange(hasPlot
                ? new Rect(
                    new Point(plotBounds.Left - ValueLabelGap - label.DesiredSize.Width, y - label.DesiredSize.Height / 2d),
                    label.DesiredSize)
                : new Rect());
        }

        for (var index = 0; index < _secondLabels.Length; index++)
        {
            var label = _secondLabels[index];
            if (index >= _secondLabelSeconds.Count)
            {
                continue;
            }

            var x = ToX(plotBounds, _secondLabelSeconds[index]);
            label.Arrange(hasPlot
                ? new Rect(new Point(x - label.DesiredSize.Width / 2d, plotBounds.Bottom + SecondLabelGap), label.DesiredSize)
                : new Rect());
        }

        GridLayer?.InvalidateVisual();
        return finalSize;
    }

    private TextBlock[] CreateLabels(int count)
    {
        var labels = new TextBlock[count];
        for (var index = 0; index < count; index++)
        {
            var label = new TextBlock
            {
                FontSize = AxisFontSize,
                IsHitTestVisible = false
            };
            label.SetResourceReference(StyleProperty, "Text.WidgetWindow");
            _children.Add(label);
            labels[index] = label;
        }

        return labels;
    }

    private static void OnPointsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((MetricTimelineChart)d).ApplyPoints();
    }

    private static object CoerceHorizontalOffset(DependencyObject d, object baseValue)
    {
        var value = (double)baseValue;
        return double.IsNaN(value)
            ? 0d
            : Math.Clamp(value, 0d, ((MetricTimelineChart)d).ScrollableSeconds);
    }

    private static void OnHorizontalOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var chart = (MetricTimelineChart)d;
        if (!chart._isSettingOffset)
        {
            // 手で動かした。右端にいる間だけ最新を追いかける。
            chart._isFollowingEnd = chart.HorizontalOffset >= chart.ScrollableSeconds;
        }

        chart.Refresh();
    }

    private static void OnVisibleSecondsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var chart = (MetricTimelineChart)d;
        chart.UpdateScrollRange();
        chart.Refresh();
    }

    private static void OnLineBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((MetricTimelineChart)d)._lineLayer.InvalidateVisual();
    }

    private void ApplyPoints()
    {
        var points = Points ?? Array.Empty<MetricTimelinePoint>();
        _orderedPoints = points
            .OrderBy(static point => point.StartSeconds)
            .ToArray();
        _maxSeconds = points.Count == 0
            ? 0d
            : points.Max(static point => point.EndSeconds);
        _maxValuePerSecond = points.Count == 0
            ? 0d
            : points.Max(static point => point.ValuePerSecond);

        if (points.Count < _lastPointCount)
        {
            _isFollowingEnd = true;
        }

        _lastPointCount = points.Count;

        UpdateScrollRange();
        Refresh();
    }

    /// <summary>ずらせる秒数を最後の区切りの終わりと <see cref="VisibleSeconds"/> から決め直す。最新を追いかけている間は右端へ寄せる。</summary>
    private void UpdateScrollRange()
    {
        var scrollableSeconds = Math.Max(_maxSeconds - VisibleSeconds, 0d);
        ScrollableSeconds = scrollableSeconds;

        _isSettingOffset = true;
        try
        {
            if (_isFollowingEnd)
            {
                SetCurrentValue(HorizontalOffsetProperty, scrollableSeconds);
            }
            else
            {
                CoerceValue(HorizontalOffsetProperty);
            }
        }
        finally
        {
            _isSettingOffset = false;
        }
    }

    private void Refresh()
    {
        UpdateLabels();
        InvalidateMeasure();
        InvalidateArrange();
        _lineLayer.InvalidateVisual();
        GridLayer?.InvalidateVisual();
        ScrollMetricsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 縦軸の数字(最大値・半分・0)と、見ている窓の中の横軸の数字の秒を決める。
    /// 最大値が無い(データが無い・全部0)ときは 0 だけを出し、ほかは空にする。
    /// </summary>
    private void UpdateLabels()
    {
        var lastValueLabelIndex = _valueLabels.Length - 1;
        for (var index = 0; index < _valueLabels.Length; index++)
        {
            _valueLabels[index].Text = _maxValuePerSecond > 0d || index == lastValueLabelIndex
                ? FormatNumber(_maxValuePerSecond * (1d - index / (double)HorizontalGridCount))
                : string.Empty;
        }

        var offset = HorizontalOffset;
        var step = GridStepSeconds;
        _secondLabelSeconds.Clear();
        for (var seconds = Math.Ceiling(offset / step) * step;
             seconds <= offset + VisibleSeconds && _secondLabelSeconds.Count < _secondLabels.Length;
             seconds += step)
        {
            _secondLabelSeconds.Add(seconds);
        }

        for (var index = 0; index < _secondLabels.Length; index++)
        {
            var label = _secondLabels[index];
            if (index < _secondLabelSeconds.Count)
            {
                var totalSeconds = (int)Math.Round(_secondLabelSeconds[index]);
                label.Text = $"{totalSeconds / 60}:{totalSeconds % 60:00}";
                label.Visibility = Visibility.Visible;
            }
            else
            {
                label.Visibility = Visibility.Collapsed;
            }
        }
    }

    private void RenderLines(DrawingContext drawingContext, Size size)
    {
        var bounds = new Rect(size);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        drawingContext.DrawRectangle(Brushes.Transparent, null, bounds);

        var plotBounds = GetPlotBounds(size);
        if (plotBounds.Width <= 0 || plotBounds.Height <= 0)
        {
            return;
        }

        if (_orderedPoints.Length == 0 || LineBrush is not { } lineBrush)
        {
            return;
        }

        // 点は区切りの中央に置く。見ている窓の外は描く領域の左右で切る。高さは最大値で割る(全部0なら下端)。
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            for (var index = 0; index < _orderedPoints.Length; index++)
            {
                var point = _orderedPoints[index];
                var ratio = _maxValuePerSecond > 0d
                    ? point.ValuePerSecond / _maxValuePerSecond
                    : 0d;
                var dataPoint = new Point(
                    ToX(plotBounds, (point.StartSeconds + point.EndSeconds) * 0.5d),
                    plotBounds.Bottom - ratio * plotBounds.Height);

                if (index == 0)
                {
                    context.BeginFigure(dataPoint, false, false);
                }
                else
                {
                    context.LineTo(dataPoint, true, false);
                }
            }
        }

        geometry.Freeze();

        var linePen = CreateLinePen(lineBrush);
        drawingContext.PushClip(new RectangleGeometry(new Rect(plotBounds.Left, bounds.Top, plotBounds.Width, bounds.Height)));
        drawingContext.DrawGeometry(null, linePen, geometry);
        drawingContext.Pop();
    }

    /// <summary>
    /// 格子と軸の線を <paramref name="target"/>(枠の層の <see cref="MetricTimelineGridLayer"/>)の座標で描く。
    /// 格子は描く領域の横 <see cref="HorizontalGridCount"/> 等分の横線だけ(横軸の分割線は描かない)。
    /// 軸は左端の縦線と下端の横線(下端の横線は格子の横線の最後の1本と同じ)。
    /// </summary>
    internal void RenderGrid(DrawingContext drawingContext, Visual target, Pen pen)
    {
        var plotBounds = GetPlotBounds(RenderSize);
        if (plotBounds.Width <= 0 || plotBounds.Height <= 0 || FindCommonVisualAncestor(target) is null)
        {
            return;
        }

        var transform = TransformToVisual(target);
        var lines = new List<(Point Start, Point End)>();
        for (var index = 0; index <= HorizontalGridCount; index++)
        {
            var y = plotBounds.Top + plotBounds.Height * index / HorizontalGridCount;
            lines.Add((transform.Transform(new Point(plotBounds.Left, y)), transform.Transform(new Point(plotBounds.Right, y))));
        }

        // 縦軸。下端の横軸は上の横線の最後の1本。
        lines.Add((transform.Transform(plotBounds.TopLeft), transform.Transform(plotBounds.BottomLeft)));

        // 分割線(Border)と同じく画素にそろえる。線の中心を画素の中央へ寄せないと 2px にぼける。
        var halfThickness = pen.Thickness / 2d;
        var guidelines = new GuidelineSet();
        foreach (var (start, end) in lines)
        {
            guidelines.GuidelinesX.Add(start.X + halfThickness);
            guidelines.GuidelinesY.Add(start.Y + halfThickness);
            guidelines.GuidelinesY.Add(end.Y + halfThickness);
        }

        drawingContext.PushGuidelineSet(guidelines);
        foreach (var (start, end) in lines)
        {
            drawingContext.DrawLine(pen, start, end);
        }

        drawingContext.Pop();
    }

    /// <summary>描く領域の左上の、<paramref name="target"/> から見た位置と描く領域の大きさ。格子の部品が位置の変化を見るのに使う。</summary>
    internal (Point Origin, Size PlotSize)? GetPlotPlacement(Visual target)
    {
        if (FindCommonVisualAncestor(target) is null)
        {
            return null;
        }

        var plotBounds = GetPlotBounds(RenderSize);
        return (TransformToVisual(target).Transform(plotBounds.TopLeft), plotBounds.Size);
    }

    private double ToX(Rect plotBounds, double seconds)
    {
        return plotBounds.Left + (seconds - HorizontalOffset) / VisibleSeconds * plotBounds.Width;
    }

    /// <summary>
    /// 描く領域。周りの余白は軸の数字の大きさから決め、数字のいちばん外側がこの要素の端に来るようにする
    /// (窓の枠との隙間はビューの余白が持つ)。左は縦軸の数字のいちばん広い幅と間、上は縦軸の数字の高さの半分
    /// (いちばん上の数字は線の高さを中心に置く)、下は横軸の数字の高さと間、右は横軸の数字の幅の半分(右端の数字は時刻を中心に置く)。
    /// 数字の大きさは測った後の値(<see cref="MeasureOverride"/> で測る)。
    /// </summary>
    private Rect GetPlotBounds(Size size)
    {
        var valueLabelWidth = 0d;
        var valueLabelHeight = 0d;
        foreach (var label in _valueLabels)
        {
            valueLabelWidth = Math.Max(valueLabelWidth, label.DesiredSize.Width);
            valueLabelHeight = Math.Max(valueLabelHeight, label.DesiredSize.Height);
        }

        var secondLabelWidth = 0d;
        var secondLabelHeight = 0d;
        foreach (var label in _secondLabels)
        {
            secondLabelWidth = Math.Max(secondLabelWidth, label.DesiredSize.Width);
            secondLabelHeight = Math.Max(secondLabelHeight, label.DesiredSize.Height);
        }

        var left = valueLabelWidth + ValueLabelGap;
        var top = valueLabelHeight / 2d;
        var right = secondLabelWidth / 2d;
        var bottom = secondLabelHeight + SecondLabelGap;
        return new Rect(
            left,
            top,
            Math.Max(size.Width - left - right, 0d),
            Math.Max(size.Height - top - bottom, 0d));
    }

    private static Pen CreateLinePen(Brush brush)
    {
        var pen = new Pen(brush, 1.5d)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };

        if (pen.CanFreeze)
        {
            pen.Freeze();
        }

        return pen;
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

    /// <summary>グラフの線の層。影を軸の数字と別に掛けるために、文字とは別の要素にしてある。</summary>
    private sealed class LineLayer(MetricTimelineChart owner) : FrameworkElement
    {
        protected override void OnRender(DrawingContext drawingContext)
        {
            owner.RenderLines(drawingContext, RenderSize);
        }
    }
}

/// <summary>
/// 推移グラフの格子と軸の線。<b>窓の枠の層(<c>WidgetWindow.FrameOverlayHost</c>)へ差し込む</b>ので、
/// 分割線と同じく窓の不透明度で合成され、影は付かない。線は1px、色は <see cref="Brush"/>(分割線の色を束縛する)。
///
/// <para>
/// 位置は <see cref="Chart"/> の描く領域から求める。グラフの中身が変わったときはグラフが描き直しを頼み、
/// 窓の中でグラフの位置だけが動いたとき(ヘッダーを隠したときなど)はこの部品が配置の後に気付いて描き直す。
/// </para>
/// </summary>
public sealed class MetricTimelineGridLayer : FrameworkElement
{
    public static readonly DependencyProperty BrushProperty = DependencyProperty.Register(
        nameof(Brush),
        typeof(Brush),
        typeof(MetricTimelineGridLayer),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    private (Point Origin, Size PlotSize)? _renderedPlacement;

    public MetricTimelineGridLayer()
    {
        IsHitTestVisible = false;
        LayoutUpdated += GridLayer_LayoutUpdated;
    }

    public Brush? Brush
    {
        get => (Brush?)GetValue(BrushProperty);
        set => SetValue(BrushProperty, value);
    }

    /// <summary>格子を描く相手のグラフ。置くのはビュー。</summary>
    public MetricTimelineChart? Chart { get; set; }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        _renderedPlacement = Chart?.GetPlotPlacement(this);
        if (Chart is null || Brush is null)
        {
            return;
        }

        var pen = new Pen(Brush, 1d);
        if (pen.CanFreeze)
        {
            pen.Freeze();
        }

        Chart.RenderGrid(drawingContext, this, pen);
    }

    private void GridLayer_LayoutUpdated(object? sender, EventArgs e)
    {
        if (Chart is null || Chart.GetPlotPlacement(this) == _renderedPlacement)
        {
            return;
        }

        InvalidateVisual();
    }
}
