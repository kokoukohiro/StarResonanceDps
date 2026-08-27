using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.Core.Services;

/// <summary>
/// 現在有効なバフ/デバフのライブ状態。
///
/// <para>
/// <see cref="Encounter"/> は計測の単位なので、戦闘終了やダンジョン勝利のたびに作り直され、
/// その <c>BuffEvents</c> は引き継がれない。しかしバフはゲーム世界側の状態であって
/// 計測の区切りでは消えないため、常時表示のウィジェットがエンカウンターを参照すると
/// 境界で表示が空になる。ここはその境界から独立した保管場所。
/// </para>
///
/// <para>
/// 残り時間の基準には<b>このストアが書き込みを受けたローカル時刻</b>を使う。
/// <see cref="BuffEvent.AddDateTime"/> はサーバ由来の <c>creationTime</c> が入る経路があり
/// ローカル時計と基準がずれるため、期限判定には使えない。
/// <see cref="BuffEvent.EventAddTime"/> はエンカウンター相対なので境界を跨げない。
/// 自前で観測時刻を持つのが、どちらの問題にも影響されない唯一の方法。
/// </para>
///
/// <para>
/// 統計用の履歴は従来どおり <c>Encounter.Entities[].BuffEvents</c> に残る。ここはライブ表示専用。
/// </para>
/// </summary>
public sealed class ActiveBuffStore
{
    private static readonly Lazy<ActiveBuffStore> LazyInstance = new(() => new ActiveBuffStore());

    private readonly object _sync = new();
    private readonly Dictionary<long, Dictionary<ulong, ActiveBuffEntry>> _buffsByEntity = [];

    /// <summary>
    /// エンティティごとの BaseId -> SourceConfigId の対応。バフからスキルを逆引きする多段解決に使う。
    /// 期限切れや解除で消えたバフの分も残す(索引であって表示ソースではない)。
    /// これが痩せると、直接一致しないスキル(ロールスキル等)の紐付けが解決できなくなる。
    /// </summary>
    private readonly Dictionary<long, Dictionary<int, HashSet<int>>> _sourceParentsByEntity = [];

    private ActiveBuffStore()
    {
    }

    public static ActiveBuffStore Instance => LazyInstance.Value;

    public void AddOrUpdate(long entityUuid, ulong buffUuid, BuffEvent buffEvent)
    {
        if (entityUuid == 0 || buffEvent is null)
        {
            return;
        }

        lock (_sync)
        {
            if (!_buffsByEntity.TryGetValue(entityUuid, out var buffs))
            {
                buffs = [];
                _buffsByEntity.Add(entityUuid, buffs);
            }

            buffs[buffUuid] = new ActiveBuffEntry(buffEvent, DateTime.Now, buffEvent.Duration);
            RecordSourceParentNoLock(entityUuid, buffEvent);
        }
    }

    private void RecordSourceParentNoLock(long entityUuid, BuffEvent buffEvent)
    {
        if (buffEvent.BaseId <= 0
            || buffEvent.SourceConfigId <= 0
            || buffEvent.BaseId == buffEvent.SourceConfigId)
        {
            return;
        }

        if (!_sourceParentsByEntity.TryGetValue(entityUuid, out var parents))
        {
            parents = [];
            _sourceParentsByEntity.Add(entityUuid, parents);
        }

        if (!parents.TryGetValue(buffEvent.BaseId, out var sourceIds))
        {
            sourceIds = [];
            parents.Add(buffEvent.BaseId, sourceIds);
        }

        sourceIds.Add(buffEvent.SourceConfigId);
    }

    /// <summary>
    /// 蓄積した BaseId -> SourceConfigId の対応を呼び出し側の索引へ合流させる。
    /// </summary>
    public void CopySourceParentsInto(long entityUuid, Dictionary<int, HashSet<int>> target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (entityUuid == 0)
        {
            return;
        }

        lock (_sync)
        {
            if (!_sourceParentsByEntity.TryGetValue(entityUuid, out var parents))
            {
                return;
            }

            foreach (var pair in parents)
            {
                if (!target.TryGetValue(pair.Key, out var sourceIds))
                {
                    sourceIds = [];
                    target.Add(pair.Key, sourceIds);
                }

                sourceIds.UnionWith(pair.Value);
            }
        }
    }

    public void Remove(long entityUuid, ulong buffUuid)
    {
        if (entityUuid == 0)
        {
            return;
        }

        lock (_sync)
        {
            if (!_buffsByEntity.TryGetValue(entityUuid, out var buffs))
            {
                return;
            }

            buffs.Remove(buffUuid);
            if (buffs.Count == 0)
            {
                _buffsByEntity.Remove(entityUuid);
            }
        }
    }

    /// <summary>期限切れを除いた、現在有効なバフを返す。</summary>
    public IReadOnlyList<BuffEvent> GetActive(long entityUuid)
    {
        if (entityUuid == 0)
        {
            return Array.Empty<BuffEvent>();
        }

        ActiveBuffEntry[] entries;
        lock (_sync)
        {
            if (!_buffsByEntity.TryGetValue(entityUuid, out var buffs) || buffs.Count == 0)
            {
                return Array.Empty<BuffEvent>();
            }

            entries = [.. buffs.Values];
        }

        var now = DateTime.Now;
        return [.. entries.Where(entry => !entry.IsExpired(now)).Select(entry => entry.BuffEvent)];
    }

    /// <summary>
    /// 残り秒数を返す。期限切れは false。
    ///
    /// <para>
    /// 持続時間が 0 以下で届いたものは <paramref name="remainingSeconds"/> に <c>null</c> を入れて
    /// true を返す。<b>「残り0秒」と「持続時間が無い」を同じ 0 で表すと区別できない</b>ため、
    /// 後者は null で表す(表示側は数字を出さない)。
    /// </para>
    /// </summary>
    public bool TryGetRemainingSeconds(long entityUuid, ulong buffUuid, out double? remainingSeconds)
    {
        remainingSeconds = null;
        if (entityUuid == 0)
        {
            return false;
        }

        ActiveBuffEntry? entry;
        lock (_sync)
        {
            entry = _buffsByEntity.TryGetValue(entityUuid, out var buffs)
                && buffs.TryGetValue(buffUuid, out var found)
                    ? found
                    : null;
        }

        if (entry is null)
        {
            return false;
        }

        if (entry.DurationMilliseconds <= 0)
        {
            // 持続時間なし。null のまま true を返し、表示は残す。
            return true;
        }

        var seconds = entry.GetRemainingSeconds(DateTime.Now);
        if (seconds <= 0d)
        {
            return false;
        }

        remainingSeconds = seconds;
        return true;
    }

    public void RemoveEntity(long entityUuid)
    {
        if (entityUuid == 0)
        {
            return;
        }

        lock (_sync)
        {
            _buffsByEntity.Remove(entityUuid);
            _sourceParentsByEntity.Remove(entityUuid);
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _buffsByEntity.Clear();
            _sourceParentsByEntity.Clear();
        }
    }

    private sealed record ActiveBuffEntry(
        BuffEvent BuffEvent,
        DateTime ObservedAt,
        int DurationMilliseconds)
    {
        public double GetRemainingSeconds(DateTime now)
        {
            return (ObservedAt + TimeSpan.FromMilliseconds(DurationMilliseconds) - now).TotalSeconds;
        }

        /// <summary>持続時間不明(0以下)は無期限扱いで残す。</summary>
        public bool IsExpired(DateTime now)
        {
            return DurationMilliseconds > 0 && GetRemainingSeconds(now) <= 0d;
        }
    }
}
