using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using StarResonanceDps.App.Models.Widgets;

namespace StarResonanceDps.App.Views.Widgets;

public partial class PlayerListWidgetView : UserControl, IWidgetVerticalScrollContent
{
    private const string PlayerSelectionContextMenuStyleKey = "Menu.WidgetWindowPlayerSelectionContextMenu";
    private const string PlayerSelectionContextMenuFirstItemStyleKey = "Menu.WidgetWindowPlayerSelectionContextMenuItem.First";
    private const string PlayerSelectionContextMenuMiddleItemStyleKey = "Menu.WidgetWindowPlayerSelectionContextMenuItem.Middle";
    private const string PlayerSelectionContextMenuLastItemStyleKey = "Menu.WidgetWindowPlayerSelectionContextMenuItem.Last";

    private ContextMenu? _openPlayerSelectionMenu;
    private PlayerListEntry? _openPlayerSelectionEntry;

    public event EventHandler? VerticalScrollMetricsChanged;

    public PlayerListWidgetView()
    {
        InitializeComponent();
        Loaded += PlayerListWidgetView_Loaded;
        Unloaded += PlayerListWidgetView_Unloaded;
        SizeChanged += PlayerListWidgetView_SizeChanged;
    }

    public WidgetVerticalScrollMetrics GetVerticalScrollMetrics()
    {
        var maximum = Math.Max(PlayerListScrollViewer.ScrollableHeight, 0);
        var viewport = Math.Max(PlayerListScrollViewer.ViewportHeight, 0);

        return new WidgetVerticalScrollMetrics(
            maximum,
            viewport,
            Math.Min(PlayerListScrollViewer.VerticalOffset, maximum),
            Math.Max(viewport * 0.9, 1),
            42);
    }

    public void SetVerticalScrollOffset(double verticalOffset)
    {
        var maximum = Math.Max(PlayerListScrollViewer.ScrollableHeight, 0);
        var offset = double.IsFinite(verticalOffset)
            ? Math.Clamp(verticalOffset, 0, maximum)
            : 0;

        PlayerListScrollViewer.ScrollToVerticalOffset(offset);
    }

    private void PlayerListWidgetView_Loaded(object sender, RoutedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void PlayerListWidgetView_Unloaded(object sender, RoutedEventArgs e)
    {
        ClosePlayerSelectionMenu();
    }

    private void PlayerListWidgetView_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void PlayerListScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void PlayerListItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button)
        {
            OpenPlayerSelectionMenu(button);
        }
    }

    private void PlayerListItem_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Button button)
        {
            OpenPlayerSelectionMenu(button);
            e.Handled = true;
        }
    }

    private void OpenPlayerSelectionMenu(Button button)
    {
        if (button.ContextMenu is not ContextMenu menu || button.DataContext is not PlayerListEntry entry)
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
            && menu.PlacementTarget is Button { DataContext: PlayerListEntry entry })
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

    private void SetOpenPlayerSelectionMenu(ContextMenu menu, PlayerListEntry entry)
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

    private void OpenPlayerSelectionEntry_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlayerListEntry.IsPlayerSelectionMenuOpen)
            && sender is PlayerListEntry { IsPlayerSelectionMenuOpen: false } entry
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

        var menuItems = menu.Items.OfType<MenuItem>().ToArray();
        for (var index = 0; index < menuItems.Length; index++)
        {
            var styleKey = index switch
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
