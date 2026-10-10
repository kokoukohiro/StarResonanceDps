using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using StarResonanceDps.App.Models.Widgets;

namespace StarResonanceDps.App.Views.Widgets;

public partial class EntityListWidgetView : UserControl, IWidgetVerticalScrollContent
{
    private const string EntitySelectionContextMenuStyleKey = "Menu.WidgetWindowEntitySelectionContextMenu";
    private const string EntitySelectionContextMenuFirstItemStyleKey = "Menu.WidgetWindowEntitySelectionContextMenuItem.First";
    private const string EntitySelectionContextMenuMiddleItemStyleKey = "Menu.WidgetWindowEntitySelectionContextMenuItem.Middle";
    private const string EntitySelectionContextMenuLastItemStyleKey = "Menu.WidgetWindowEntitySelectionContextMenuItem.Last";

    /// <summary>一覧のテンプレートの中の ScrollViewer(仮想化のため一覧の中に置く)。読み込むまでは null。</summary>
    private ScrollViewer? _scrollViewer;

    private ContextMenu? _openEntitySelectionMenu;
    private EntityListEntry? _openEntitySelectionEntry;

    public event EventHandler? VerticalScrollMetricsChanged;

    public EntityListWidgetView()
    {
        InitializeComponent();
        Loaded += EntityListWidgetView_Loaded;
        Unloaded += EntityListWidgetView_Unloaded;
        SizeChanged += EntityListWidgetView_SizeChanged;
        PreviewMouseWheel += EntityListWidgetView_PreviewMouseWheel;
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
            50);
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

        StarResonanceDps.App.Diagnostics.HistorySwitchProbe.ScrollInputReceived("EntityList");
        _scrollViewer.ScrollToVerticalOffset(offset);
    }

    private void EntityListWidgetView_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        StarResonanceDps.App.Diagnostics.HistorySwitchProbe.ScrollInputReceived("EntityList");
    }

    private void EntityListWidgetView_Loaded(object sender, RoutedEventArgs e)
    {
        if (_scrollViewer is null)
        {
            EntityListItemsControl.ApplyTemplate();
            _scrollViewer = (ScrollViewer)EntityListItemsControl.Template.FindName("EntityListScrollViewer", EntityListItemsControl);
            _scrollViewer.ScrollChanged += EntityListScrollViewer_ScrollChanged;
        }

        NotifyVerticalScrollMetricsChanged();
    }

    private void EntityListWidgetView_Unloaded(object sender, RoutedEventArgs e)
    {
        CloseEntitySelectionMenu();
    }

    private void EntityListWidgetView_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void EntityListScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        // 行の部品は別の敵の行に使い回されるので、位置が動いたら開いているメニューを閉じる
        // (開いたままだと、メニューの相手が使い回した先の敵に替わる)。
        if (e.VerticalChange != 0)
        {
            CloseEntitySelectionMenu();
            StarResonanceDps.App.Diagnostics.HistorySwitchProbe.ScrollPositionChanged();
        }

        NotifyVerticalScrollMetricsChanged();
    }

    /// <summary>
    /// 行の部品が見えている範囲から外された(別の行に使い回される)。その行がメニューを開いている行なら閉じる。
    /// 位置が動かずに行だけが外へ押し出される(敵の出入りで並びが動く)ときも、ここで拾う。
    /// </summary>
    private void EntityListItemsControl_CleanUpVirtualizedItem(object sender, CleanUpVirtualizedItemEventArgs e)
    {
        if (_openEntitySelectionEntry is not null && ReferenceEquals(e.Value, _openEntitySelectionEntry))
        {
            CloseEntitySelectionMenu();
        }
    }

    private void EntityListItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button)
        {
            OpenEntitySelectionMenu(button);
        }
    }

    private void EntityListItem_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Button button)
        {
            OpenEntitySelectionMenu(button);
            e.Handled = true;
        }
    }

    private void OpenEntitySelectionMenu(Button button)
    {
        if (button.ContextMenu is not ContextMenu menu || button.DataContext is not EntityListEntry entry)
        {
            return;
        }

        ApplyEntitySelectionMenuStyles(menu);

        if (!ReferenceEquals(_openEntitySelectionMenu, menu))
        {
            CloseEntitySelectionMenu();
        }

        menu.PlacementTarget = button;
        SetOpenEntitySelectionMenu(menu, entry);
        menu.IsOpen = true;
    }

    private void EntitySelectionContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is ContextMenu menu
            && menu.PlacementTarget is Button { DataContext: EntityListEntry entry })
        {
            SetOpenEntitySelectionMenu(menu, entry);
        }
    }

    private void EntitySelectionContextMenu_Closed(object sender, RoutedEventArgs e)
    {
        if (sender is ContextMenu menu)
        {
            ClearEntitySelectionMenu(menu);
        }
    }

    private void SetOpenEntitySelectionMenu(ContextMenu menu, EntityListEntry entry)
    {
        if (ReferenceEquals(_openEntitySelectionMenu, menu)
            && ReferenceEquals(_openEntitySelectionEntry, entry))
        {
            entry.IsEntitySelectionMenuOpen = true;
            return;
        }

        CloseEntitySelectionMenu();

        _openEntitySelectionMenu = menu;
        _openEntitySelectionEntry = entry;
        entry.PropertyChanged += OpenEntitySelectionEntry_PropertyChanged;
        entry.IsEntitySelectionMenuOpen = true;
    }

    private void CloseEntitySelectionMenu()
    {
        var menu = _openEntitySelectionMenu;
        ClearEntitySelectionMenu(menu);

        if (menu?.IsOpen == true)
        {
            menu.IsOpen = false;
        }
    }

    private void ClearEntitySelectionMenu(ContextMenu? expectedMenu)
    {
        if (expectedMenu is not null
            && !ReferenceEquals(_openEntitySelectionMenu, expectedMenu))
        {
            return;
        }

        var entry = _openEntitySelectionEntry;
        _openEntitySelectionMenu = null;
        _openEntitySelectionEntry = null;

        if (entry is null)
        {
            return;
        }

        entry.PropertyChanged -= OpenEntitySelectionEntry_PropertyChanged;
        entry.IsEntitySelectionMenuOpen = false;
    }

    private void OpenEntitySelectionEntry_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EntityListEntry.IsEntitySelectionMenuOpen)
            && sender is EntityListEntry { IsEntitySelectionMenuOpen: false } entry
            && ReferenceEquals(entry, _openEntitySelectionEntry))
        {
            CloseEntitySelectionMenu();
        }
    }

    private void ApplyEntitySelectionMenuStyles(ContextMenu menu)
    {
        if (TryFindResource(EntitySelectionContextMenuStyleKey) is Style contextMenuStyle)
        {
            menu.Style = contextMenuStyle;
        }

        var menuItems = menu.Items.OfType<MenuItem>().ToArray();
        for (var index = 0; index < menuItems.Length; index++)
        {
            var styleKey = index switch
            {
                0 => EntitySelectionContextMenuFirstItemStyleKey,
                var lastIndex when lastIndex == menuItems.Length - 1 => EntitySelectionContextMenuLastItemStyleKey,
                _ => EntitySelectionContextMenuMiddleItemStyleKey
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
