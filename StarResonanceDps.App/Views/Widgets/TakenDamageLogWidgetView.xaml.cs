using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Serilog;
using StarResonanceDps.App.Behaviors;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.ViewModels;

namespace StarResonanceDps.App.Views.Widgets;

/// <summary>
/// 被ダメログ。縦スクロールはウィンドウ枠側の細いスクロールバーに繋ぐ(他のウィジェットと同じ)。
///
/// <para>
/// <b>末尾に居るときだけ追記に追従する。</b> 途中を読んでいる間に行が増えても位置は動かさない。
/// 追従するかは、利用者のスクロール(内容の高さが変わっていない回)で決める。
/// </para>
///
/// <para>
/// 文字は行をまたいで選べ(<see cref="TextRowsSelectionBehavior"/>)、選んだ文字は右クリックの「コピー」(窓がアクティブなら Ctrl+C も)で
/// クリップボードへ入れる(<see cref="TakenDamageLogWidgetViewModel.CreateCopyText"/>)。
/// </para>
/// </summary>
public partial class TakenDamageLogWidgetView : UserControl, IWidgetVerticalScrollContent
{
    private ScrollViewer? _scrollViewer;
    private bool _followsTail = true;

    public TakenDamageLogWidgetView()
    {
        InitializeComponent();
    }

    public event EventHandler? VerticalScrollMetricsChanged;

    public WidgetVerticalScrollMetrics GetVerticalScrollMetrics()
    {
        if (_scrollViewer is null)
        {
            return new WidgetVerticalScrollMetrics(0, 0, 0, 1, 1);
        }

        var maximum = Math.Max(_scrollViewer.ScrollableHeight, 0);
        var viewport = Math.Max(_scrollViewer.ViewportHeight, 0);

        return new WidgetVerticalScrollMetrics(
            maximum,
            viewport,
            Math.Min(_scrollViewer.VerticalOffset, maximum),
            Math.Max(viewport * 0.9, 1),
            18);
    }

    public void SetVerticalScrollOffset(double verticalOffset)
    {
        if (_scrollViewer is null)
        {
            return;
        }

        var maximum = Math.Max(_scrollViewer.ScrollableHeight, 0);
        var offset = double.IsFinite(verticalOffset)
            ? Math.Clamp(verticalOffset, 0, maximum)
            : 0;

        _scrollViewer.ScrollToVerticalOffset(offset);
    }

    private void TakenDamageLogWidgetView_Loaded(object sender, RoutedEventArgs e)
    {
        if (_scrollViewer is null)
        {
            LogItemsControl.ApplyTemplate();
            _scrollViewer = (ScrollViewer)LogItemsControl.Template.FindName("LogScrollViewer", LogItemsControl);
            _scrollViewer.ScrollChanged += LogScrollViewer_ScrollChanged;
        }

        NotifyVerticalScrollMetricsChanged();
    }

    private void TakenDamageLogWidgetView_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void LogScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        var scrollViewer = (ScrollViewer)sender;

        if (e.ExtentHeightChange == 0)
        {
            _followsTail = scrollViewer.VerticalOffset >= scrollViewer.ScrollableHeight - 1;
        }
        else if (_followsTail)
        {
            scrollViewer.ScrollToEnd();
        }

        NotifyVerticalScrollMetricsChanged();
    }

    private void NotifyVerticalScrollMetricsChanged()
    {
        VerticalScrollMetricsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void CopyCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = TextRowsSelectionBehavior.TryGetSelection(LogItemsControl, out _);
        e.Handled = true;
    }

    /// <summary>
    /// 選んだ文字をクリップボードへ入れる。
    /// 入れられなければ(ほかのアプリがクリップボードを使っているときなど)、写真のコピーと同じくメッセージの窓で知らせる。オーナーはこのウィジェットの窓。
    /// </summary>
    private void CopyCommand_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        e.Handled = true;
        if (DataContext is not TakenDamageLogWidgetViewModel viewModel
            || !TextRowsSelectionBehavior.TryGetSelection(LogItemsControl, out var selection))
        {
            return;
        }

        var text = viewModel.CreateCopyText(selection);
        try
        {
            Clipboard.SetText(text);
        }
        catch (ExternalException ex)
        {
            Log.Warning(ex, "Failed to copy timeline rows to clipboard");
            var localization = LocalizationManager.Instance;
            MessageWindow.Show(
                Window.GetWindow(this),
                localization.GetString("TakenDamageLog_CopyTextFailed_Title"),
                localization.GetString("TakenDamageLog_CopyTextFailed_Message"));
        }
    }
}
