using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.ViewModels;

namespace StarResonanceDps.App.Views.Widgets;

public partial class WidgetWindow : Window
{
    private const int WmNcHitTest = 0x0084;
    private const int HtClient = 1;
    private const int HtLeft = 10;
    private const int HtRight = 11;
    private const int HtTop = 12;
    private const int HtTopLeft = 13;
    private const int HtTopRight = 14;
    private const int HtBottom = 15;
    private const int HtBottomLeft = 16;
    private const int HtBottomRight = 17;

    private const double ResizeBorderThickness = 8.0;

    private const int GwlExStyle = -20;
    private const int WsExNoActivate = 0x08000000;
    private const int WmMouseActivate = 0x0021;

    /// <summary>ヘッダー/フッター1つ分の高さ。XAML の行定義と合わせること。</summary>
    private const double ChromeRowHeight = 30d;
    private const int MaNoActivate = 3;

    public static readonly DependencyProperty HeaderTextProperty = DependencyProperty.Register(
        nameof(HeaderText),
        typeof(string),
        typeof(WidgetWindow),
        new PropertyMetadata(string.Empty));

    private readonly WidgetListItemViewModel _widget;
    private readonly IWidgetVerticalScrollContent? _verticalScrollContent;
    private readonly DispatcherTimer _saveBoundsTimer;
    private readonly bool _usesWidgetDisplayNameForHeader;
    private bool _isRestoringBounds = true;
    private bool _isSynchronizingContentScrollBar;

    /// <summary>フッターに中身があるか。ピン留め中に隠す判定と合わせて可視状態を決める。</summary>
    private bool _hasFooterContent;

    private bool _isPinned;

    /// <summary>
    /// 開いた直後の猶予。<b>まだ一度も非アクティブになっていない間だけ真。</b>
    ///
    /// <para>
    /// この間はピン留めの制約(アクティブにしない・ヘッダー/フッターを隠す)を一切掛けない。
    /// 掛けたままだとドラッグ領域が無く、開いた窓を置き直せないため。
    /// <b>一度でも非アクティブになったら二度と戻らない。</b>
    /// </para>
    ///
    /// <para>
    /// アプリ起動時の復元では与えない(<see cref="SuppressInitialGrace"/>)。
    /// 置き直しの猶予は、その場で開いた窓にだけ要る。
    /// </para>
    /// </summary>
    private bool _isInitialGrace = true;

    /// <summary>
    /// 猶予の終了判定を始めてよいか。<b>開いた直後の一括生成が終わるまでは偽。</b>
    ///
    /// <para>
    /// 窓を続けて開くと、後から開いた窓が前の窓のアクティブを奪う。
    /// その非アクティブ化まで数えると、最後に開いた1枚以外は開いた瞬間に猶予が終わり、
    /// 触ってもいないのに動かせなくなる。生成が一段落する
    /// (ディスパッチャが <see cref="DispatcherPriority.Background"/> まで降りる)まで数えない。
    /// </para>
    /// </summary>
    private bool _isGraceArmed;

    /// <summary>手動ドラッグ中か。<see cref="DragMove"/> が使えないときだけ使う。</summary>
    private bool _isManualDragging;

    /// <summary>手動ドラッグ開始時の、ウィンドウ内でのカーソル位置(DIP)。</summary>
    private Point _manualDragOrigin;

    private IInputElement? _manualDragCaptureTarget;

    /// <summary>この窓を開くときに渡された位置と大きさ。まだ測れていない間の保存に使う。</summary>
    private readonly WidgetWindowConfig _restoredBounds;

    public WidgetWindow(
        WidgetListItemViewModel widget,
        FrameworkElement? widgetContent,
        WidgetWindowConfig savedBounds,
        Window? owner,
        string? headerText = null,
        FrameworkElement? headerChromeActions = null,
        FrameworkElement? headerActions = null,
        FrameworkElement? footerContent = null)
    {
        _widget = widget;
        _usesWidgetDisplayNameForHeader = string.IsNullOrWhiteSpace(headerText);

        InitializeComponent();
        DataContext = widget;
        HeaderText = _usesWidgetDisplayNameForHeader
            ? widget.DisplayName
            : headerText!;
        WidgetContentHost.Content = widgetContent;
        WidgetHeaderChromeActionsHost.Content = headerChromeActions;
        WidgetHeaderActionsHost.Content = headerActions;
        SetFooterContent(footerContent);
        _widget.PropertyChanged += Widget_PropertyChanged;
        ConfigManager.Instance.SettingsPreviewChanged += ConfigManager_SettingsPreviewChanged;
        MouseMove += WidgetWindow_ManualDragMouseMove;
        MouseLeftButtonUp += WidgetWindow_ManualDragMouseUp;

        _verticalScrollContent = widgetContent as IWidgetVerticalScrollContent;
        if (_verticalScrollContent is not null)
        {
            Grid.SetColumnSpan(WidgetContentHost, 1);
            _verticalScrollContent.VerticalScrollMetricsChanged += VerticalScrollContent_VerticalScrollMetricsChanged;
            WidgetContentScrollBar.ValueChanged += WidgetContentScrollBar_ValueChanged;
        }

        // まだ測れていないときに返す値。窓を出す前に保存を要求されることがある
        // (対象ごとに開く窓は Show() の前に SaveOpenTargets が走る)ので、
        // そこで 0 を書かせないために復元に使った値を控えておく。
        _restoredBounds = savedBounds.Clone();

        ApplySavedBounds(savedBounds, owner, widget.OriginalIndex);
        ApplyPinState(widget.IsPinned);

        _saveBoundsTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        _saveBoundsTimer.Tick += SaveBoundsTimer_Tick;

        Loaded += WidgetWindow_Loaded;
        LocationChanged += WidgetWindow_BoundsChanged;
        SizeChanged += WidgetWindow_SizeChanged;
        StateChanged += WidgetWindow_StateChanged;
        SourceInitialized += WidgetWindow_SourceInitialized;
    }

    public WidgetListItemViewModel Widget => _widget;

    /// <summary>ピン留めの制約をいま掛けているか。開いた直後の猶予中は掛けない。</summary>
    private bool IsPinBehaviorActive => _isPinned && !_isInitialGrace;

    /// <summary>
    /// 開いた直後の猶予を与えない。アプリ起動時の復元で開く窓に使う。
    /// <b><see cref="Window.Show"/> の前に呼ぶこと。</b>
    /// </summary>
    public void SuppressInitialGrace()
    {
        if (!_isInitialGrace)
        {
            return;
        }

        _isInitialGrace = false;
        ApplyNoActivateState();
        ApplyInactiveChromeVisibility();
    }

    public string HeaderText
    {
        get => (string)GetValue(HeaderTextProperty);
        private set => SetValue(HeaderTextProperty, value);
    }

    public void SetHeaderText(string headerText)
    {
        HeaderText = string.IsNullOrWhiteSpace(headerText)
            ? _widget.DisplayName
            : headerText;
    }

    /// <summary>
    /// ピン留めは「配置を終えて以後は触らない」状態。
    /// フォーカスを奪わなくなり、ヘッダー/フッターを隠す設定もここでだけ効く。
    /// 最前面も、全体設定が「ピン留め時のみ最前面」ならピン留めに従う(<see cref="ApplyTopmost"/>)。
    /// </summary>
    public void ApplyPinState(bool isPinned)
    {
        _isPinned = isPinned;
        ApplyTopmost();
        ApplyNoActivateState();
        ApplyInactiveChromeVisibility();
        QueueContentScrollBarUpdate();
    }

    /// <summary>
    /// 最前面にするか。全体設定「ウィジェットウィンドウ」が常に最前面なら常に、ピン留め時のみ最前面ならピン留め中だけ。
    /// <b><c>Topmost</c> を書く場所はここ1つだけ。</b>
    ///
    /// <para>
    /// <b>ピン留めは猶予(<see cref="IsPinBehaviorActive"/>)ではなくピン留めそのものを見る。</b>
    /// 猶予中は窓を配置している最中なので、そこで最前面を外すとゲームの裏へ落ちる。
    /// </para>
    /// </summary>
    private void ApplyTopmost()
    {
        Topmost = _isPinned
            || ConfigManager.Instance.GetSettingsSnapshot().WidgetWindowTopmostModeIndex
                == AppConfigDefaults.AlwaysWidgetWindowTopmostModeIndex;
    }

    /// <summary>全体設定(プレビューか保存)が変わった。最前面の条件を当て直す。</summary>
    private void ConfigManager_SettingsPreviewChanged(object? sender, EventArgs e)
    {
        ApplyTopmost();
    }

    /// <summary>
    /// ピン留め中にフォーカスを奪わないようにする(<c>WS_EX_NOACTIVATE</c>)。
    /// マウス入力自体は届くので操作はできるが、アクティブにはならない。
    /// そのぶんアクティブ時の不透明度の底上げも起きない。
    /// </summary>
    private void ApplyNoActivateState()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            // まだ HWND が無い。SourceInitialized で貼り直す。
            return;
        }

        var noActivate = IsPinBehaviorActive;
        var exStyle = GetWindowLong(handle, GwlExStyle);
        var updated = noActivate
            ? exStyle | WsExNoActivate
            : exStyle & ~WsExNoActivate;

        if (updated != exStyle)
        {
            SetWindowLong(handle, GwlExStyle, updated);
        }
    }

    /// <summary>
    /// ピン留め中にヘッダー/フッターを隠す設定を反映する。
    ///
    /// <para>
    /// ヘッダーは枠側(<c>FrameHeaderRow</c>)と中身側(<c>ContentHeaderRow</c>)の2層に分かれているので
    /// 両方の行高を畳む。<b>隠している間はドラッグ領域も消える</b>ので、
    /// 動かすにはいったんクリックしてアクティブにする必要がある。
    /// </para>
    /// </summary>
    /// <summary>
    /// ピン留め中にヘッダー/フッターを隠す設定を反映する。
    ///
    /// <para>
    /// <b>窓の矩形は動かさない。</b> 行を畳んで窓を縮める形にすると、
    /// 「本体が窓の中で30px上へ」と「窓が画面上で30px下へ」の2つが必要になり、
    /// どちらか片方だけ反映された瞬間が1フレームでも出ると本体が跳ねて見える
    /// (順序を入れ替えても跳ねる向きが変わるだけ)。
    /// 代わりに行の高さは残したまま中身だけ隠し、枠の背景をその分だけ内側へ寄せる。
    /// <b>本体は1pxも動かないので跳ねようがない。</b>
    /// </para>
    ///
    /// <para>
    /// 隠した側は完全に透明になる。階層化ウィンドウではアルファ0のピクセルは
    /// クリックが素通りするので、窓の矩形が残っていても操作の邪魔にならない。
    /// </para>
    /// </summary>
    private void ApplyInactiveChromeVisibility()
    {
        var pinned = IsPinBehaviorActive;
        var hideHeader = _widget.HideHeaderWhenInactive && pinned;
        var hideFooter = _hasFooterContent && _widget.HideFooterWhenInactive && pinned;

        FrameHeaderChrome.Visibility = hideHeader ? Visibility.Collapsed : Visibility.Visible;
        WidgetHeader.Visibility = hideHeader ? Visibility.Collapsed : Visibility.Visible;

        // 枠の層の行も詰める。枠を30px内側へ寄せたうえで行が30px残っていると、
        // 枠の中身(スクロールバー)が本体の層より30px下にずれる。
        // 本体の層の行は30のまま残す(そこを詰めると本体が動いてしまう)。
        FrameHeaderRow.Height = hideHeader ? new GridLength(0) : new GridLength(ChromeRowHeight);

        UpdateFooterVisibility();

        // 枠(背景・角丸)を隠した分だけ内側へ寄せる。ここを寄せないと、
        // 中身を消しても背景の帯だけが残ってしまう。
        WidgetWindowFrame.Margin = new Thickness(
            0,
            hideHeader ? ChromeRowHeight : 0d,
            0,
            hideFooter ? ChromeRowHeight : 0d);

        QueueContentScrollBarUpdate();
    }

    /// <summary>
    /// フッターの可視状態。<b>枠の層と本体の層で Visibility が違う。</b>
    ///
    /// <para>
    /// 枠側は <c>Collapsed</c> にして行を詰める(枠が30px上で終わるようにする)。
    /// 本体側は <c>Hidden</c> で場所を残す。ここを <c>Collapsed</c> にすると行が0になり、
    /// 本体の表示領域が下へ30px広がってスクロール範囲が枠とずれる。
    /// </para>
    /// </summary>
    private void UpdateFooterVisibility()
    {
        if (!_hasFooterContent)
        {
            WidgetFooterFrame.Visibility = Visibility.Collapsed;
            WidgetFooterHost.Visibility = Visibility.Collapsed;
            return;
        }

        var hidden = _widget.HideFooterWhenInactive && IsPinBehaviorActive;
        WidgetFooterFrame.Visibility = hidden ? Visibility.Collapsed : Visibility.Visible;
        WidgetFooterHost.Visibility = hidden ? Visibility.Hidden : Visibility.Visible;
    }

    private void SetFooterContent(FrameworkElement? footerContent)
    {
        WidgetFooterHost.Content = footerContent;
        _hasFooterContent = footerContent is not null;
        UpdateFooterVisibility();
    }

    protected override void OnClosed(EventArgs e)
    {
        SaveBounds();
        _saveBoundsTimer.Stop();
        _saveBoundsTimer.Tick -= SaveBoundsTimer_Tick;
        _widget.PropertyChanged -= Widget_PropertyChanged;
        ConfigManager.Instance.SettingsPreviewChanged -= ConfigManager_SettingsPreviewChanged;
        MouseMove -= WidgetWindow_ManualDragMouseMove;
        MouseLeftButtonUp -= WidgetWindow_ManualDragMouseUp;
        EndManualDrag();

        if (_verticalScrollContent is not null)
        {
            _verticalScrollContent.VerticalScrollMetricsChanged -= VerticalScrollContent_VerticalScrollMetricsChanged;
            WidgetContentScrollBar.ValueChanged -= WidgetContentScrollBar_ValueChanged;
        }

        if (WidgetContentHost.Content is FrameworkElement { DataContext: IDisposable disposable })
        {
            disposable.Dispose();
        }

        base.OnClosed(e);
    }

    private void WidgetWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _isRestoringBounds = false;

        // 続けて開かれる窓が出そろってから、猶予の終了判定を始める。
        Dispatcher.BeginInvoke(
            DispatcherPriority.Background,
            new Action(() => _isGraceArmed = true));

        UpdateWindowRootClip();
        QueueContentScrollBarUpdate();
    }

    private void WidgetWindow_SourceInitialized(object? sender, EventArgs e)
    {
        if (PresentationSource.FromVisual(this) is HwndSource source)
        {
            source.AddHook(WndProc);
        }

        ApplyNoActivateState();
        ApplyInactiveChromeVisibility();
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // WS_EX_NOACTIVATE だけでは足りない。クリックすると WM_MOUSEACTIVATE が来て、
        // 既定では MA_ACTIVATE が返るのでフォーカスを奪ってしまう。
        // ここで MA_NOACTIVATE を返して、入力だけ受け取り活性化はしない状態にする。
        if (msg == WmMouseActivate && IsPinBehaviorActive)
        {
            handled = true;
            return new IntPtr(MaNoActivate);
        }

        if (msg != WmNcHitTest || WindowState == WindowState.Maximized)
        {
            return IntPtr.Zero;
        }

        var cursor = PointFromScreen(GetScreenPoint(lParam));
        var width = ActualWidth;
        var height = ActualHeight;

        var left = cursor.X >= 0 && cursor.X < ResizeBorderThickness;
        var right = cursor.X <= width && cursor.X > width - ResizeBorderThickness;
        var top = cursor.Y >= 0 && cursor.Y < ResizeBorderThickness;
        var bottom = cursor.Y <= height && cursor.Y > height - ResizeBorderThickness;

        var hitTest = HtClient;

        if (top && left)
        {
            hitTest = HtTopLeft;
        }
        else if (top && right)
        {
            hitTest = HtTopRight;
        }
        else if (bottom && left)
        {
            hitTest = HtBottomLeft;
        }
        else if (bottom && right)
        {
            hitTest = HtBottomRight;
        }
        else if (left)
        {
            hitTest = HtLeft;
        }
        else if (right)
        {
            hitTest = HtRight;
        }
        else if (top)
        {
            hitTest = HtTop;
        }
        else if (bottom)
        {
            hitTest = HtBottom;
        }

        if (hitTest == HtClient)
        {
            return IntPtr.Zero;
        }

        handled = true;
        return new IntPtr(hitTest);
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        // DragMove() は WM_SYSCOMMAND(SC_MOVE) を送って OS の移動ループに入るため、
        // その中で必ずアクティブ化される。WM_MOUSEACTIVATE を潰しても別経路なので通る。
        // 「ピン留め中アクティブにしない」が効いている間だけ、自前でドラッグする。
        if (IsPinBehaviorActive)
        {
            BeginManualDrag(sender as IInputElement, e);
            return;
        }

        DragMove();
    }

    private void BeginManualDrag(IInputElement? captureTarget, MouseButtonEventArgs e)
    {
        if (captureTarget is null || !captureTarget.CaptureMouse())
        {
            return;
        }

        _manualDragCaptureTarget = captureTarget;
        _manualDragOrigin = e.GetPosition(this);
        _isManualDragging = true;
        e.Handled = true;
    }

    private void EndManualDrag()
    {
        if (!_isManualDragging)
        {
            return;
        }

        _isManualDragging = false;
        _manualDragCaptureTarget?.ReleaseMouseCapture();
        _manualDragCaptureTarget = null;
        ScheduleBoundsSave();
    }

    /// <summary>
    /// 手動ドラッグの移動。ウィンドウを (dx, dy) 動かすと、ウィンドウ内でのカーソル位置は
    /// 開始時の値へ戻る。だから差分をそのまま Left / Top に足せばよい。
    /// すべて DIP で完結するので DPI が 100% 以外でもずれない。
    /// </summary>
    private void WidgetWindow_ManualDragMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isManualDragging)
        {
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed)
        {
            EndManualDrag();
            return;
        }

        var cursor = e.GetPosition(this);
        Left += cursor.X - _manualDragOrigin.X;
        Top += cursor.Y - _manualDragOrigin.Y;
    }

    private void WidgetWindow_ManualDragMouseUp(object sender, MouseButtonEventArgs e)
    {
        EndManualDrag();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Widget_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_usesWidgetDisplayNameForHeader
            && e.PropertyName == nameof(WidgetListItemViewModel.DisplayName))
        {
            HeaderText = _widget.DisplayName;
        }

        if (e.PropertyName is nameof(WidgetListItemViewModel.HideHeaderWhenInactive)
            or nameof(WidgetListItemViewModel.HideFooterWhenInactive))
        {
            ApplyInactiveChromeVisibility();
        }
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        ApplyInactiveChromeVisibility();
    }

    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);

        if (_isInitialGrace && _isGraceArmed)
        {
            // 開いた直後の猶予はここで終わる。以後はピン留めの制約が通常どおり掛かる。
            // 一度も触られていない窓(隣の窓が開いて奪われただけ)はここへ来ない。
            _isInitialGrace = false;
            ApplyNoActivateState();
        }

        ApplyInactiveChromeVisibility();
    }

    private void WidgetWindow_BoundsChanged(object? sender, EventArgs e)
    {
        ScheduleBoundsSave();
    }

    private void WidgetWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateWindowRootClip();
        QueueContentScrollBarUpdate();
        ScheduleBoundsSave();
    }

    private void WidgetWindow_StateChanged(object? sender, EventArgs e)
    {
        UpdateWindowRootClip();
    }

    private void UpdateWindowRootClip()
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowRoot.Clip = null;
            return;
        }

        if (WindowRoot.ActualWidth <= 0 || WindowRoot.ActualHeight <= 0)
        {
            return;
        }

        var cornerRadius = TryFindResource("Radius.Default") is CornerRadius configuredRadius
            ? configuredRadius.TopLeft
            : 6d;

        WindowRoot.Clip = new RectangleGeometry(
            new Rect(0, 0, WindowRoot.ActualWidth, WindowRoot.ActualHeight),
            cornerRadius,
            cornerRadius);
    }

    private void VerticalScrollContent_VerticalScrollMetricsChanged(object? sender, EventArgs e)
    {
        UpdateContentScrollBar();
    }

    private void QueueContentScrollBarUpdate()
    {
        if (_verticalScrollContent is null)
        {
            WidgetContentScrollBar.Visibility = Visibility.Collapsed;
            WidgetContentScrollBarColumn.Width = new GridLength(5);
            return;
        }

        Dispatcher.BeginInvoke(
            UpdateContentScrollBar,
            DispatcherPriority.Loaded);
    }

    private void WidgetContentScrollBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isSynchronizingContentScrollBar || _verticalScrollContent is null)
        {
            return;
        }

        _verticalScrollContent.SetVerticalScrollOffset(e.NewValue);
    }

    private void UpdateContentScrollBar()
    {
        if (_verticalScrollContent is null || !IsLoaded)
        {
            WidgetContentScrollBar.Visibility = Visibility.Collapsed;
            WidgetContentScrollBarColumn.Width = new GridLength(5);
            return;
        }

        var metrics = _verticalScrollContent.GetVerticalScrollMetrics();
        var maximum = Math.Max(metrics.Maximum, 0);
        var viewport = Math.Max(metrics.ViewportSize, 0);

        _isSynchronizingContentScrollBar = true;
        try
        {
            WidgetContentScrollBar.Minimum = 0;
            WidgetContentScrollBar.Maximum = maximum;
            WidgetContentScrollBar.ViewportSize = viewport;
            WidgetContentScrollBar.LargeChange = Math.Max(metrics.LargeChange, 1);
            WidgetContentScrollBar.SmallChange = Math.Max(metrics.SmallChange, 1);
            WidgetContentScrollBar.Value = Math.Clamp(metrics.Value, 0, maximum);

            var isScrollBarVisible = maximum > 0;

            WidgetContentScrollBar.Visibility = isScrollBarVisible
                ? Visibility.Visible
                : Visibility.Collapsed;

            WidgetContentScrollBarColumn.Width = isScrollBarVisible
                ? new GridLength(16)
                : new GridLength(5);
        }
        finally
        {
            _isSynchronizingContentScrollBar = false;
        }
    }

    private void ScheduleBoundsSave()
    {
        if (_isRestoringBounds || !IsLoaded || WindowState == WindowState.Minimized)
        {
            return;
        }

        _saveBoundsTimer.Stop();
        _saveBoundsTimer.Start();
    }

    private void SaveBoundsTimer_Tick(object? sender, EventArgs e)
    {
        _saveBoundsTimer.Stop();
        SaveBounds();
    }

    /// <summary>
    /// この窓ぶんの位置を保存する経路。<c>null</c> なら種別ごとの位置へ保存する。
    ///
    /// <para>
    /// 同じウィジェットを複数開くと、種別ごとの位置では最後に動かした窓が全部を上書きし、
    /// 次の起動で全員が同じ場所に出る。1枚ずつ持つ窓はこちらへ流す。
    /// </para>
    /// </summary>
    /// <summary>
    /// 枠のレイヤーに重ねる飾りの置き場。<b>中身のレイヤーに置くと背景の上にベタで乗る</b>ので、
    /// 分割線や閉じる印と同じ不透明度で合成したいものはここへ入れる。
    /// </summary>
    public ContentControl FrameOverlayHost => WidgetFrameOverlayHost;

    public Action? SaveWindowBoundsOverride { get; set; }

    private void SaveBounds()
    {
        if (_isRestoringBounds
            || !IsLoaded
            || WindowState == WindowState.Minimized
            || !IsFinitePositive(ActualWidth)
            || !IsFinitePositive(ActualHeight)
            || !double.IsFinite(Left)
            || !double.IsFinite(Top))
        {
            return;
        }

        if (SaveWindowBoundsOverride is not null)
        {
            SaveWindowBoundsOverride();
            return;
        }

        WidgetStateManager.Instance.SaveWidgetWindowBounds(
            _widget.Kind,
            Left,
            Top,
            ActualWidth,
            ActualHeight);
    }

    /// <summary>
    /// いまの位置と大きさ。保存用。
    ///
    /// <para>
    /// <b>まだ測れていない項目は、復元に使った値をそのまま返す。</b>
    /// この窓を <c>Show()</c> する前に <c>SaveOpenTargets</c> が走る経路があり
    /// (窓を作った直後・対象名が判明したとき・別の窓が閉じたとき)、そこでは
    /// <see cref="FrameworkElement.ActualWidth"/> がまだ 0 になっている。
    /// </para>
    ///
    /// <para>
    /// 素通しで 0 を書くと、復元側 <c>ApplySavedBounds</c> が正の有限値しか採らないため
    /// <b>大きさだけが既定に戻る</b>。位置は <c>Left</c>/<c>Top</c> がコンストラクタで
    /// 入るので残り、「位置は覚えているのにリサイズだけ忘れる」という形で出る。
    /// </para>
    /// </summary>
    public WidgetWindowConfig GetCurrentBounds()
    {
        var hasMeasuredSize = IsFinitePositive(ActualWidth) && IsFinitePositive(ActualHeight);
        var hasPosition = double.IsFinite(Left) && double.IsFinite(Top);

        return new WidgetWindowConfig
        {
            X = hasPosition ? Left : _restoredBounds.X,
            Y = hasPosition ? Top : _restoredBounds.Y,
            Width = hasMeasuredSize ? ActualWidth : _restoredBounds.Width,
            Height = hasMeasuredSize ? ActualHeight : _restoredBounds.Height
        };
    }

    /// <summary>掴める最小の領域(px)。これだけ仮想画面内に残っていれば動かさない。</summary>
    private const double ReachableWidth = 60d;
    private const double ReachableHeight = 30d;

    /// <summary>
    /// 保存された位置が画面に届かないときだけ、仮想画面の中へ寄せる。
    ///
    /// <para>
    /// 表示していたモニタが無くなると、保存座標がどのモニタにも属さなくなって
    /// ウィンドウごと画面外に残る。メインウィンドウは
    /// <c>WindowPlacementBehavior</c> で同じ対策をしているが、ウィジェットには無かった。
    /// </para>
    ///
    /// <para>
    /// <b>少しはみ出している程度では動かさない。</b> オーバーレイなので端から
    /// わざとはみ出して置く使い方があり、そこを引き戻すと邪魔になる。
    /// ヘッダーを掴める分が残っているかだけを見る。
    /// </para>
    /// </summary>
    private void MoveIntoVirtualScreenIfUnreachable()
    {
        if (!double.IsFinite(Left) || !double.IsFinite(Top)
            || !IsFinitePositive(Width) || !IsFinitePositive(Height))
        {
            return;
        }

        var minX = SystemParameters.VirtualScreenLeft;
        var minY = SystemParameters.VirtualScreenTop;
        var maxX = minX + SystemParameters.VirtualScreenWidth;
        var maxY = minY + SystemParameters.VirtualScreenHeight;

        var visibleWidth = Math.Min(Left + Width, maxX) - Math.Max(Left, minX);
        var visibleHeight = Math.Min(Top + Height, maxY) - Math.Max(Top, minY);
        if (visibleWidth >= ReachableWidth && visibleHeight >= ReachableHeight)
        {
            return;
        }

        var width = Math.Min(Width, SystemParameters.VirtualScreenWidth);
        var height = Math.Min(Height, SystemParameters.VirtualScreenHeight);

        Width = width;
        Height = height;
        Left = Math.Clamp(Left, minX, Math.Max(minX, maxX - width));
        Top = Math.Clamp(Top, minY, Math.Max(minY, maxY - height));
    }

    private void ApplySavedBounds(WidgetWindowConfig savedBounds, Window? owner, int originalIndex)
    {
        if (IsFinitePositive(savedBounds.Width) && IsFinitePositive(savedBounds.Height))
        {
            Width = Math.Max(savedBounds.Width!.Value, MinWidth);
            Height = Math.Max(savedBounds.Height!.Value, MinHeight);
        }

        if (double.IsFinite(savedBounds.X ?? double.NaN) && double.IsFinite(savedBounds.Y ?? double.NaN))
        {
            Left = savedBounds.X!.Value;
            Top = savedBounds.Y!.Value;
            MoveIntoVirtualScreenIfUnreachable();
            return;
        }

        if (owner is null)
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            return;
        }

        const double initialOffset = 44;
        const double cascadeOffset = 20;
        Left = owner.Left + initialOffset + (cascadeOffset * originalIndex);
        Top = owner.Top + initialOffset + (cascadeOffset * originalIndex);
    }

    private static bool IsFinitePositive(double? value)
    {
        return value.HasValue && double.IsFinite(value.Value) && value.Value > 0;
    }

    private static Point GetScreenPoint(IntPtr lParam)
    {
        var value = lParam.ToInt64();
        var x = unchecked((short)(value & 0xFFFF));
        var y = unchecked((short)((value >> 16) & 0xFFFF));
        return new Point(x, y);
    }
}
