using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Serilog;
using StarResonanceDps.App.Models.Widgets;
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
///
/// <para>
/// 横軸の数字の下(窓の横のスクロールバーの上)に、押した技のアイコンの行を置く(<see cref="SkillMarkers"/>)。
/// 位置は線と同じ式で時刻から決め、見ている窓の中の時刻のものだけを丸ごと出す(横軸の数字と同じ)。重なったら後の開始を上に描く。
/// 要素を持つのは見ている窓の中の分だけ(窓の外の分を畳んで持つと、畳んだ要素も配置の時間を食う)。
/// アイコンにマウスを乗せると技の名前を TIPS で出す。TIPS が閉じないよう、窓の中に残る要素は作り直さず使い回す。
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

    /// <summary>技のアイコンの一辺。</summary>
    private const double SkillMarkerSize = 16d;

    /// <summary>横軸の数字の下端と技のアイコンの行の上端の間。</summary>
    private const double SkillMarkerGap = 2d;

    /// <summary>TIPS の文字の大きさ(ほかのウィジェットの TIPS と同じ)。</summary>
    private const double SkillMarkerToolTipFontSize = 12d;

    /// <summary>技のアイコンの画像(ファイルのパスごと)。読めなかったファイルは null を控え、クラス不明のアイコンで出す。</summary>
    private static readonly Dictionary<string, ImageSource?> SkillIconImages = new(StringComparer.OrdinalIgnoreCase);

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

    /// <summary>横軸の下に出す技のアイコン(技の開始の届いた順)。</summary>
    public static readonly DependencyProperty SkillMarkersProperty = DependencyProperty.Register(
        nameof(SkillMarkers),
        typeof(IReadOnlyList<MetricTimelineSkillMarker>),
        typeof(MetricTimelineChart),
        new FrameworkPropertyMetadata(Array.Empty<MetricTimelineSkillMarker>(), OnSkillMarkersChanged));

    /// <summary>アイコンのファイルが無い技に出すクラス不明のアイコンの色(グラフカラーの「不明」)。</summary>
    public static readonly DependencyProperty UnknownSkillIconBrushProperty = DependencyProperty.Register(
        nameof(UnknownSkillIconBrush),
        typeof(Brush),
        typeof(MetricTimelineChart),
        new FrameworkPropertyMetadata(null, OnUnknownSkillIconBrushChanged));

    /// <summary>
    /// 技のアイコンの TIPS の色を決めるもの。<c>ToolTip.WidgetWindowInfo</c> が TIPS を付けた要素の Tag から窓のパレットを引くので、
    /// ウィジェット(<c>WidgetListItemViewModel</c>)を渡す。
    /// </summary>
    public static readonly DependencyProperty SkillMarkerToolTipTagProperty = DependencyProperty.Register(
        nameof(SkillMarkerToolTipTag),
        typeof(object),
        typeof(MetricTimelineChart),
        new FrameworkPropertyMetadata(null, OnSkillMarkerToolTipTagChanged));

    private readonly VisualCollection _children;
    private readonly LineLayer _lineLayer;
    private readonly TextBlock[] _valueLabels;
    private readonly TextBlock[] _secondLabels;
    private readonly Canvas _skillMarkerLayer;
    /// <summary>見ている窓の中の技の開始の要素。鍵は <see cref="SkillMarkers"/> の何番目か。</summary>
    private readonly Dictionary<int, SkillMarkerElement> _skillMarkerElements = [];
    private ImageBrush? _unknownSkillIconMask;
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

        // 最後に足すので、線の層(全面で当たる)より上でマウスを受ける。
        _skillMarkerLayer = new Canvas();
        _children.Add(_skillMarkerLayer);
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

    public IReadOnlyList<MetricTimelineSkillMarker> SkillMarkers
    {
        get => (IReadOnlyList<MetricTimelineSkillMarker>)GetValue(SkillMarkersProperty);
        set => SetValue(SkillMarkersProperty, value);
    }

    public Brush? UnknownSkillIconBrush
    {
        get => (Brush?)GetValue(UnknownSkillIconBrushProperty);
        set => SetValue(UnknownSkillIconBrushProperty, value);
    }

    public object? SkillMarkerToolTipTag
    {
        get => GetValue(SkillMarkerToolTipTagProperty);
        set => SetValue(SkillMarkerToolTipTagProperty, value);
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
        _skillMarkerLayer.Measure(availableSize);
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

        // 技のアイコンの行は横軸の数字の下。位置を決めてから置き場を並べ直す(位置の変更で置き場の配置がやり直しになる)。
        // 要素は見ている窓の中のものだけ(SynchronizeVisibleSkillMarkers)。
        var markerTop = plotBounds.Bottom + SecondLabelGap + GetSecondLabelHeight() + SkillMarkerGap;
        foreach (var element in _skillMarkerElements.Values)
        {
            Canvas.SetLeft(element, ToX(plotBounds, element.Marker.Seconds) - SkillMarkerSize / 2d);
            Canvas.SetTop(element, markerTop);
        }

        // 描く領域が無い(窓が小さすぎる)ときは軸の数字と同じく出さない。
        _skillMarkerLayer.Clip = hasPlot ? null : Geometry.Empty;
        _skillMarkerLayer.Arrange(new Rect(finalSize));
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

    private static void OnSkillMarkersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((MetricTimelineChart)d).ApplySkillMarkers();
    }

    private static void OnUnknownSkillIconBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var chart = (MetricTimelineChart)d;
        foreach (var element in chart._skillMarkerElements.Values)
        {
            element.SetUnknownIconFill(chart.UnknownSkillIconBrush);
        }
    }

    private static void OnSkillMarkerToolTipTagChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var chart = (MetricTimelineChart)d;
        foreach (var element in chart._skillMarkerElements.Values)
        {
            element.Tag = chart.SkillMarkerToolTipTag;
        }
    }

    private void ApplySkillMarkers()
    {
        var probeWatch = System.Diagnostics.Stopwatch.StartNew();
        var (created, removed) = SynchronizeVisibleSkillMarkers();
        InvalidateMeasure();
        InvalidateArrange();
        StarResonanceDps.App.Diagnostics.HistorySwitchProbe.Note(
            $"markers total={SkillMarkers?.Count ?? 0} elements={_skillMarkerElements.Count} created={created} removed={removed} " +
            $"took={probeWatch.Elapsed.TotalMilliseconds:0.0}ms");
    }

    /// <summary>
    /// 技のアイコンの要素を、見ている窓 [<see cref="HorizontalOffset"/>, + <see cref="VisibleSeconds"/>] の中の時刻の技の開始の分だけにする
    /// (横軸の数字と同じ扱い。端のアイコンは半分が描く領域の左右の余白へはみ出す)。
    /// 要素は一覧の何番目かで持ち、窓に入った番目は作り、出た番目は外し、残る番目は使い回す(中身が変わったところだけ差し替える)。
    /// 作り直さないのは、マウスの下の要素が入れ替わると TIPS が閉じるため。
    /// 窓の中かは一覧の全部を見て決める(並び順に頼らない)。重なったら後の開始を上に描くよう、Z の順は番目で決める(足した順にしない)。
    /// </summary>
    private (int Created, int Removed) SynchronizeVisibleSkillMarkers()
    {
        var markers = SkillMarkers ?? Array.Empty<MetricTimelineSkillMarker>();
        var start = HorizontalOffset;
        var end = start + VisibleSeconds;
        var created = 0;
        var removed = 0;

        foreach (var (index, element) in _skillMarkerElements.ToArray())
        {
            if (index < markers.Count && IsInVisibleWindow(markers[index].Seconds, start, end))
            {
                continue;
            }

            _skillMarkerLayer.Children.Remove(element);
            _skillMarkerElements.Remove(index);
            removed++;
        }

        for (var index = 0; index < markers.Count; index++)
        {
            var marker = markers[index];
            if (!IsInVisibleWindow(marker.Seconds, start, end))
            {
                continue;
            }

            if (_skillMarkerElements.TryGetValue(index, out var element))
            {
                if (!string.Equals(element.Marker.IconPath, marker.IconPath, StringComparison.OrdinalIgnoreCase))
                {
                    element.SetIcon(marker.IconPath, this);
                }

                element.Update(marker);
                continue;
            }

            element = new SkillMarkerElement(marker, this);
            element.SetIcon(marker.IconPath, this);
            Panel.SetZIndex(element, index);
            _skillMarkerElements.Add(index, element);
            _skillMarkerLayer.Children.Add(element);
            created++;
        }

        return (created, removed);
    }

    private static bool IsInVisibleWindow(double seconds, double start, double end)
    {
        return seconds >= start && seconds <= end;
    }

    /// <summary>クラス不明のアイコンの形(<c>Icon.Profession.Unknown</c>)。白い塗りをこの形で抜く(クラスアイコンと同じ染め方)。</summary>
    private ImageBrush GetUnknownSkillIconMask()
    {
        if (_unknownSkillIconMask is null)
        {
            _unknownSkillIconMask = new ImageBrush((ImageSource)FindResource("Icon.Profession.Unknown"))
            {
                Stretch = Stretch.Uniform
            };
            _unknownSkillIconMask.Freeze();
        }

        return _unknownSkillIconMask;
    }

    /// <summary>
    /// 技のアイコンの画像。ファイルは <c>CombatIconResolver</c> が在ることを確かめたもの。
    /// 読めなかったら記録してクラス不明のアイコンで出す(グラフごと止めない)。
    /// </summary>
    private static ImageSource? LoadSkillIconImage(string path)
    {
        if (SkillIconImages.TryGetValue(path, out var cached))
        {
            return cached;
        }

        ImageSource? image = null;
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            image = bitmap;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load skill icon {Path}; the unknown-class icon is shown instead", path);
        }

        SkillIconImages[path] = image;
        return image;
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
        SynchronizeVisibleSkillMarkers();
        InvalidateMeasure();
        InvalidateArrange();
        _lineLayer.InvalidateVisual();
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
    /// (いちばん上の数字は線の高さを中心に置く)、下は横軸の数字の高さと間に技のアイコンの行(間と一辺)を足したもの、
    /// 右は横軸の数字の幅の半分(右端の数字は時刻を中心に置く)。アイコンの行は技が無くても空けておく(軸が上下に動かないように)。
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
        var bottom = secondLabelHeight + SecondLabelGap + SkillMarkerGap + SkillMarkerSize;
        return new Rect(
            left,
            top,
            Math.Max(size.Width - left - right, 0d),
            Math.Max(size.Height - top - bottom, 0d));
    }

    /// <summary>横軸の数字のいちばん高い高さ(測った後の値)。</summary>
    private double GetSecondLabelHeight()
    {
        var height = 0d;
        foreach (var label in _secondLabels)
        {
            height = Math.Max(height, label.DesiredSize.Height);
        }

        return height;
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

    /// <summary>
    /// 技のアイコン1つ。背景を透明で塗ってアイコンの枠全体でマウスを受ける。
    /// 中身はアイコンの画像か、クラス不明のアイコン(グラフカラーの「不明」の塗りを形で抜き、クラスアイコンと同じ影を付ける)。
    /// TIPS は <c>ToolTip.WidgetWindowInfo</c> で、色は Tag(ウィジェット)の窓のパレットから取る。
    /// </summary>
    private sealed class SkillMarkerElement : Border
    {
        private readonly TextBlock _toolTipText;
        private Rectangle? _unknownIcon;

        public SkillMarkerElement(MetricTimelineSkillMarker marker, MetricTimelineChart owner)
        {
            Marker = marker;
            Width = SkillMarkerSize;
            Height = SkillMarkerSize;
            Background = Brushes.Transparent;
            Tag = owner.SkillMarkerToolTipTag;

            _toolTipText = new TextBlock
            {
                Style = (Style)owner.FindResource("Text.WidgetWindow"),
                FontSize = SkillMarkerToolTipFontSize,
                Text = marker.Name
            };
            ToolTip = new ToolTip
            {
                Style = (Style)owner.FindResource("ToolTip.WidgetWindowInfo"),
                Content = _toolTipText
            };
        }

        public MetricTimelineSkillMarker Marker { get; private set; }

        public void Update(MetricTimelineSkillMarker marker)
        {
            Marker = marker;
            _toolTipText.Text = marker.Name;
        }

        /// <summary>中身を <paramref name="iconPath"/> の画像にする。パスが無いか読めなければクラス不明のアイコンにする。</summary>
        public void SetIcon(string? iconPath, MetricTimelineChart owner)
        {
            if (iconPath is not null && LoadSkillIconImage(iconPath) is { } image)
            {
                var picture = new Image
                {
                    Source = image,
                    Stretch = Stretch.Uniform
                };
                RenderOptions.SetBitmapScalingMode(picture, BitmapScalingMode.HighQuality);
                _unknownIcon = null;
                Child = picture;
                return;
            }

            _unknownIcon = new Rectangle
            {
                Fill = owner.UnknownSkillIconBrush,
                OpacityMask = owner.GetUnknownSkillIconMask(),
                Effect = (Effect)owner.FindResource("Effect.ProfessionIconShadow")
            };
            Child = _unknownIcon;
        }

        public void SetUnknownIconFill(Brush? brush)
        {
            if (_unknownIcon is not null)
            {
                _unknownIcon.Fill = brush;
            }
        }
    }
}

/// <summary>
/// 推移グラフの格子と軸の線。<b>窓の枠の層(<c>WidgetWindow.FrameOverlayHost</c>)へ差し込む</b>ので、
/// 分割線と同じく窓の不透明度で合成され、影は付かない。線は1px、色は <see cref="Brush"/>(分割線の色を束縛する)。
///
/// <para>
/// 位置は <see cref="Chart"/> の描く領域から求める。格子は描く領域の位置と大きさだけで決まる(点にも横のずらしにも依らない)ので、
/// 配置の後に描く領域の位置か大きさが変わったと気付いたとき(縦軸の数字の幅が変わった、ヘッダーを隠したなど)だけ描き直す。
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
