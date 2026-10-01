using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.ViewModels;

namespace StarResonanceDps.App.Views.Widgets;

/// <summary>
/// バフ/デバフの一覧。プレイヤー用とエンティティ用の2つのViewModel(<see cref="IBuffListWindowViewModel"/>)が同じこのビューを使う。
///
/// <para>
/// 縦スクロールは<b>ウィンドウ枠側の細いスクロールバー</b>に繋ぐ。内側にWPFのスクロールバーを
/// 出すと他のウィジェットと見た目が揃わず、コンテンツ幅も削られる。
/// </para>
///
/// <para>
/// 行の左クリックはカードを開き、右クリックはメニュー(カード・非表示)を開く。メニューの開き方はエンティティリストと同じ。
/// </para>
/// </summary>
public partial class PlayerBuffListWidgetView : UserControl, IWidgetVerticalScrollContent
{
    private const string BuffContextMenuStyleKey = "Menu.WidgetWindowBuffListContextMenu";
    private const string BuffContextMenuFirstItemStyleKey = "Menu.WidgetWindowBuffListContextMenuItem.First";
    private const string BuffContextMenuMiddleItemStyleKey = "Menu.WidgetWindowBuffListContextMenuItem.Middle";
    private const string BuffContextMenuLastItemStyleKey = "Menu.WidgetWindowBuffListContextMenuItem.Last";

    private ContextMenu? _openBuffMenu;
    private PlayerBuffEntry? _openBuffEntry;

    public PlayerBuffListWidgetView()
    {
        InitializeComponent();
    }

    public event EventHandler? VerticalScrollMetricsChanged;

    public WidgetVerticalScrollMetrics GetVerticalScrollMetrics()
    {
        var maximum = Math.Max(BuffListScrollViewer.ScrollableHeight, 0);
        var viewport = Math.Max(BuffListScrollViewer.ViewportHeight, 0);

        return new WidgetVerticalScrollMetrics(
            maximum,
            viewport,
            Math.Min(BuffListScrollViewer.VerticalOffset, maximum),
            Math.Max(viewport * 0.9, 1),
            34);
    }

    public void SetVerticalScrollOffset(double verticalOffset)
    {
        var maximum = Math.Max(BuffListScrollViewer.ScrollableHeight, 0);
        var offset = double.IsFinite(verticalOffset)
            ? Math.Clamp(verticalOffset, 0, maximum)
            : 0;

        BuffListScrollViewer.ScrollToVerticalOffset(offset);
    }

    private void PlayerBuffListWidgetView_Loaded(object sender, RoutedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void PlayerBuffListWidgetView_Unloaded(object sender, RoutedEventArgs e)
    {
        CloseBuffMenu();
    }

    private void BuffListItem_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Button button)
        {
            OpenBuffMenu(button);
            e.Handled = true;
        }
    }

    private void OpenBuffMenu(Button button)
    {
        if (DataContext is not IBuffListWindowViewModel
            || button.ContextMenu is not ContextMenu menu
            || button.DataContext is not PlayerBuffEntry entry)
        {
            return;
        }

        ApplyBuffMenuStyles(menu);

        if (!ReferenceEquals(_openBuffMenu, menu))
        {
            CloseBuffMenu();
        }

        menu.PlacementTarget = button;
        SetOpenBuffMenu(menu, entry);
        menu.IsOpen = true;
    }

    private void BuffContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is ContextMenu menu
            && menu.PlacementTarget is Button { DataContext: PlayerBuffEntry entry })
        {
            SetOpenBuffMenu(menu, entry);
        }
    }

    private void BuffContextMenu_Closed(object sender, RoutedEventArgs e)
    {
        if (sender is ContextMenu menu)
        {
            ClearBuffMenu(menu);
        }
    }

    private void SetOpenBuffMenu(ContextMenu menu, PlayerBuffEntry entry)
    {
        if (ReferenceEquals(_openBuffMenu, menu)
            && ReferenceEquals(_openBuffEntry, entry))
        {
            entry.IsMenuOpen = true;
            return;
        }

        CloseBuffMenu();

        _openBuffMenu = menu;
        _openBuffEntry = entry;
        entry.PropertyChanged += OpenBuffEntry_PropertyChanged;
        entry.IsMenuOpen = true;
    }

    private void CloseBuffMenu()
    {
        var menu = _openBuffMenu;
        ClearBuffMenu(menu);

        if (menu?.IsOpen == true)
        {
            menu.IsOpen = false;
        }
    }

    private void ClearBuffMenu(ContextMenu? expectedMenu)
    {
        if (expectedMenu is not null
            && !ReferenceEquals(_openBuffMenu, expectedMenu))
        {
            return;
        }

        var entry = _openBuffEntry;
        _openBuffMenu = null;
        _openBuffEntry = null;

        if (entry is null)
        {
            return;
        }

        entry.PropertyChanged -= OpenBuffEntry_PropertyChanged;
        entry.IsMenuOpen = false;
    }

    private void OpenBuffEntry_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlayerBuffEntry.IsMenuOpen)
            && sender is PlayerBuffEntry { IsMenuOpen: false } entry
            && ReferenceEquals(entry, _openBuffEntry))
        {
            CloseBuffMenu();
        }
    }

    private void ApplyBuffMenuStyles(ContextMenu menu)
    {
        if (TryFindResource(BuffContextMenuStyleKey) is Style contextMenuStyle)
        {
            menu.Style = contextMenuStyle;
        }

        var menuItems = menu.Items.OfType<MenuItem>().ToArray();
        for (var index = 0; index < menuItems.Length; index++)
        {
            var styleKey = index switch
            {
                0 => BuffContextMenuFirstItemStyleKey,
                var lastIndex when lastIndex == menuItems.Length - 1 => BuffContextMenuLastItemStyleKey,
                _ => BuffContextMenuMiddleItemStyleKey
            };

            if (TryFindResource(styleKey) is Style menuItemStyle)
            {
                menuItems[index].Style = menuItemStyle;
            }
        }
    }

    private void PlayerBuffListWidgetView_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void PlayerBuffListWidgetView_DataContextChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void BuffListScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void NotifyVerticalScrollMetricsChanged()
    {
        VerticalScrollMetricsChanged?.Invoke(this, EventArgs.Empty);
    }
}
