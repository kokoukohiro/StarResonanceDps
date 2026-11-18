// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace StarResonanceDps.App.Behaviors;

/// <summary>
/// ウィンドウの拡大率が100%以外のウィジェットの窓で、影の付いた文字の左上を画面の画素へそろえる。
/// 窓は100%の配置のまま変形で拡大するので、行が画素の途中に来ると影の付いた文字がにじむ(影の効果の中間の絵が端数の位置で重ねられる)。
/// 配置の後に、文字の描く位置だけを最大0.5画素動かす(<c>RenderTransform</c>。配置はやり直さない)。文字がもともと持っている移動の上に足す。
/// そろえる文字は <see cref="IsTargetProperty"/> が真のもので、影の付いた文字のスタイル(<c>Text.WidgetWindow</c>)が付ける
/// (プラグインのウィジェットもこのスタイルを使えば入る)。100%の窓では何もしない。
/// </summary>
public sealed class ScaledTextPixelSnapping
{
    /// <summary>そろえる文字か。画面に出たとき・消えたときに、そろえている最中の窓の一覧へ出し入れする。</summary>
    public static readonly DependencyProperty IsTargetProperty = DependencyProperty.RegisterAttached(
        "IsTarget",
        typeof(bool),
        typeof(ScaledTextPixelSnapping),
        new PropertyMetadata(false, OnIsTargetChanged));

    /// <summary>そろえている最中の窓。</summary>
    private static readonly Dictionary<Window, ScaledTextPixelSnapping> ActiveWindows = [];

    private readonly Window _window;
    private readonly Dictionary<FrameworkElement, SnapState> _targets = [];
    private bool _isActive;

    /// <summary>文字が増えた後のそろえ直しを予約したか(まとめて1回にする)。</summary>
    private bool _isSnapScheduled;

    public ScaledTextPixelSnapping(Window window)
    {
        _window = window;
    }

    public static bool GetIsTarget(DependencyObject obj)
    {
        return (bool)obj.GetValue(IsTargetProperty);
    }

    public static void SetIsTarget(DependencyObject obj, bool value)
    {
        obj.SetValue(IsTargetProperty, value);
    }

    /// <summary>そろえ始める。窓に今出ている文字を集め、以後は出入りと配置のたびに当て直す。</summary>
    public void Activate()
    {
        if (_isActive)
        {
            return;
        }

        _isActive = true;
        ActiveWindows[_window] = this;
        _window.LayoutUpdated += Window_LayoutUpdated;
        CollectTargets(_window);
        SnapAll();
    }

    /// <summary>そろえるのをやめ、動かした文字を元の移動に戻して一覧を手放す。</summary>
    public void Deactivate()
    {
        if (!_isActive)
        {
            return;
        }

        _isActive = false;
        ActiveWindows.Remove(_window);
        _window.LayoutUpdated -= Window_LayoutUpdated;
        foreach (var (target, state) in _targets)
        {
            state.Restore(target);
        }

        _targets.Clear();
    }

    private static void OnIsTargetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element)
        {
            return;
        }

        element.Loaded -= Target_Loaded;
        element.Unloaded -= Target_Unloaded;
        if (e.NewValue is true)
        {
            element.Loaded += Target_Loaded;
            element.Unloaded += Target_Unloaded;
        }
    }

    private static void Target_Loaded(object sender, RoutedEventArgs e)
    {
        if (ActiveWindows.Count == 0
            || sender is not FrameworkElement target
            || Window.GetWindow(target) is not { } window
            || !ActiveWindows.TryGetValue(window, out var snapping))
        {
            return;
        }

        if (snapping._targets.TryAdd(target, new SnapState()))
        {
            snapping.ScheduleSnap();
        }
    }

    private static void Target_Unloaded(object sender, RoutedEventArgs e)
    {
        if (ActiveWindows.Count == 0 || sender is not FrameworkElement target)
        {
            return;
        }

        foreach (var snapping in ActiveWindows.Values)
        {
            if (snapping._targets.Remove(target, out var state))
            {
                state.Restore(target);
                return;
            }
        }
    }

    /// <summary>
    /// 増えた文字をそろえる。文字が画面に出る合図は配置の後に来るので、次の配置を待たずに当てる
    /// (配置し直されない窓では、待つといつまでもそろわない)。
    /// </summary>
    private void ScheduleSnap()
    {
        if (_isSnapScheduled)
        {
            return;
        }

        _isSnapScheduled = true;
        _window.Dispatcher.BeginInvoke(() =>
        {
            _isSnapScheduled = false;
            if (_isActive)
            {
                SnapAll();
            }
        }, DispatcherPriority.Loaded);
    }

    private void CollectTargets(DependencyObject node)
    {
        if (node is FrameworkElement element && GetIsTarget(element))
        {
            _targets.TryAdd(element, new SnapState());
        }

        var count = VisualTreeHelper.GetChildrenCount(node);
        for (var i = 0; i < count; i++)
        {
            CollectTargets(VisualTreeHelper.GetChild(node, i));
        }
    }

    private void Window_LayoutUpdated(object? sender, EventArgs e)
    {
        SnapAll();
    }

    private void SnapAll()
    {
        if (PresentationSource.FromVisual(_window) is not { CompositionTarget: { } compositionTarget })
        {
            return;
        }

        var toDevice = compositionTarget.TransformToDevice;
        foreach (var (target, state) in _targets)
        {
            if (target.Effect is null || !target.IsVisible)
            {
                state.Restore(target);
                continue;
            }

            state.Snap(target, _window, toDevice);
        }
    }

    /// <summary>1つの文字の動かした量と、動かす前の移動。</summary>
    private sealed class SnapState
    {
        /// <summary>そろえるための移動(親の座標の単位)。null ならまだ動かしていない。</summary>
        private TranslateTransform? _snap;

        /// <summary>動かす前の文字自身の移動の値(ローカルの値が無ければ <see cref="DependencyProperty.UnsetValue"/>)。</summary>
        private object? _originalLocal;

        public void Snap(FrameworkElement target, Window window, Matrix toDevice)
        {
            if (VisualTreeHelper.GetParent(target) is not Visual parent
                || !parent.IsDescendantOf(window)
                || parent.TransformToAncestor(window) is not Transform parentTransform)
            {
                return;
            }

            // 親の座標の1単位が画面の何画素か(拡大は縦横同じ倍率で、回転は無い)。
            var parentToDevice = parentTransform.Value * toDevice;
            var scaleX = parentToDevice.M11;
            var scaleY = parentToDevice.M22;
            if (scaleX <= 0d || scaleY <= 0d)
            {
                return;
            }

            // 今の左上(そろえる移動を含む)から、そろえる前の左上を出し、画素の格子の一番近い所までの移動を決める。
            var current = toDevice.Transform(target.TransformToAncestor(window).Transform(new Point(0d, 0d)));
            var appliedX = (_snap?.X ?? 0d) * scaleX;
            var appliedY = (_snap?.Y ?? 0d) * scaleY;
            var layoutX = current.X - appliedX;
            var layoutY = current.Y - appliedY;
            var targetX = Math.Round(layoutX) - layoutX;
            var targetY = Math.Round(layoutY) - layoutY;
            if (Math.Abs(targetX - appliedX) < 0.01d && Math.Abs(targetY - appliedY) < 0.01d)
            {
                return;
            }

            if (_snap is null)
            {
                // 移動を束縛で決めている文字は、元に戻せないので動かさない。
                var originalLocal = target.ReadLocalValue(UIElement.RenderTransformProperty);
                if (originalLocal is Expression)
                {
                    return;
                }

                _snap = new TranslateTransform();
                _originalLocal = originalLocal;
                var original = target.RenderTransform;
                target.RenderTransform = original is null || original.Value.IsIdentity
                    ? _snap
                    : new TransformGroup { Children = { original, _snap } };
            }

            _snap.X = targetX / scaleX;
            _snap.Y = targetY / scaleY;
        }

        public void Restore(FrameworkElement target)
        {
            if (_snap is null)
            {
                return;
            }

            if (_originalLocal == DependencyProperty.UnsetValue)
            {
                target.ClearValue(UIElement.RenderTransformProperty);
            }
            else
            {
                target.RenderTransform = (Transform?)_originalLocal;
            }

            _snap = null;
            _originalLocal = null;
        }
    }
}
