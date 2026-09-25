using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using StarResonanceDps.App.ViewModels;

namespace StarResonanceDps.App.Views.Widgets;

/// <summary>
/// ステータス詳細。自分の実体に届いた属性を番号順に1列で並べる。
///
/// <para>
/// 縦スクロールは<b>ウィンドウ枠側の細いスクロールバー</b>に繋ぐ(バフ一覧と同じ)。
/// 内側に WPF のスクロールバーを出すと他のウィジェットと見た目が揃わない。
/// </para>
/// </summary>
public partial class PlayerStatusWidgetView : UserControl, IWidgetVerticalScrollContent
{
    public PlayerStatusWidgetView()
    {
        InitializeComponent();
    }

    public event EventHandler? VerticalScrollMetricsChanged;

    public WidgetVerticalScrollMetrics GetVerticalScrollMetrics()
    {
        var maximum = Math.Max(StatusListScrollViewer.ScrollableHeight, 0d);
        var viewport = Math.Max(StatusListScrollViewer.ViewportHeight, 0d);

        return new WidgetVerticalScrollMetrics(
            maximum,
            viewport,
            Math.Min(StatusListScrollViewer.VerticalOffset, maximum),
            Math.Max(viewport * 0.9d, 1d),
            // 行1つぶん。行の高さ(31)と合わせる。
            31d);
    }

    public void SetVerticalScrollOffset(double verticalOffset)
    {
        var maximum = Math.Max(StatusListScrollViewer.ScrollableHeight, 0d);
        var offset = double.IsFinite(verticalOffset)
            ? Math.Clamp(verticalOffset, 0d, maximum)
            : 0d;

        StatusListScrollViewer.ScrollToVerticalOffset(offset);
    }

    private void PlayerStatusWidgetView_Loaded(object sender, RoutedEventArgs e)
    {
        AttachDropIndicator();
        NotifyVerticalScrollMetricsChanged();
    }

    private void PlayerStatusWidgetView_Unloaded(object sender, RoutedEventArgs e)
    {
        DetachDropIndicator();
    }

    /// <summary>
    /// 落とし先の線を<b>枠のレイヤー</b>へ入れる。中身のレイヤーに置くと、半透明の背景の
    /// 上にベタで乗って暗く濁るため(閉じる印や分割線は枠の側にある)。
    /// </summary>
    private void AttachDropIndicator()
    {
        if (_dropIndicator is not null)
        {
            return;
        }

        if (Window.GetWindow(this) is not WidgetWindow owner
            || TryFindResource("DropIndicator") is not Rectangle indicator)
        {
            return;
        }

        _dropIndicator = indicator;
        _frameOverlayOwner = owner;
        owner.FrameOverlayHost.Content = indicator;
    }

    private void DetachDropIndicator()
    {
        if (_frameOverlayOwner is not null
            && ReferenceEquals(_frameOverlayOwner.FrameOverlayHost.Content, _dropIndicator))
        {
            _frameOverlayOwner.FrameOverlayHost.Content = null;
        }

        _dropIndicator = null;
        _frameOverlayOwner = null;
    }

    private void PlayerStatusWidgetView_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void PlayerStatusWidgetView_DataContextChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void StatusListScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void NotifyVerticalScrollMetricsChanged()
    {
        VerticalScrollMetricsChanged?.Invoke(this, EventArgs.Empty);
    }

    // ---- 行を掴んで並べ替える ----
    //
    // DragDrop.DoDragDrop は使わない。ウィジェットは階層化ウィンドウで、OLE ドラッグが
    // その上でどう振る舞うか確かめられないため、マウスの捕捉だけで閉じた作りにする。

    private Rectangle? _dropIndicator;
    private WidgetWindow? _frameOverlayOwner;
    private ButtonBase? _dragRow;
    private Point _dragOrigin;
    private bool _isDraggingRow;
    private int _dropIndex = -1;

    private void StatusRow_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ButtonBase row)
        {
            return;
        }

        _dragRow = row;
        _dragOrigin = e.GetPosition(this);
        _isDraggingRow = false;
        _dropIndex = -1;
    }

    protected override void OnPreviewMouseMove(MouseEventArgs e)
    {
        base.OnPreviewMouseMove(e);

        if (_dragRow is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var position = e.GetPosition(this);
        if (!_isDraggingRow)
        {
            if (Math.Abs(position.Y - _dragOrigin.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            _isDraggingRow = true;
            _dragRow.Opacity = 0.4d;
            CaptureMouse();

            // 掴んでいる間は行の作り直しを止める。止めないと属性の更新でコンテナが差し替わる。
            (DataContext as PlayerStatusWidgetViewModel)?.BeginRowDrag();
        }

        UpdateDropIndicator(position);
    }

    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonUp(e);

        if (!_isDraggingRow)
        {
            _dragRow = null;
            return;
        }

        var from = IndexOfRow(_dragRow);
        var to = _dropIndex;
        EndRowDrag();

        if (from >= 0 && to >= 0 && DataContext is PlayerStatusWidgetViewModel viewModel)
        {
            viewModel.MoveRow(from, to);
        }
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);

        // <b>行の Button が手放したときも上がってくる</b>(LostMouseCapture はバブリング)。
        // ButtonBase は押下で自分をキャプチャするので、こちらが CaptureMouse した瞬間に
        // 行がキャプチャを失う。自分が失ったときだけ畳まないと、開始と同時に終わる。
        if (ReferenceEquals(e.OriginalSource, this))
        {
            EndRowDrag();
        }
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        if (_isDraggingRow && e.Key == Key.Escape)
        {
            EndRowDrag();
            e.Handled = true;
        }
    }

    /// <summary>掴んだ状態を解く。並びは変えない(確定は呼び出し側で行う)。</summary>
    private void EndRowDrag()
    {
        var wasDragging = _isDraggingRow;

        if (_dragRow is not null)
        {
            _dragRow.Opacity = 1d;
        }

        _dragRow = null;
        _isDraggingRow = false;
        _dropIndex = -1;
        if (_dropIndicator is not null)
        {
            _dropIndicator.Visibility = Visibility.Collapsed;
        }

        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }

        if (wasDragging)
        {
            (DataContext as PlayerStatusWidgetViewModel)?.EndRowDrag();
        }
    }

    /// <summary>マウスの位置から落とし先を決め、線をそこへ出す。</summary>
    private void UpdateDropIndicator(Point position)
    {
        var rows = GetRowContainers();
        if (_dropIndicator is null || _frameOverlayOwner is null || rows.Count == 0)
        {
            if (_dropIndicator is not null)
            {
                _dropIndicator.Visibility = Visibility.Collapsed;
            }

            return;
        }

        var index = -1;
        var top = 0d;
        for (var i = 0; i < rows.Count; i++)
        {
            var bounds = GetRowBounds(rows[i]);
            if (position.Y < bounds.Bottom || i == rows.Count - 1)
            {
                index = i;
                // 行の下半分なら、その行の後ろへ落とす。
                top = position.Y > bounds.Top + (bounds.Height / 2d) ? bounds.Bottom : bounds.Top;
                break;
            }
        }

        if (index < 0)
        {
            _dropIndicator.Visibility = Visibility.Collapsed;
            return;
        }

        // 線は別のレイヤーにいるので、行の位置をそちらの座標系へ直す。
        var host = _frameOverlayOwner.FrameOverlayHost;
        var origin = TransformToVisual(host).Transform(new Point(0d, top));

        _dropIndex = index;
        _dropIndicator.Width = Math.Max(ActualWidth - 5d, 0d);
        _dropIndicator.Margin = new Thickness(
            origin.X + 5d,
            Math.Max(origin.Y - 1d, 0d),
            0d,
            0d);
        _dropIndicator.Visibility = Visibility.Visible;
    }

    private List<ButtonBase> GetRowContainers()
    {
        var rows = new List<ButtonBase>();
        CollectRows(StatusListScrollViewer, rows);
        return rows;
    }

    private static void CollectRows(DependencyObject parent, List<ButtonBase> rows)
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is ButtonBase row)
            {
                rows.Add(row);
                continue;
            }

            CollectRows(child, rows);
        }
    }

    private int IndexOfRow(ButtonBase? row)
    {
        return row is null ? -1 : GetRowContainers().IndexOf(row);
    }

    private Rect GetRowBounds(ButtonBase row)
    {
        var topLeft = row.TransformToAncestor(this).Transform(new Point(0d, 0d));
        return new Rect(topLeft, new Size(row.ActualWidth, row.ActualHeight));
    }
}
