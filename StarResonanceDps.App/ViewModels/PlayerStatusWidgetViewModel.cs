using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.ViewModels;

public sealed partial class PlayerStatusWidgetViewModel : PlayerWidgetWindowViewModel, IDisposable
{
    private PlayerStatusWidgetSettingsConfig _settings;
    private IReadOnlyList<int> _rowOrder;
    private PlayerRosterEntry? _player;
    private bool _isRowDragging;
    private bool _needsRefreshAfterDrag;
    private bool _isDisposed;

    [ObservableProperty]
    private IReadOnlyList<PlayerStatusRow> _playerStatus = [];

    public PlayerStatusWidgetViewModel(
        WidgetListItemViewModel statusWidget,
        long? requestedCharacterId,
        PlayerRosterEntry? initialPlayer)
        : base(statusWidget, requestedCharacterId, showPlayerIdentityInHeader: false)
    {
        _settings = statusWidget.GetPlayerStatusSettingsSnapshot();
        _rowOrder = statusWidget.GetPlayerStatusRowOrderSnapshot();
        statusWidget.PlayerStatusSettingsChanged += Widget_PlayerStatusSettingsChanged;
        InitializePlayer(initialPlayer);
    }

    public WidgetListItemViewModel StatusWidget => PlayerWidget;

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        StatusWidget.PlayerStatusSettingsChanged -= Widget_PlayerStatusSettingsChanged;
    }

    protected override void OnSelectedPlayerChanged(PlayerRosterEntry? player)
    {
        _player = player;
        RefreshRows();
    }

    /// <summary>表示設定(保存かプレビュー)が変わった。行を作り直す。</summary>
    private void Widget_PlayerStatusSettingsChanged(object? sender, EventArgs e)
    {
        _settings = StatusWidget.GetPlayerStatusSettingsSnapshot();
        RefreshRows();
    }

    private void RefreshRows()
    {
        if (_isRowDragging)
        {
            _needsRefreshAfterDrag = true;
            return;
        }

        PlayerStatus = _player is null
            ? []
            : PlayerStatusEntry.Create(
                _player,
                _settings.HideInactiveStatusEffects
                    ?? WidgetConfigDefaults.DefaultHideInactiveStatusEffects,
                _settings.RowVisibility,
                CreateTextBrushes(_settings.TextColors),
                _rowOrder);
    }

    /// <summary>
    /// 行を掴んでいる間は作り直しを止める。属性は届くたびに行を差し替えるので、
    /// そのままだと<b>掴んでいるコンテナが visual tree から外れて手が離れる</b>。
    /// </summary>
    public void BeginRowDrag()
    {
        _isRowDragging = true;
    }

    /// <summary>掴んだ手を離した。止めている間に来た更新があれば、ここで1回だけ反映する。</summary>
    public void EndRowDrag()
    {
        _isRowDragging = false;
        if (!_needsRefreshAfterDrag)
        {
            return;
        }

        _needsRefreshAfterDrag = false;
        RefreshRows();
    }

    /// <summary>
    /// 掴んだ行を別の位置へ落とす。<b>隠れている行も含めた全体の並び</b>の中で動かし、
    /// その場で保存する(ウィンドウの位置と同じ扱い)。
    /// </summary>
    public void MoveRow(int fromIndex, int toIndex)
    {
        var rows = PlayerStatus;
        if (fromIndex < 0 || toIndex < 0
            || fromIndex >= rows.Count || toIndex >= rows.Count
            || fromIndex == toIndex)
        {
            return;
        }

        var fromUnit = rows[fromIndex].OrderUnitAttrId;
        var toUnit = rows[toIndex].OrderUnitAttrId;

        var order = new List<int>(_rowOrder);
        var from = order.IndexOf(fromUnit);
        var to = order.IndexOf(toUnit);
        if (from < 0 || to < 0 || from == to)
        {
            return;
        }

        order.RemoveAt(from);

        // 下へ動かすなら落とした行の後ろ、上へ動かすなら前へ入れる。
        var insertAt = order.IndexOf(toUnit);
        if (from < to)
        {
            insertAt += 1;
        }

        order.Insert(insertAt, fromUnit);

        StatusWidget.SavePlayerStatusRowOrder(order);
        _rowOrder = StatusWidget.GetPlayerStatusRowOrderSnapshot();
        RefreshRows();
    }

    /// <summary>設定の各行の選択色を刷にする。<see cref="PlayerStatusEntry.Create"/> が並びを繰り返して配る。</summary>
    private static IReadOnlyList<Brush> CreateTextBrushes(IReadOnlyList<PlayerStatusTextColorConfig>? textColors)
    {
        if (textColors is null || textColors.Count == 0)
        {
            return [];
        }

        var brushes = new List<Brush>(textColors.Count);
        foreach (var row in textColors)
        {
            var palette = row.Palette;
            if (palette is null || palette.Count == 0)
            {
                continue;
            }

            var hex = palette[Math.Clamp(row.SelectedIndex, 0, palette.Count - 1)];
            if (!ColorUtilities.TryParseHex(hex, out var color))
            {
                continue;
            }

            var brush = new SolidColorBrush(color);
            brush.Freeze();
            brushes.Add(brush);
        }

        return brushes;
    }
}
