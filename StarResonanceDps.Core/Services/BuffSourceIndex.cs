using System.Collections.Generic;

namespace StarResonanceDps.Core.Services;

/// <summary>
/// いま生きているバフ実体を「術者 × バフID」で引けるようにする索引。
///
/// <para>
/// ダメージ・回復のイベントは発生源をバフの<b>種類</b>(<c>OwnerId</c> = baseId)までしか名乗らない。
/// 親スキルを運ぶのは実体側の <c>FightSourceInfo</c> だけで、しかも同じバフが別のスキルから
/// 作られることがある(実測: <c>21423</c> は <c>1531/1541/1561</c>、<c>55302</c> は <c>2307/2361</c>)。
/// だから静的な「バフID → スキルID」の表では決まらず、実体を引く必要がある。
/// </para>
///
/// <para>
/// <b>履歴は持たない。</b> 保持するのは生存中の実体だけで、除去イベントで即消し、
/// 失効ぶんは参照時に刈る。エンカウンター全体ぶんを溜めない。
/// </para>
///
/// <para>
/// 実体の鍵は <c>(保持者, BuffUuid)</c>。<c>BuffUuid</c> は種別をまたいで使い回されるため
/// (実測で21,196回・同一UUIDが最大314回)、単体では別の実体を消してしまう。
/// </para>
/// </summary>
public sealed class BuffSourceIndex
{
    /// <summary>持続が分からない実体を、いつまで生きているとみなすか。</summary>
    private static readonly TimeSpan UnknownDurationLifetime = TimeSpan.FromSeconds(30);

    /// <summary>失効の判定に持たせる猶予。イベントが持続の端に乗ることがあるため。</summary>
    private static readonly TimeSpan ExpiryGrace = TimeSpan.FromMilliseconds(300);

    /// <summary>刈り取りを走らせる間隔。</summary>
    private static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(10);

    public static BuffSourceIndex Instance { get; } = new();

    private readonly object _gate = new();

    /// <summary>(術者, バフID) → 生きている実体。</summary>
    private readonly Dictionary<(long Caster, int BaseId), List<LiveBuff>> _live = [];

    /// <summary>(保持者, BuffUuid) → どの束に入っているか。除去で引くために持つ。</summary>
    private readonly Dictionary<(long Holder, int BuffUuid), (long Caster, int BaseId)> _byInstance = [];

    /// <summary>
    /// バフID → 直近に観測した発生源。鎖の中継(バフがバフを作る)を辿るのに使う。
    /// 実体ごとではなく型ごとに1件だけ持つ。
    /// </summary>
    private readonly Dictionary<int, (int FightSourceType, int SourceConfigId)> _lastSourceByBaseId = [];

    private DateTime _lastSweep = DateTime.MinValue;

    /// <summary>生きているバフ実体1つぶん。</summary>
    public sealed class LiveBuff
    {
        public required long HolderUuid { get; init; }
        public required int BuffUuid { get; init; }
        public required DateTime AddTime { get; init; }
        public required DateTime EndTime { get; init; }
        public required int FightSourceType { get; init; }
        public required int SourceConfigId { get; init; }
    }

    /// <summary>バフの付与を取り込む。</summary>
    public void Add(
        long casterUuid,
        long holderUuid,
        int buffUuid,
        int baseId,
        int fightSourceType,
        int sourceConfigId,
        int durationMilliseconds,
        DateTime now)
    {
        if (casterUuid == 0 || baseId == 0)
        {
            return;
        }

        var end = durationMilliseconds > 0
            ? now.AddMilliseconds(durationMilliseconds)
            : now.Add(UnknownDurationLifetime);

        var entry = new LiveBuff
        {
            HolderUuid = holderUuid,
            BuffUuid = buffUuid,
            AddTime = now,
            EndTime = end,
            FightSourceType = fightSourceType,
            SourceConfigId = sourceConfigId,
        };

        lock (_gate)
        {
            // 同じ実体が既に別の束にいるなら先に外す。BuffUuid は使い回される。
            RemoveNoLock(holderUuid, buffUuid);

            var key = (casterUuid, baseId);
            if (!_live.TryGetValue(key, out var list))
            {
                list = [];
                _live[key] = list;
            }

            list.Add(entry);
            _byInstance[(holderUuid, buffUuid)] = key;

            if (sourceConfigId != 0)
            {
                _lastSourceByBaseId[baseId] = (fightSourceType, sourceConfigId);
            }

            SweepIfDueNoLock(now);
        }
    }

    /// <summary>バフの除去を取り込む。除去イベントは <c>BuffUuid</c> しか運ばない。</summary>
    public void Remove(long holderUuid, int buffUuid)
    {
        lock (_gate)
        {
            RemoveNoLock(holderUuid, buffUuid);
        }
    }

    /// <summary>
    /// その術者が出した、指定バフIDの生きている実体を返す。失効ぶんはここで刈る。
    /// </summary>
    public IReadOnlyList<LiveBuff> GetLive(long casterUuid, int baseId, DateTime now)
    {
        lock (_gate)
        {
            var key = (casterUuid, baseId);
            if (!_live.TryGetValue(key, out var list))
            {
                return [];
            }

            PruneNoLock(key, list, now);
            return list.Count == 0 ? [] : list.ToArray();
        }
    }

    /// <summary>バフID → 直近に観測した発生源。鎖の中継用。</summary>
    public bool TryGetLastSource(int baseId, out int fightSourceType, out int sourceConfigId)
    {
        lock (_gate)
        {
            if (_lastSourceByBaseId.TryGetValue(baseId, out var found))
            {
                fightSourceType = found.FightSourceType;
                sourceConfigId = found.SourceConfigId;
                return true;
            }
        }

        fightSourceType = 0;
        sourceConfigId = 0;
        return false;
    }

    /// <summary>マップ・チャンネル切替で捨てる。</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _live.Clear();
            _byInstance.Clear();
            _lastSourceByBaseId.Clear();
        }
    }

    private void RemoveNoLock(long holderUuid, int buffUuid)
    {
        var instanceKey = (holderUuid, buffUuid);
        if (!_byInstance.TryGetValue(instanceKey, out var key))
        {
            return;
        }

        _byInstance.Remove(instanceKey);

        if (!_live.TryGetValue(key, out var list))
        {
            return;
        }

        for (var index = list.Count - 1; index >= 0; index--)
        {
            if (list[index].HolderUuid == holderUuid && list[index].BuffUuid == buffUuid)
            {
                list.RemoveAt(index);
            }
        }

        if (list.Count == 0)
        {
            _live.Remove(key);
        }
    }

    private void PruneNoLock((long Caster, int BaseId) key, List<LiveBuff> list, DateTime now)
    {
        for (var index = list.Count - 1; index >= 0; index--)
        {
            if (now <= list[index].EndTime.Add(ExpiryGrace))
            {
                continue;
            }

            _byInstance.Remove((list[index].HolderUuid, list[index].BuffUuid));
            list.RemoveAt(index);
        }

        if (list.Count == 0)
        {
            _live.Remove(key);
        }
    }

    /// <summary>
    /// 除去イベントが来ないまま消えた実体を回収する。参照されない束は刈られないため。
    /// </summary>
    private void SweepIfDueNoLock(DateTime now)
    {
        if (now - _lastSweep < SweepInterval)
        {
            return;
        }

        _lastSweep = now;

        List<(long Caster, int BaseId)>? empties = null;
        foreach (var pair in _live)
        {
            var list = pair.Value;
            for (var index = list.Count - 1; index >= 0; index--)
            {
                if (now <= list[index].EndTime.Add(ExpiryGrace))
                {
                    continue;
                }

                _byInstance.Remove((list[index].HolderUuid, list[index].BuffUuid));
                list.RemoveAt(index);
            }

            if (list.Count == 0)
            {
                (empties ??= []).Add(pair.Key);
            }
        }

        if (empties is null)
        {
            return;
        }

        foreach (var key in empties)
        {
            _live.Remove(key);
        }
    }
}
