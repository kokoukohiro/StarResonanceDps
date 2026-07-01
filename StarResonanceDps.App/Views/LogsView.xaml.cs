using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using StarResonanceDps.Core.Logging;

namespace StarResonanceDps.App.Views;

public partial class LogsView : UserControl
{
    private readonly PacketDiagnosticLogStore _diagnosticLog = PacketDiagnosticLogStore.Instance;
    private bool _isSubscribed;

    internal ScrollViewer ContentScrollViewer => PacketDiagnosticsScrollViewer;

    public LogsView()
    {
        InitializeComponent();
        Loaded += LogsView_Loaded;
        Unloaded += LogsView_Unloaded;
    }

    private void LogsView_Loaded(object sender, RoutedEventArgs e)
    {
        if (!_isSubscribed)
        {
            _diagnosticLog.EntriesChanged += DiagnosticLog_EntriesChanged;
            _isSubscribed = true;
        }

        RefreshLogText();
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

    private void DiagnosticLog_EntriesChanged(object? sender, EventArgs e)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(RefreshLogText, DispatcherPriority.Background);
            return;
        }

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
