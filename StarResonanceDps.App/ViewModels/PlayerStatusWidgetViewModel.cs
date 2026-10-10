using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Media;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.ViewModels;

public sealed class PlayerStatusWidgetViewModel : PlayerWidgetWindowViewModel, IDisposable
{
    private PlayerStatusWidgetSettingsConfig _settings;

    /// <summary>行の色。設定が変わったときだけ作る(毎回作ると、色が同じでも行の色の差し替えになる)。</summary>
    private IReadOnlyDictionary<int, Brush> _textBrushes;

    private IReadOnlyList<int> _rowOrder;
    private PlayerRosterEntry? _player;
    private bool _isRowDragging;
    private bool _needsRefreshAfterDrag;
    private bool _isDisposed;

    public PlayerStatusWidgetViewModel(
        WidgetListItemViewModel statusWidget,
        long? requestedCharacterId,
        PlayerRosterEntry? initialPlayer)
        : base(statusWidget, requestedCharacterId, showPlayerIdentityInHeader: false)
    {
        _settings = statusWidget.GetPlayerStatusSettingsSnapshot();
        _textBrushes = CreateTextBrushes(_settings);
        _rowOrder = statusWidget.GetPlayerStatusRowOrderSnapshot();
        statusWidget.PlayerStatusSettingsChanged += Widget_PlayerStatusSettingsChanged;
        InitializePlayer(initialPlayer);
    }

    public WidgetListItemViewModel StatusWidget => PlayerWidget;

    /// <summary>表示する行。行の部品は使い回し、更新では値だけを入れ替える(<see cref="SynchronizeRows"/>)。</summary>
    public ObservableCollection<PlayerStatusRowItem> PlayerStatus { get; } = [];

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

    /// <summary>表示設定(保存かプレビュー)が変わった。色を作り直して行に当て直す。</summary>
    private void Widget_PlayerStatusSettingsChanged(object? sender, EventArgs e)
    {
        _settings = StatusWidget.GetPlayerStatusSettingsSnapshot();
        _textBrushes = CreateTextBrushes(_settings);
        RefreshRows();
    }

    private void RefreshRows()
    {
        if (_isRowDragging)
        {
            _needsRefreshAfterDrag = true;
            return;
        }

        SynchronizeRows(_player is null
            ? []
            : PlayerStatusEntry.Create(
                _player,
                _settings.HideInactiveStatusEffects
                    ?? WidgetConfigDefaults.DefaultHideInactiveStatusEffects,
                _settings.RowVisibility,
                _textBrushes,
                _rowOrder));
    }

    /// <summary>
    /// 行を位置ごとに当て直す。行の部品は作り直さず値だけを入れ替え、増えた分だけ足し、減った分だけ末尾から外す
    /// (行が途中で出入りしても、後ろの行は値が1つずれて入るだけ)。
    /// </summary>
    private void SynchronizeRows(IReadOnlyList<PlayerStatusRow> rows)
    {
        for (var index = 0; index < rows.Count; index++)
        {
            if (index < PlayerStatus.Count)
            {
                PlayerStatus[index].Apply(rows[index]);
                continue;
            }

            var item = new PlayerStatusRowItem();
            item.Apply(rows[index]);
            PlayerStatus.Add(item);
        }

        while (PlayerStatus.Count > rows.Count)
        {
            PlayerStatus.RemoveAt(PlayerStatus.Count - 1);
        }
    }

    /// <summary>
    /// 行を掴んでいる間は行の当て直しを止める。行の数が減ると末尾の行の部品が外れるので、
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

    /// <summary>設定の各行の選択色を、行の鍵ごとの刷にする。</summary>
    private static IReadOnlyDictionary<int, Brush> CreateTextBrushes(PlayerStatusWidgetSettingsConfig settings)
    {
        var brushes = new Dictionary<int, Brush>(PlayerStatusEntry.SettingRowAttrIds.Count);
        var palettes = settings.TextColorPalettes;
        var indexes = settings.TextColorIndexes;
        if (palettes is null)
        {
            return brushes;
        }

        foreach (var attrId in PlayerStatusEntry.SettingRowAttrIds)
        {
            var key = attrId.ToString(CultureInfo.InvariantCulture);
            if (!palettes.TryGetValue(key, out var palette) || palette is not { Count: > 0 })
            {
                continue;
            }

            var index = indexes is not null && indexes.TryGetValue(key, out var saved)
                ? saved
                : WidgetConfigDefaults.DefaultPlayerStatusTextColorIndex;

            if (!ColorUtilities.TryParseHex(palette[Math.Clamp(index, 0, palette.Count - 1)], out var color))
            {
                continue;
            }

            var brush = new SolidColorBrush(color);
            brush.Freeze();
            brushes[attrId] = brush;
        }

        return brushes;
    }
}
