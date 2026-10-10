using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Threading;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.ViewModels;

namespace StarResonanceDps.App.Views.Widgets;

public partial class PlayerListWidgetView : UserControl, IWidgetVerticalScrollContent
{
    private const string PlayerSelectionContextMenuStyleKey = "Menu.WidgetWindowPlayerSelectionContextMenu";
    private const string PlayerSelectionContextMenuFirstItemStyleKey = "Menu.WidgetWindowPlayerSelectionContextMenuItem.First";
    private const string PlayerSelectionContextMenuMiddleItemStyleKey = "Menu.WidgetWindowPlayerSelectionContextMenuItem.Middle";
    private const string PlayerSelectionContextMenuLastItemStyleKey = "Menu.WidgetWindowPlayerSelectionContextMenuItem.Last";

    private readonly DispatcherTimer _skillRefreshTimer;
    private readonly DispatcherTimer _skillEffectRefreshTimer;

    /// <summary>一覧のテンプレートの中の ScrollViewer(仮想化のため一覧の中に置く)。読み込むまでは null。</summary>
    private ScrollViewer? _scrollViewer;

    private ContextMenu? _openPlayerSelectionMenu;
    private PlayerListEntry? _openPlayerSelectionEntry;

    public event EventHandler? VerticalScrollMetricsChanged;

    public PlayerListWidgetView()
    {
        InitializeComponent();
        _skillRefreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _skillRefreshTimer.Tick += SkillRefreshTimer_Tick;
        _skillEffectRefreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _skillEffectRefreshTimer.Tick += SkillEffectRefreshTimer_Tick;
        Loaded += PlayerListWidgetView_Loaded;
        Unloaded += PlayerListWidgetView_Unloaded;
        SizeChanged += PlayerListWidgetView_SizeChanged;
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
            42);
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

    private void PlayerListWidgetView_Loaded(object sender, RoutedEventArgs e)
    {
        if (_scrollViewer is null)
        {
            PlayerListItemsControl.ApplyTemplate();
            _scrollViewer = (ScrollViewer)PlayerListItemsControl.Template.FindName("PlayerListScrollViewer", PlayerListItemsControl);
            _scrollViewer.ScrollChanged += PlayerListScrollViewer_ScrollChanged;
        }

        _skillRefreshTimer.Start();
        _skillEffectRefreshTimer.Start();
        RefreshSkillEntries(refreshEffects: true);
        NotifyVerticalScrollMetricsChanged();
    }

    private void PlayerListWidgetView_Unloaded(object sender, RoutedEventArgs e)
    {
        _skillRefreshTimer.Stop();
        _skillEffectRefreshTimer.Stop();
        ClosePlayerSelectionMenu();
    }

    private void SkillRefreshTimer_Tick(object? sender, EventArgs e)
    {
        RefreshSkillEntries(refreshEffects: false);
    }

    private void SkillEffectRefreshTimer_Tick(object? sender, EventArgs e)
    {
        RefreshSkillEntries(refreshEffects: true);
    }

    private void RefreshSkillEntries(bool refreshEffects)
    {
        if (DataContext is WidgetListItemViewModel viewModel)
        {
            viewModel.RefreshPlayerListSkillEntries(refreshEffects);
        }
    }

    private void PlayerListWidgetView_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void PlayerListScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        // 行の部品は別の人の行に使い回されるので、位置が動いたら開いているメニューを閉じる
        // (開いたままだと、メニューの相手が使い回した先の人に替わる)。
        if (e.VerticalChange != 0)
        {
            ClosePlayerSelectionMenu();
        }

        NotifyVerticalScrollMetricsChanged();
    }

    /// <summary>
    /// 行の部品が見えている範囲から外された(別の行に使い回される)。その行がメニューを開いている行なら閉じる。
    /// 位置が動かずに行だけが外へ押し出される(顔ぶれの更新で並びが動く)ときも、ここで拾う。
    /// </summary>
    private void PlayerListItemsControl_CleanUpVirtualizedItem(object sender, CleanUpVirtualizedItemEventArgs e)
    {
        if (_openPlayerSelectionEntry is not null && ReferenceEquals(e.Value, _openPlayerSelectionEntry))
        {
            ClosePlayerSelectionMenu();
        }
    }

    /// <summary>
    /// 左クリックはメニューを開かず、その行のプレイヤー情報ウィジェットを直接開く。
    /// メニューは右クリック側が担う。
    /// </summary>
    private void PlayerListItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: WidgetListItemViewModel widget, DataContext: PlayerListEntry entry })
        {
            widget.RequestPlayerInfoCommand.Execute(entry);
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

        // 隠れている項目は数に入れない。自分の行だけに出る「ステータス詳細」を
        // Collapsed のまま数えると、見えていない項目に先頭/末尾の角丸が当たる。
        var menuItems = menu.Items.OfType<MenuItem>()
            .Where(item => item.Visibility == Visibility.Visible)
            .ToArray();
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
