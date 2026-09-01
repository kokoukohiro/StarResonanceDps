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

            // 起点は受信時刻。サーバの付与時刻を絶対時刻として使うと、
            // 通信遅延と時計のずれがそのまま差し引かれ、
            // 持続の短いバフが受信した瞬間に期限切れになる(実測: 1.1秒のバフが全滅)。
            //
            // ただし<b>同じバフ実体の再送では起点を据え置く</b>。マップ移動のたびに
            // 自分の全バフが1デルタで再送されるので、毎回受信時刻で置き直すと
            // 残り時間が満額へ巻き戻る(料理なら1800秒に戻る)。
            //
            // 同一実体かどうかは、サーバの付与時刻と持続時間が両方一致するかで見る。
            // 掛け直しなら付与時刻が変わるので、そのときは受信時刻から数え直す。
            var serverAddTime = buffEvent.AddDateTime;
            var observedAt = buffs.TryGetValue(buffUuid, out var existing)
                && existing.ServerAddTime == serverAddTime
                && existing.DurationMilliseconds == buffEvent.Duration
                    ? existing.ObservedAt
                    : DateTime.UtcNow;

            buffs[buffUuid] = new ActiveBuffEntry(
                buffEvent,
                observedAt,
                buffEvent.Duration,
                serverAddTime);
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

        var now = DateTime.UtcNow;
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

        var seconds = entry.GetRemainingSeconds(DateTime.UtcNow);
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

    /// <param name="ObservedAt">残り時間の起点。受信時刻。<b>UTC</b>。</param>
    /// <param name="ServerAddTime">
    /// サーバが送ってきた付与時刻。<b>同一実体の判定にだけ使う</b>(残り時間の計算には使わない)。
    /// </param>
    private sealed record ActiveBuffEntry(
        BuffEvent BuffEvent,
        DateTime ObservedAt,
        int DurationMilliseconds,
        DateTime ServerAddTime)
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
