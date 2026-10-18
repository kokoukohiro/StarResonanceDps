using System.Collections.ObjectModel;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.ViewModels;

/// <summary>
/// アビリティ情報・潜在心相晶情報・モジュール情報。プレイヤーに乗っている根のバフ(名簿の行の <see cref="PlayerRosterEntry.RootBuffData"/>)のうち、
/// このウィジェットの種類のものを、種類の順・表の順で1行ずつ出す。根のバフの一覧をまだ受けていなければ何も出さない。
///
/// <para>
/// 名簿の行は HP などで頻繁に入れ替わるので、行の中身(名前・絵)を作り直して前の中身と値で比べ、違う行だけ差し替える。
/// 根のバフは時間では変わらないのでタイマーは持たない。
/// 相手が一覧から消えても(パーティ外で周りから外れた)最後に届いた値のまま残し、ログアウトで捨てる(プレイヤー情報と同じ)。
/// </para>
/// </summary>
public sealed class PlayerRootBuffWidgetViewModel : PlayerWidgetWindowViewModel, IDisposable
{
    private readonly ObservableCollection<PlayerRootBuffEntry> _entries = [];

    /// <summary>出す根のバフの種類。並びは行の並び。</summary>
    private readonly RootBuffKind[] _kinds;

    private PlayerRosterEntry? _player;
    private bool _isDisposed;

    public PlayerRootBuffWidgetViewModel(
        WidgetListItemViewModel widget,
        long? requestedCharacterId,
        PlayerRosterEntry? initialPlayer,
        IReadOnlyList<RootBuffKind> kinds)
        : base(widget, requestedCharacterId)
    {
        _kinds = [.. kinds];
        Entries = new ReadOnlyObservableCollection<PlayerRootBuffEntry>(_entries);
        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;
        InitializePlayer(initialPlayer);
    }

    public ReadOnlyObservableCollection<PlayerRootBuffEntry> Entries { get; }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;
    }

    /// <summary>相手が一覧から消えても最後に届いた値のまま残す(プレイヤー情報と同じ)。</summary>
    protected override bool KeepsLastPlayerWhenRemoved => true;

    protected override void OnSelectedPlayerChanged(PlayerRosterEntry? player)
    {
        _player = player;
        Refresh();
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        Refresh();
    }

    private void Refresh()
    {
        if (_isDisposed)
        {
            return;
        }

        var entries = CreateEntries(_player);

        // 位置ごとに当て直し、増えたぶんだけ足し、減ったぶんだけ外す(中身が同じ行は差し替えない)。
        for (var index = 0; index < entries.Count; index++)
        {
            if (index < _entries.Count)
            {
                if (!Equals(_entries[index], entries[index]))
                {
                    _entries[index] = entries[index];
                }
            }
            else
            {
                _entries.Add(entries[index]);
            }
        }

        while (_entries.Count > entries.Count)
        {
            _entries.RemoveAt(_entries.Count - 1);
        }
    }

    private List<PlayerRootBuffEntry> CreateEntries(PlayerRosterEntry? player)
    {
        var rootBuffData = player?.RootBuffData;
        if (rootBuffData is null)
        {
            return [];
        }

        var rows = new List<(int KindIndex, int Order, int BuffId, RootBuffKind Kind)>();
        foreach (var buffId in rootBuffData.BuffIds)
        {
            if (!CombatDataCatalog.TryGetRootBuffKind(buffId, out var kind, out var order))
            {
                continue;
            }

            var kindIndex = Array.IndexOf(_kinds, kind);
            if (kindIndex >= 0)
            {
                rows.Add((kindIndex, order, buffId, kind));
            }
        }

        rows.Sort((left, right) => left.KindIndex != right.KindIndex
            ? left.KindIndex.CompareTo(right.KindIndex)
            : left.Order.CompareTo(right.Order));

        var seasonTalentBuffId = player!.SeasonTalentBuffId;
        var entries = new List<PlayerRootBuffEntry>(rows.Count);
        foreach (var (_, _, buffId, kind) in rows)
        {
            var iconName = CombatDataCatalog.GetRootBuffIconName(buffId, seasonTalentBuffId);
            entries.Add(new PlayerRootBuffEntry(
                CombatDataCatalog.GetRootBuffName(buffId),
                CombatIconResolver.ResolveRootBuffIcon(kind, iconName),
                kind,
                CombatDataCatalog.GetRootBuffTalentRole(buffId)));
        }

        return entries;
    }
}
