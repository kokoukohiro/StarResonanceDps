using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace StarResonanceDps.App.Behaviors;

/// <summary>
/// 選んだ範囲。行は一覧の項目の番号、位置は行の文字(<see cref="TextRowsSelectionBehavior"/>)の中の文字の位置。
/// <see cref="StartRow"/> / <see cref="StartOffset"/> が前で、<see cref="EndOffset"/> の文字は含まない。
/// </summary>
public readonly record struct TextRowsSelection(int StartRow, int StartOffset, int EndRow, int EndOffset)
{
    public bool IsEmpty => StartRow == EndRow && StartOffset == EndOffset;
}

/// <summary>
/// 仮想化した <see cref="ItemsControl"/> の行を、行をまたいで文字単位で選べるようにする振る舞い。
///
/// <para>
/// 使い方: 一覧に <c>TextRowsSelectionBehavior.BodyElementName</c>(本文の <see cref="TextBlock"/> の名前)を付ける。
/// 前置き(時刻など)の <see cref="TextBlock"/> があれば <c>PrefixElementName</c>、選んだ文字の地にする要素(行の作りの中で左寄せ・横幅いっぱいに置ける所)を
/// <c>HighlightElementName</c> に、行の作りの中の名前で渡す。地の色は行の作りが決め、この振る舞いは位置・幅・見え方だけを決める。
/// </para>
///
/// <para>
/// 行の文字は、前置きの文字と空白1つ(前置きが空なら無し)と、本文の文字(<see cref="Run"/> だけ数え、アイコンなどは数えない)。
/// 前置きが隠れている(<see cref="Visibility.Hidden"/>)行は、前置きの場所を行の先頭として扱う。
/// 選んだ文字をどう文字にするかは呼び出し側が決める(行の文字の作り方をこの決まりにそろえる)。
/// </para>
///
/// <para>
/// 押してなぞると選び、Shift+クリックで選択を広げ、ただのクリックで選択を解除する。なぞったまま一覧の上下の外へ出ると流れる。
/// 右クリックでは選択を変えない。押したときに一覧へキーボードの焦点を移す(窓がアクティブなら Ctrl+C などのコマンドが一覧に届く)。
/// </para>
///
/// <para>
/// 選択は「何行目の何文字目」で持つ。一覧が空になった(<see cref="NotifyCollectionChangedAction.Reset"/>)ら消す。
/// 地は見えている行(作られている行の部品)の分だけ動かす。DragDrop は使わず、マウスの捕捉だけで閉じた作りにする(<see cref="RowReorderBehavior"/> と同じ理由)。
/// </para>
/// </summary>
public static class TextRowsSelectionBehavior
{
    public static readonly DependencyProperty BodyElementNameProperty = DependencyProperty.RegisterAttached(
        "BodyElementName",
        typeof(string),
        typeof(TextRowsSelectionBehavior),
        new PropertyMetadata(null, OnBodyElementNameChanged));

    public static readonly DependencyProperty PrefixElementNameProperty = DependencyProperty.RegisterAttached(
        "PrefixElementName",
        typeof(string),
        typeof(TextRowsSelectionBehavior),
        new PropertyMetadata(null));

    public static readonly DependencyProperty HighlightElementNameProperty = DependencyProperty.RegisterAttached(
        "HighlightElementName",
        typeof(string),
        typeof(TextRowsSelectionBehavior),
        new PropertyMetadata(null));

    private static readonly DependencyProperty ControllerProperty = DependencyProperty.RegisterAttached(
        "Controller",
        typeof(TextRowsSelectionController),
        typeof(TextRowsSelectionBehavior),
        new PropertyMetadata(null));

    public static string? GetBodyElementName(DependencyObject obj)
    {
        return (string?)obj.GetValue(BodyElementNameProperty);
    }

    public static void SetBodyElementName(DependencyObject obj, string? value)
    {
        obj.SetValue(BodyElementNameProperty, value);
    }

    public static string? GetPrefixElementName(DependencyObject obj)
    {
        return (string?)obj.GetValue(PrefixElementNameProperty);
    }

    public static void SetPrefixElementName(DependencyObject obj, string? value)
    {
        obj.SetValue(PrefixElementNameProperty, value);
    }

    public static string? GetHighlightElementName(DependencyObject obj)
    {
        return (string?)obj.GetValue(HighlightElementNameProperty);
    }

    public static void SetHighlightElementName(DependencyObject obj, string? value)
    {
        obj.SetValue(HighlightElementNameProperty, value);
    }

    /// <summary>今選んでいる範囲。選択が無いか空なら false。</summary>
    public static bool TryGetSelection(ItemsControl itemsControl, out TextRowsSelection selection)
    {
        if (itemsControl.GetValue(ControllerProperty) is TextRowsSelectionController controller
            && controller.Selection is { IsEmpty: false } current)
        {
            selection = current;
            return true;
        }

        selection = default;
        return false;
    }

    private static void OnBodyElementNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ItemsControl itemsControl)
        {
            throw new InvalidOperationException($"{nameof(TextRowsSelectionBehavior)} can only be attached to an ItemsControl.");
        }

        if (itemsControl.GetValue(ControllerProperty) is TextRowsSelectionController previous)
        {
            previous.Detach();
            itemsControl.ClearValue(ControllerProperty);
        }

        if (e.NewValue is string { Length: > 0 })
        {
            var controller = new TextRowsSelectionController(itemsControl);
            controller.Attach();
            itemsControl.SetValue(ControllerProperty, controller);
        }
    }
}

/// <summary>1つの一覧の文字の選択の状態と処理(<see cref="TextRowsSelectionBehavior"/>)。</summary>
internal sealed class TextRowsSelectionController
{
    /// <summary>なぞったまま一覧の外へ出たとき、1回に流す最大の量。外へ遠いほどこの値に近づく。</summary>
    private const double AutoScrollMaxStep = 12d;

    /// <summary>流す量がいちばん大きくなる、一覧の外への距離。</summary>
    private const double AutoScrollFullSpeedDistance = 40d;

    private static readonly TimeSpan AutoScrollInterval = TimeSpan.FromMilliseconds(16);

    private readonly ItemsControl _itemsControl;
    private readonly DispatcherTimer _autoScrollTimer;
    private ScrollViewer? _scrollViewer;
    private Panel? _itemsHost;
    private INotifyCollectionChanged? _items;
    private TextPosition? _anchor;
    private TextPosition? _active;
    private bool _isDragging;

    /// <summary>地を出している行の部品がある。選択が無い間に地を消し終えたら、行の作り直しのたびに見直さない。</summary>
    private bool _hasShownHighlights;

    public TextRowsSelectionController(ItemsControl itemsControl)
    {
        _itemsControl = itemsControl;
        _autoScrollTimer = new DispatcherTimer(DispatcherPriority.Input)
        {
            Interval = AutoScrollInterval
        };
        _autoScrollTimer.Tick += AutoScrollTimer_Tick;
    }

    /// <summary>今選んでいる範囲(前から後ろの順)。選び始めていなければ null。</summary>
    public TextRowsSelection? Selection
    {
        get
        {
            if (_anchor is not { } anchor || _active is not { } active)
            {
                return null;
            }

            var (start, end) = anchor.CompareTo(active) <= 0 ? (anchor, active) : (active, anchor);
            return new TextRowsSelection(start.Row, start.Offset, end.Row, end.Offset);
        }
    }

    public void Attach()
    {
        _itemsControl.PreviewMouseLeftButtonDown += ItemsControl_PreviewMouseLeftButtonDown;
        _itemsControl.PreviewMouseMove += ItemsControl_PreviewMouseMove;
        _itemsControl.PreviewMouseLeftButtonUp += ItemsControl_PreviewMouseLeftButtonUp;
        _itemsControl.LostMouseCapture += ItemsControl_LostMouseCapture;
        _itemsControl.LayoutUpdated += ItemsControl_LayoutUpdated;
        _items = _itemsControl.Items;
        _items.CollectionChanged += Items_CollectionChanged;
    }

    public void Detach()
    {
        EndDrag();
        _itemsControl.PreviewMouseLeftButtonDown -= ItemsControl_PreviewMouseLeftButtonDown;
        _itemsControl.PreviewMouseMove -= ItemsControl_PreviewMouseMove;
        _itemsControl.PreviewMouseLeftButtonUp -= ItemsControl_PreviewMouseLeftButtonUp;
        _itemsControl.LostMouseCapture -= ItemsControl_LostMouseCapture;
        _itemsControl.LayoutUpdated -= ItemsControl_LayoutUpdated;
        if (_items is not null)
        {
            _items.CollectionChanged -= Items_CollectionChanged;
            _items = null;
        }

        _autoScrollTimer.Tick -= AutoScrollTimer_Tick;
    }

    private void ItemsControl_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 1 || !TryHitTest(e.GetPosition(_itemsControl), out var hit))
        {
            return;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0 && _anchor is not null)
        {
            _active = hit;
        }
        else
        {
            _anchor = hit;
            _active = hit;
        }

        _isDragging = _itemsControl.CaptureMouse();
        _itemsControl.Focus();
        e.Handled = true;
        RefreshHighlights();
    }

    private void ItemsControl_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        var point = e.GetPosition(_itemsControl);
        if (point.Y < 0d || point.Y > _itemsControl.ActualHeight)
        {
            _autoScrollTimer.Start();
        }
        else
        {
            _autoScrollTimer.Stop();
        }

        UpdateActive(point);
    }

    private void ItemsControl_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        EndDrag();
        e.Handled = true;
    }

    private void ItemsControl_LostMouseCapture(object sender, MouseEventArgs e)
    {
        _isDragging = false;
        _autoScrollTimer.Stop();
    }

    /// <summary>行の部品の作り直し(流したときの使い回しなど)や文字の変化に追いかける。選択も地も無ければ何もしない。</summary>
    private void ItemsControl_LayoutUpdated(object? sender, EventArgs e)
    {
        if (Selection is { IsEmpty: false } || _hasShownHighlights)
        {
            RefreshHighlights();
        }
    }

    private void Items_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            EndDrag();
            _anchor = null;
            _active = null;
            RefreshHighlights();
        }
    }

    private void EndDrag()
    {
        _isDragging = false;
        _autoScrollTimer.Stop();
        if (ReferenceEquals(Mouse.Captured, _itemsControl))
        {
            _itemsControl.ReleaseMouseCapture();
        }
    }

    /// <summary>なぞったまま一覧の上下の外にいる間、外へ遠いほど速く流し、端の行の文字までを選ぶ。</summary>
    private void AutoScrollTimer_Tick(object? sender, EventArgs e)
    {
        if (!_isDragging || GetScrollViewer() is not { } scrollViewer)
        {
            _autoScrollTimer.Stop();
            return;
        }

        var point = Mouse.GetPosition(_itemsControl);
        var distance = point.Y < 0d
            ? point.Y
            : point.Y > _itemsControl.ActualHeight
                ? point.Y - _itemsControl.ActualHeight
                : 0d;
        if (distance == 0d)
        {
            _autoScrollTimer.Stop();
            return;
        }

        var step = Math.Sign(distance) * AutoScrollMaxStep * Math.Min(1d, Math.Abs(distance) / AutoScrollFullSpeedDistance);
        var offset = Math.Clamp(scrollViewer.VerticalOffset + step, 0d, scrollViewer.ScrollableHeight);
        if (offset != scrollViewer.VerticalOffset)
        {
            scrollViewer.ScrollToVerticalOffset(offset);
            scrollViewer.UpdateLayout();
        }

        UpdateActive(point);
    }

    private void UpdateActive(Point point)
    {
        if (TryHitTest(point, out var hit) && hit != _active)
        {
            _active = hit;
            RefreshHighlights();
        }
    }

    /// <summary>
    /// 一覧の座標の点が指す、行と文字の位置。見えている行より上なら最初の行の先頭、下なら最後の行の末尾。
    /// 作られている行が無ければ false。
    /// </summary>
    private bool TryHitTest(Point point, out TextPosition position)
    {
        position = default;
        var rows = GetRealizedRows();
        if (rows.Count == 0)
        {
            return false;
        }

        if (point.Y < rows[0].Top)
        {
            position = new TextPosition(rows[0].Index, 0);
            return true;
        }

        var row = rows.Find(candidate => point.Y < candidate.Bottom);
        if (row is null)
        {
            var last = rows[^1];
            position = new TextPosition(last.Index, GetRowLength(last.Container));
            return true;
        }

        position = new TextPosition(row.Index, GetRowOffsetAt(row.Container, point));
        return true;
    }

    /// <summary>行の中の点が指す文字の位置。本文より左は前置き(隠れていれば行の先頭、前置きと本文の間なら本文の先頭)。</summary>
    private int GetRowOffsetAt(FrameworkElement container, Point point)
    {
        if (FindPart<TextBlock>(container, TextRowsSelectionBehavior.GetBodyElementName(_itemsControl)) is not { } body)
        {
            return 0;
        }

        var prefix = FindPrefix(container);
        var prefixLength = GetPrefixLength(prefix);
        var bodyPoint = _itemsControl.TranslatePoint(point, body);
        if (bodyPoint.X >= 0d)
        {
            return prefixLength + GetTextOffsetAt(body, bodyPoint);
        }

        if (prefix is null || prefixLength == 0 || prefix.Visibility != Visibility.Visible)
        {
            return 0;
        }

        var prefixPoint = _itemsControl.TranslatePoint(point, prefix);
        return prefixPoint.X > prefix.ActualWidth
            ? prefixLength
            : GetTextOffsetAt(prefix, prefixPoint);
    }

    /// <summary>見えている行の地を、選んだ文字の範囲に合わせる。選んでいない行は地を消す。</summary>
    private void RefreshHighlights()
    {
        var selection = Selection;
        var highlightName = TextRowsSelectionBehavior.GetHighlightElementName(_itemsControl);
        var hasShownHighlights = false;
        foreach (var row in GetRealizedRows())
        {
            if (FindPart<FrameworkElement>(row.Container, highlightName) is not { } highlight)
            {
                continue;
            }

            if (selection is not { IsEmpty: false } range
                || row.Index < range.StartRow
                || row.Index > range.EndRow
                || !TryGetHighlightBounds(row.Container, highlight, row.Index, range, out var left, out var width))
            {
                HideHighlight(highlight);
                continue;
            }

            ShowHighlight(highlight, left, width);
            hasShownHighlights = true;
        }

        _hasShownHighlights = hasShownHighlights;
    }

    /// <summary>行の中の選んだ文字の範囲の、地を置く要素の親の座標での左端と幅。選んだ文字が無ければ false。</summary>
    private bool TryGetHighlightBounds(
        FrameworkElement container,
        FrameworkElement highlight,
        int rowIndex,
        TextRowsSelection range,
        out double left,
        out double width)
    {
        left = 0d;
        width = 0d;
        if (VisualTreeHelper.GetParent(highlight) is not UIElement reference
            || FindPart<TextBlock>(container, TextRowsSelectionBehavior.GetBodyElementName(_itemsControl)) is not { } body)
        {
            return false;
        }

        var prefix = FindPrefix(container);
        var prefixLength = GetPrefixLength(prefix);
        var rowLength = prefixLength + GetTextLength(body);
        var start = rowIndex == range.StartRow ? Math.Clamp(range.StartOffset, 0, rowLength) : 0;
        var end = rowIndex == range.EndRow ? Math.Clamp(range.EndOffset, start, rowLength) : rowLength;
        if (start >= end)
        {
            return false;
        }

        var startX = GetRowX(prefix, prefixLength, body, start, reference);
        var endX = GetRowX(prefix, prefixLength, body, end, reference);
        left = Math.Max(0d, startX);
        width = Math.Max(0d, endX - left);
        return width > 0d;
    }

    /// <summary>行の文字の位置の横の位置(<paramref name="reference"/> の座標)。前置きが隠れていれば、前置きの中の位置は前置きの左端。</summary>
    private static double GetRowX(TextBlock? prefix, int prefixLength, TextBlock body, int offset, UIElement reference)
    {
        if (offset >= prefixLength)
        {
            return GetTextX(body, offset - prefixLength, reference);
        }

        if (prefix is null)
        {
            return GetTextX(body, 0, reference);
        }

        return prefix.Visibility == Visibility.Visible
            ? GetTextX(prefix, Math.Min(offset, GetTextLength(prefix)), reference)
            : prefix.TranslatePoint(new Point(0d, 0d), reference).X;
    }

    private static void ShowHighlight(FrameworkElement highlight, double left, double width)
    {
        var margin = new Thickness(left, 0d, 0d, 0d);
        if (highlight.Margin != margin)
        {
            highlight.Margin = margin;
        }

        if (highlight.Width != width)
        {
            highlight.Width = width;
        }

        if (highlight.Visibility != Visibility.Visible)
        {
            highlight.Visibility = Visibility.Visible;
        }
    }

    private static void HideHighlight(FrameworkElement highlight)
    {
        if (highlight.Visibility != Visibility.Collapsed)
        {
            highlight.Visibility = Visibility.Collapsed;
        }
    }

    private TextBlock? FindPrefix(FrameworkElement container)
    {
        return TextRowsSelectionBehavior.GetPrefixElementName(_itemsControl) is { Length: > 0 } prefixName
            ? FindPart<TextBlock>(container, prefixName)
            : null;
    }

    /// <summary>行の文字の長さ(前置きと空白1つと本文)。</summary>
    private int GetRowLength(FrameworkElement container)
    {
        return FindPart<TextBlock>(container, TextRowsSelectionBehavior.GetBodyElementName(_itemsControl)) is { } body
            ? GetPrefixLength(FindPrefix(container)) + GetTextLength(body)
            : 0;
    }

    /// <summary>前置きの長さ。前置きの文字と、本文との間の空白1つ。前置きが無いか空なら 0。</summary>
    private static int GetPrefixLength(TextBlock? prefix)
    {
        var length = prefix is null ? 0 : GetTextLength(prefix);
        return length > 0 ? length + 1 : 0;
    }

    /// <summary><see cref="TextBlock"/> の文字の長さ。<see cref="Run"/> だけを数える。</summary>
    private static int GetTextLength(TextBlock textBlock)
    {
        var length = 0;
        foreach (var inline in textBlock.Inlines)
        {
            if (inline is Run run)
            {
                length += run.Text.Length;
            }
        }

        return length;
    }

    /// <summary><see cref="TextBlock"/> の中の点に一番近い文字の位置(<see cref="Run"/> の文字だけを数えた位置)。</summary>
    private static int GetTextOffsetAt(TextBlock textBlock, Point point)
    {
        var clamped = new Point(
            Math.Max(0d, point.X),
            Math.Clamp(point.Y, 0d, Math.Max(0d, textBlock.ActualHeight - 1d)));
        if (textBlock.GetPositionFromPoint(clamped, snapToText: true) is not { } pointer)
        {
            return clamped.X <= 0d ? 0 : GetTextLength(textBlock);
        }

        var offset = 0;
        foreach (var inline in textBlock.Inlines)
        {
            if (inline is Run run)
            {
                if (pointer.CompareTo(run.ContentStart) <= 0)
                {
                    return offset;
                }

                if (pointer.CompareTo(run.ContentEnd) <= 0)
                {
                    return offset + run.ContentStart.GetOffsetToPosition(pointer);
                }

                offset += run.Text.Length;
            }
            else if (pointer.CompareTo(inline.ElementEnd) <= 0)
            {
                return offset;
            }
        }

        return offset;
    }

    /// <summary><see cref="TextBlock"/> の文字の位置(<see cref="Run"/> の文字だけを数えた位置)の横の位置(<paramref name="reference"/> の座標)。</summary>
    private static double GetTextX(TextBlock textBlock, int offset, UIElement reference)
    {
        TextPointer? pointer = null;
        var remaining = offset;
        foreach (var inline in textBlock.Inlines)
        {
            if (inline is not Run run)
            {
                continue;
            }

            if (remaining <= run.Text.Length)
            {
                pointer = run.ContentStart.GetPositionAtOffset(remaining);
                break;
            }

            remaining -= run.Text.Length;
        }

        pointer ??= textBlock.ContentEnd;
        var x = Math.Clamp(pointer.GetCharacterRect(LogicalDirection.Forward).Left, 0d, textBlock.ActualWidth);
        return textBlock.TranslatePoint(new Point(x, 0d), reference).X;
    }

    /// <summary>作られていて見えている行の部品(一覧の座標の上端と下端つき)。行の番号の順。</summary>
    private List<RealizedRow> GetRealizedRows()
    {
        var rows = new List<RealizedRow>();
        if (GetItemsHost() is not { } itemsHost)
        {
            return rows;
        }

        foreach (UIElement child in itemsHost.Children)
        {
            if (child is not FrameworkElement { IsVisible: true } container)
            {
                continue;
            }

            var index = _itemsControl.ItemContainerGenerator.IndexFromContainer(container);
            if (index < 0)
            {
                continue;
            }

            var top = container.TranslatePoint(new Point(0d, 0d), _itemsControl).Y;
            rows.Add(new RealizedRow(index, container, top, top + container.ActualHeight));
        }

        rows.Sort((left, right) => left.Index.CompareTo(right.Index));
        return rows;
    }

    private Panel? GetItemsHost()
    {
        return _itemsHost ??= FindDescendant<Panel>(_itemsControl, panel => panel.IsItemsHost);
    }

    private ScrollViewer? GetScrollViewer()
    {
        return _scrollViewer ??= FindDescendant<ScrollViewer>(_itemsControl, _ => true);
    }

    /// <summary>行の部品の中の、その名前の要素。名前が空か見つからなければ null。</summary>
    private static T? FindPart<T>(DependencyObject container, string? name)
        where T : FrameworkElement
    {
        return string.IsNullOrEmpty(name)
            ? null
            : FindDescendant<T>(container, element => element.Name == name);
    }

    private static T? FindDescendant<T>(DependencyObject start, Func<T, bool> predicate)
        where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(start);
        for (var index = 0; index < count; index++)
        {
            var child = VisualTreeHelper.GetChild(start, index);
            if (child is T found && predicate(found))
            {
                return found;
            }

            if (FindDescendant(child, predicate) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }

    /// <summary>行と、行の文字の中の位置。</summary>
    private readonly record struct TextPosition(int Row, int Offset) : IComparable<TextPosition>
    {
        public int CompareTo(TextPosition other)
        {
            return Row != other.Row ? Row.CompareTo(other.Row) : Offset.CompareTo(other.Offset);
        }
    }

    private sealed record RealizedRow(int Index, FrameworkElement Container, double Top, double Bottom);
}
