using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using StarResonanceDps.Core.Logging;

namespace StarResonanceDps.App.Views;

/// <summary>
/// ログタブ。見えている間だけ文字を読み直す。隠れている間に来た通知は印だけ付けて、次に見えたときに1回読む。
/// </summary>
public partial class LogsView : UserControl
{
    private readonly PacketDiagnosticLogStore _diagnosticLog = PacketDiagnosticLogStore.Instance;
    private bool _isSubscribed;

    /// <summary>画面のスレッドへの読み直しを予約済みか(0 / 1)。通知が続けて来ても予約は1回にまとめる。</summary>
    private int _isRefreshQueued;

    /// <summary>まだ読んでいない通知がある。画面のスレッドだけが触る。</summary>
    private bool _needsRefresh = true;

    internal ScrollViewer ContentScrollViewer => PacketDiagnosticsScrollViewer;

    public LogsView()
    {
        InitializeComponent();
        Loaded += LogsView_Loaded;
        Unloaded += LogsView_Unloaded;
        IsVisibleChanged += LogsView_IsVisibleChanged;
    }

    private void LogsView_Loaded(object sender, RoutedEventArgs e)
    {
        if (!_isSubscribed)
        {
            _diagnosticLog.EntriesChanged += DiagnosticLog_EntriesChanged;
            _isSubscribed = true;
        }

        // 外れていた間の通知は受けていないので、見えていれば読み直す。
        _needsRefresh = true;
        RefreshIfNeeded();
    }

    private void LogsView_Unloaded(object sender, RoutedEventArgs e)
    {
        if (!_isSubscribed)
        {
            return;
        }

        _diagnosticLog.EntriesChanged -= DiagnosticLog_EntriesChanged;
        _isSubscribed = false;
    }

    private void LogsView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        RefreshIfNeeded();
    }

    /// <summary>
    /// ほかのスレッドからも続けて来る。予約が残っている間の通知は、その予約の読み直しに含まれる
    /// (読むのは読んだ時点の全部)。
    /// </summary>
    private void DiagnosticLog_EntriesChanged(object? sender, EventArgs e)
    {
        if (Interlocked.Exchange(ref _isRefreshQueued, 1) == 1)
        {
            return;
        }

        Dispatcher.BeginInvoke(ApplyQueuedRefresh, DispatcherPriority.Background);
    }

    private void ApplyQueuedRefresh()
    {
        // 先に下ろす。読んでいる間に来た通知は次の予約になる。
        Interlocked.Exchange(ref _isRefreshQueued, 0);
        _needsRefresh = true;
        RefreshIfNeeded();
    }

    private void RefreshIfNeeded()
    {
        if (!_needsRefresh || !IsVisible)
        {
            return;
        }

        _needsRefresh = false;
        RefreshLogText();
    }

    private void RefreshLogText()
    {
        var wasAtBottom = PacketDiagnosticsScrollViewer.VerticalOffset >= PacketDiagnosticsScrollViewer.ScrollableHeight - 1;
        PacketDiagnosticsTextBox.Text = _diagnosticLog.GetDisplayText();

        if (wasAtBottom)
        {
            Dispatcher.BeginInvoke(
                PacketDiagnosticsScrollViewer.ScrollToEnd,
                DispatcherPriority.Background);
        }
    }
}
