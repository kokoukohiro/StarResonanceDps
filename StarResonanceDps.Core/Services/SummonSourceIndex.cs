namespace StarResonanceDps.Core.Services;

/// <summary>
/// プレイヤー以外の実体(ボスが出す仮想体など)を出した技の索引。鍵は実体の UUID。
/// 被ダメログが、仮想体の技に名前が無いときに出した元の技名を引くのに使う。
///
/// <para>
/// 出した元は実体の属性 <c>AttrFightSourceInfo</c> が運ぶ。技ならその技、バフならその時点で
/// <see cref="BuffInstanceIndex"/> からバフの付与元の技を引く。<b>バフは被弾する頃には除去されている</b>ので、
/// 属性が届いた瞬間に決めて控える。
/// </para>
///
/// <para>
/// 仮想体の UUID は使い回されるので、出した元が届くたびに上書きし、消える通知・マップ切替・計測の停止で消す。
/// エンカウンターの作り直しでは消さない(区切りをまたいで被弾が届く)。
/// </para>
/// </summary>
public sealed class SummonSourceIndex
{
    private static readonly Lazy<SummonSourceIndex> LazyInstance = new(() => new SummonSourceIndex());

    private readonly object _sync = new();
    private readonly Dictionary<long, int> _sourceSkillByUuid = [];

    private SummonSourceIndex()
    {
    }

    public static SummonSourceIndex Instance => LazyInstance.Value;

    public void Set(long uuid, int skillId)
    {
        lock (_sync)
        {
            _sourceSkillByUuid[uuid] = skillId;
        }
    }

    public void Remove(long uuid)
    {
        lock (_sync)
        {
            _sourceSkillByUuid.Remove(uuid);
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _sourceSkillByUuid.Clear();
        }
    }

    public bool TryGet(long uuid, out int skillId)
    {
        lock (_sync)
        {
            return _sourceSkillByUuid.TryGetValue(uuid, out skillId);
        }
    }
}
