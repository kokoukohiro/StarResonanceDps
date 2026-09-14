using Zproto;

namespace StarResonanceDps.Core.Services;

/// <summary>
/// 生きているバフ実体の索引。<b>敵も含めた全エンティティ</b>の付与を持ち、除去で消す。
/// 被ダメログが、バフ由来の被ダメを受けた瞬間にそのバフの付与元(技)を引くのに使う。
///
/// <para>
/// 実体の鍵は <c>(保持者, BuffUuid)</c>。<c>BuffUuid</c> は種別をまたいで使い回されるので単体では鍵にならない。
/// </para>
///
/// <para>
/// 除去が届かない実体があるので、持続が正のものは持続が切れて <see cref="ExpiryGrace"/> 過ぎたら掃除する。
/// 持続が0以下のものは除去かマップ切替でしか消えない。
/// 時刻はすべてパケットの到着時刻(UTC)で、付与と参照で同じ基準を使う。
/// </para>
///
/// <para>
/// <see cref="ActiveBuffStore"/> はプレイヤーのバフの表示用で、敵のバフも付与元も持たない。別物。
/// </para>
/// </summary>
public sealed class BuffInstanceIndex
{
    private static readonly Lazy<BuffInstanceIndex> LazyInstance = new(() => new BuffInstanceIndex());

    private static readonly TimeSpan ExpiryGrace = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(10);

    private readonly object _sync = new();
    private readonly Dictionary<(long Holder, int BuffUuid), BuffInstance> _instances = [];
    private readonly Dictionary<int, HashSet<(long Holder, int BuffUuid)>> _keysByBaseId = [];
    private DateTime _lastSweep = DateTime.MinValue;

    private readonly record struct BuffInstance(
        int BaseId,
        long FireUuid,
        int FightSourceType,
        int SourceConfigId,
        DateTime? ExpiresAt);

    private BuffInstanceIndex()
    {
    }

    public static BuffInstanceIndex Instance => LazyInstance.Value;

    /// <summary>付与(差分の <c>BuffEffectAddBuff</c> と、出現時の全バフスナップショット)。</summary>
    public void Add(
        long holderUuid,
        int buffUuid,
        int baseId,
        long fireUuid,
        int fightSourceType,
        int sourceConfigId,
        int durationMilliseconds,
        DateTime arrivalTime)
    {
        if (baseId <= 0)
        {
            return;
        }

        var key = (holderUuid, buffUuid);
        DateTime? expiresAt = durationMilliseconds > 0
            ? arrivalTime.AddMilliseconds(durationMilliseconds) + ExpiryGrace
            : null;

        lock (_sync)
        {
            if (_instances.TryGetValue(key, out var previous))
            {
                RemoveBaseIdKeyNoLock(previous.BaseId, key);
            }

            _instances[key] = new BuffInstance(baseId, fireUuid, fightSourceType, sourceConfigId, expiresAt);
            if (!_keysByBaseId.TryGetValue(baseId, out var keys))
            {
                keys = [];
                _keysByBaseId[baseId] = keys;
            }

            keys.Add(key);

            if (arrivalTime - _lastSweep >= SweepInterval)
            {
                SweepNoLock(arrivalTime);
                _lastSweep = arrivalTime;
            }
        }
    }

    /// <summary>除去(<c>BuffEventRemove</c>)。除去イベントは <c>BuffUuid</c> しか運ばない。</summary>
    public void Remove(long holderUuid, int buffUuid)
    {
        var key = (holderUuid, buffUuid);
        lock (_sync)
        {
            if (_instances.Remove(key, out var removed))
            {
                RemoveBaseIdKeyNoLock(removed.BaseId, key);
            }
        }
    }

    /// <summary>マップ切替と計測の停止で空にする。</summary>
    public void Clear()
    {
        lock (_sync)
        {
            _instances.Clear();
            _keysByBaseId.Clear();
            _lastSweep = DateTime.MinValue;
        }
    }

    /// <summary>
    /// バフ由来のダメージ1件について、そのバフを付けた技を引く。
    ///
    /// <para>
    /// 候補は、同じ <paramref name="baseId"/> の生きている実体のうち術者が
    /// <paramref name="attackerRawUuid"/>(ダメージを出した実体そのもの)か
    /// <paramref name="attackerUuid"/>(召喚元へ寄せた加害者)のもの。
    /// <b>候補の付与元が1つにそろい、その種別が技のときだけ</b>その技IDを返す。
    /// 候補が無い・割れる・技以外(親のバフ・タレントなど)なら false。
    /// </para>
    ///
    /// <para>
    /// 保持者は条件にしない。被弾した本人に乗っていない実体が多い(召喚体が自分に掛けて持つオーラなど)。
    /// </para>
    /// </summary>
    public bool TryResolveSourceSkill(
        int baseId,
        long attackerRawUuid,
        long attackerUuid,
        DateTime arrivalTime,
        out int skillId)
    {
        skillId = 0;
        lock (_sync)
        {
            if (!_keysByBaseId.TryGetValue(baseId, out var keys))
            {
                return false;
            }

            (int FightSourceType, int SourceConfigId)? source = null;
            foreach (var key in keys)
            {
                var instance = _instances[key];
                if (instance.ExpiresAt < arrivalTime
                    || (instance.FireUuid != attackerRawUuid && instance.FireUuid != attackerUuid))
                {
                    continue;
                }

                var candidate = (instance.FightSourceType, instance.SourceConfigId);
                if (source is null)
                {
                    source = candidate;
                }
                else if (source.Value != candidate)
                {
                    return false;
                }
            }

            if (source is null
                || source.Value.FightSourceType != (int)EFightSource.Skill
                || source.Value.SourceConfigId <= 0)
            {
                return false;
            }

            skillId = source.Value.SourceConfigId;
            return true;
        }
    }

    private void SweepNoLock(DateTime arrivalTime)
    {
        List<(long Holder, int BuffUuid)>? expired = null;
        foreach (var (key, instance) in _instances)
        {
            if (instance.ExpiresAt < arrivalTime)
            {
                (expired ??= []).Add(key);
            }
        }

        if (expired is null)
        {
            return;
        }

        foreach (var key in expired)
        {
            var instance = _instances[key];
            _instances.Remove(key);
            RemoveBaseIdKeyNoLock(instance.BaseId, key);
        }
    }

    private void RemoveBaseIdKeyNoLock(int baseId, (long Holder, int BuffUuid) key)
    {
        if (_keysByBaseId.TryGetValue(baseId, out var keys) && keys.Remove(key) && keys.Count == 0)
        {
            _keysByBaseId.Remove(baseId);
        }
    }
}
