using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.ViewModels;

namespace StarResonanceDps.App.Views.Widgets;

/// <summary>
/// 被ダメログ(識別子は被ダメリスト)の中身。縦のスクロールバーは窓の枠の層が出す(蘇生リストと同じ)。
/// 行の左クリックはその人の被ダメ詳細を開く。右クリックのメニューの作りは蘇生リスト(プレイヤーリスト)と同じで、項目は被ダメ詳細だけ。
///
/// <para>
/// 新しい行は一番上に入る。<b>一番上に居るときだけ新しい行に付いていく</b>(タイムラインの末尾への追従を上下逆にしたもの)。
/// 下の古い行を読んでいる間に行が増えたら、増えた高さぶん位置をずらして、読んでいる行を動かさない。
/// 一番上に居るかは、利用者のスクロール(内容の高さが変わっていない回)で決める。
/// </para>
/// </summary>
public partial class DamageTakenListWidgetView : UserControl, IWidgetVerticalScrollContent
{
    private const string PlayerSelectionContextMenuStyleKey = "Menu.WidgetWindowPlayerSelectionContextMenu";
    private const string PlayerSelectionContextMenuFirstItemStyleKey = "Menu.WidgetWindowPlayerSelectionContextMenuItem.First";
    private const string PlayerSelectionContextMenuMiddleItemStyleKey = "Menu.WidgetWindowPlayerSelectionContextMenuItem.Middle";
    private const string PlayerSelectionContextMenuLastItemStyleKey = "Menu.WidgetWindowPlayerSelectionContextMenuItem.Last";
    private const string PlayerSelectionContextMenuSingleItemStyleKey = "Menu.WidgetWindowPlayerSelectionContextMenuItem.Single";

    /// <summary>スクロールバーの矢印1回で動く量。行の高さと同じ。</summary>
    private const double SmallScrollChange = 31d;

    private ScrollViewer? _scrollViewer;
    private bool _followsHead = true;
    private ContextMenu? _openPlayerSelectionMenu;
    private DamageTakenListEntry? _openPlayerSelectionEntry;

    public event EventHandler? VerticalScrollMetricsChanged;

    public DamageTakenListWidgetView()
    {
        InitializeComponent();
        Loaded += DamageTakenListWidgetView_Loaded;
        Unloaded += DamageTakenListWidgetView_Unloaded;
        SizeChanged += DamageTakenListWidgetView_SizeChanged;
    }

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
            SmallScrollChange);
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

    private void DamageTakenListWidgetView_Loaded(object sender, RoutedEventArgs e)
    {
        if (_scrollViewer is null)
        {
            DamageTakenListItemsControl.ApplyTemplate();
            _scrollViewer = (ScrollViewer)DamageTakenListItemsControl.Template.FindName("DamageTakenListScrollViewer", DamageTakenListItemsControl);
            _scrollViewer.ScrollChanged += DamageTakenListScrollViewer_ScrollChanged;
        }

        NotifyVerticalScrollMetricsChanged();
    }

    private void DamageTakenListWidgetView_Unloaded(object sender, RoutedEventArgs e)
    {
        ClosePlayerSelectionMenu();
    }

    private void DamageTakenListWidgetView_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void DamageTakenListScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        var scrollViewer = (ScrollViewer)sender;

        if (e.ExtentHeightChange == 0)
        {
            _followsHead = scrollViewer.VerticalOffset <= 1;
        }
        else if (_followsHead)
        {
            scrollViewer.ScrollToTop();
        }
        else if (e.ExtentHeightChange > 0)
        {
            // 行は一番上にしか増えない(組み直しは一覧ごと作り直す)ので、増えた高さぶん下げると読んでいる行がそのままの位置に残る。
            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + e.ExtentHeightChange);
        }

        NotifyVerticalScrollMetricsChanged();
    }

    /// <summary>行の部品が別の行に使い回される(仮想化)。開いているメニューの行なら閉じる(プレイヤーリストと同じ)。</summary>
    private void DamageTakenListItemsControl_CleanUpVirtualizedItem(object sender, CleanUpVirtualizedItemEventArgs e)
    {
        if (_openPlayerSelectionEntry is not null && ReferenceEquals(e.Value, _openPlayerSelectionEntry))
        {
            ClosePlayerSelectionMenu();
        }
    }

    /// <summary>左クリックはメニューを開かず、その行の人の被ダメ詳細を直接開く。メニューは右クリック側が担う。</summary>
    private void DamageTakenListItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: DamageTakenListWidgetViewModel viewModel, DataContext: DamageTakenListEntry entry })
        {
            viewModel.RequestDamageTakenSummaryCommand.Execute(entry);
        }
    }

    private void DamageTakenListItem_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Button button)
        {
            OpenPlayerSelectionMenu(button);
            e.Handled = true;
        }
    }

    private void OpenPlayerSelectionMenu(Button button)
    {
        if (button.ContextMenu is not ContextMenu menu || button.DataContext is not DamageTakenListEntry entry)
        {
            return;
        }

        ApplyPlayerSelectionMenuStyles(menu);

        if (!ReferenceEquals(_openPlayerSelectionMenu, menu))
        {
            ClosePlayerSelectionMenu();
        }

        menu.PlacementTarget = button;
        SetOpenPlayerSelectionMenu(menu, entry);
        menu.IsOpen = true;
    }

    private void PlayerSelectionContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is ContextMenu menu
            && menu.PlacementTarget is Button { DataContext: DamageTakenListEntry entry })
        {
            SetOpenPlayerSelectionMenu(menu, entry);
        }
    }

    private void PlayerSelectionContextMenu_Closed(object sender, RoutedEventArgs e)
    {
        if (sender is ContextMenu menu)
        {
            ClearPlayerSelectionMenu(menu);
        }
    }

    private void SetOpenPlayerSelectionMenu(ContextMenu menu, DamageTakenListEntry entry)
    {
        if (ReferenceEquals(_openPlayerSelectionMenu, menu)
            && ReferenceEquals(_openPlayerSelectionEntry, entry))
        {
            entry.IsPlayerSelectionMenuOpen = true;
            return;
        }

        ClosePlayerSelectionMenu();

        _openPlayerSelectionMenu = menu;
        _openPlayerSelectionEntry = entry;
        entry.PropertyChanged += OpenPlayerSelectionEntry_PropertyChanged;
        entry.IsPlayerSelectionMenuOpen = true;
    }

    private void ClosePlayerSelectionMenu()
    {
        var menu = _openPlayerSelectionMenu;
        ClearPlayerSelectionMenu(menu);

        if (menu?.IsOpen == true)
        {
            menu.IsOpen = false;
        }
    }

    private void ClearPlayerSelectionMenu(ContextMenu? expectedMenu)
    {
        if (expectedMenu is not null
            && !ReferenceEquals(_openPlayerSelectionMenu, expectedMenu))
        {
            return;
        }

        var entry = _openPlayerSelectionEntry;
        _openPlayerSelectionMenu = null;
        _openPlayerSelectionEntry = null;

        if (entry is null)
        {
            return;
        }

        entry.PropertyChanged -= OpenPlayerSelectionEntry_PropertyChanged;
        entry.IsPlayerSelectionMenuOpen = false;
    }

    /// <summary>行の側でメニューの印が下ろされた(その人が一覧から消えた)。メニューを閉じる。</summary>
    private void OpenPlayerSelectionEntry_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DamageTakenListEntry.IsPlayerSelectionMenuOpen)
            && sender is DamageTakenListEntry { IsPlayerSelectionMenuOpen: false } entry
            && ReferenceEquals(entry, _openPlayerSelectionEntry))
        {
            ClosePlayerSelectionMenu();
        }
    }

    private void ApplyPlayerSelectionMenuStyles(ContextMenu menu)
    {
        if (TryFindResource(PlayerSelectionContextMenuStyleKey) is Style contextMenuStyle)
        {
            menu.Style = contextMenuStyle;
        }

        // 隠れている項目は数に入れない(見えていない項目に先頭/末尾の角丸が当たらないように)。1つだけなら四隅とも角丸。
        var menuItems = menu.Items.OfType<MenuItem>()
            .Where(item => item.Visibility == Visibility.Visible)
            .ToArray();
        for (var index = 0; index < menuItems.Length; index++)
        {
            var styleKey = menuItems.Length == 1
                ? PlayerSelectionContextMenuSingleItemStyleKey
                : index switch
                {
                    0 => PlayerSelectionContextMenuFirstItemStyleKey,
                    var lastIndex when lastIndex == menuItems.Length - 1 => PlayerSelectionContextMenuLastItemStyleKey,
                    _ => PlayerSelectionContextMenuMiddleItemStyleKey
                };

            if (TryFindResource(styleKey) is Style menuItemStyle)
            {
                menuItems[index].Style = menuItemStyle;
            }
        }
    }

    private void NotifyVerticalScrollMetricsChanged()
    {
        VerticalScrollMetricsChanged?.Invoke(this, EventArgs.Empty);
    }
}
