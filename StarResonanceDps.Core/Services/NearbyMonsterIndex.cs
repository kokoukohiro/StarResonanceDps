namespace StarResonanceDps.Core.Services;

/// <summary>
/// 周囲にいるモンスターの索引。鍵は実体の UUID、値は種別ID(<c>AttrId</c> = <c>MonsterTable</c> のキー)。
/// ボス大技の予告は誰が構えたかを運ばないので、被ダメログがその技を持つモンスターを周囲から探すのに使う。
/// エンカウンターを作り直した後に実体が作られたとき、<c>AttrId</c> を戻すのにも使う。
///
/// <para>
/// <c>AttrId</c> は出現時にしか届かず、エンカウンターを作り直すと敵の属性は運ばれないことがあるので、
/// エンカウンターとは別に持つ。出現・属性の到着で入れ、消える通知・マップ切替・計測の停止で消す。
/// </para>
///
/// <para>
/// エンティティリストの索引(<c>NearbyEntityStore</c>)は <c>HudShowParam</c> が 0 のボスを外すので使えない。
/// </para>
/// </summary>
public sealed class NearbyMonsterIndex
{
    private static readonly Lazy<NearbyMonsterIndex> LazyInstance = new(() => new NearbyMonsterIndex());

    private readonly object _sync = new();
    private readonly Dictionary<long, int> _monsterIdByUuid = [];

    private NearbyMonsterIndex()
    {
    }

    public static NearbyMonsterIndex Instance => LazyInstance.Value;

    public void Set(long uuid, int monsterId)
    {
        lock (_sync)
        {
            _monsterIdByUuid[uuid] = monsterId;
        }
    }

    public void Remove(long uuid)
    {
        lock (_sync)
        {
            _monsterIdByUuid.Remove(uuid);
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _monsterIdByUuid.Clear();
        }
    }

    public bool TryGetMonsterId(long uuid, out int monsterId)
    {
        lock (_sync)
        {
            return _monsterIdByUuid.TryGetValue(uuid, out monsterId);
        }
    }

    /// <summary>周囲にいるモンスターのうち、<paramref name="match"/> に当たる種別IDを1つ返す。</summary>
    public bool TryFindMonsterId(Func<int, bool> match, out int monsterId)
    {
        lock (_sync)
        {
            foreach (var candidate in _monsterIdByUuid.Values)
            {
                if (match(candidate))
                {
                    monsterId = candidate;
                    return true;
                }
            }
        }

        monsterId = 0;
        return false;
    }
}
