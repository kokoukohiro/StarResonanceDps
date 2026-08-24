using System.Globalization;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.CombatRuntime.DataTypes.Skills;

namespace StarResonanceDps.Core.Services;

/// <summary>
/// 自分以外のパーティメンバーについて、ライブ経路が存在しない情報だけを保持する。
///
/// <para>
/// キャッシュしてよいのは「他に取得手段が無いもの」だけに限る。名前・レベル・戦闘力・職業・
/// シーズン値・装備・HPはパーティのsocial data(<see cref="PartyMemberSupplement"/>)で
/// AOI外でもライブに届くため、ここには持たない。冗長なコピーを持つと、それがライブ値を
/// 追い越して更新を握り潰す事故になる(旧 EntityCache がまさにそれだった)。
/// </para>
///
/// <para>保持するのは次の2種類だけ:</para>
/// <list type="bullet">
///   <item>
///     <b>職業特化(SubProfessionId)</b> — サーバから届く値ではなく、特化スキルの使用を
///     観測して推定している。マップを読み直すと不明に戻るため保持する。
///   </item>
///   <item>
///     <b>習得スキル一覧(AttrSkillLevelIdList)</b> — イマジン/ロールスキルの表示元。
///     AOI同期でしか届かないため、相手がマップ外に出ると取得できなくなる。
///   </item>
/// </list>
///
/// <para>
/// 対象は自分以外のパーティメンバーのみ。自分は常に完全なライブ値が取れるので一切キャッシュしない。
/// パーティから外れたメンバーの分は破棄する。
/// </para>
///
/// <para>
/// 各エントリは観測時刻を持つ。表示には使わないが、古い値を掴んでいないかを追えるようにしておく。
/// </para>
/// </summary>
public sealed class PartyMemberCache
{
    private static readonly Lazy<PartyMemberCache> LazyInstance = new(() => new PartyMemberCache());

    private readonly object _sync = new();
    private readonly Dictionary<long, CacheEntry> _entriesByCharacterId = [];

    private PartyMemberCache()
    {
        PartyStateStore.Instance.Changed += (_, _) => PruneToCurrentParty();
    }

    public static PartyMemberCache Instance => LazyInstance.Value;

    public void SetSubProfession(long characterId, int subProfessionId)
    {
        if (subProfessionId <= 0 || !IsCacheableMember(characterId))
        {
            return;
        }

        lock (_sync)
        {
            var entry = GetOrCreateNoLock(characterId);
            entry.SubProfessionId = subProfessionId;
            entry.SubProfessionObservedAt = DateTime.Now;
        }
    }

    public void ClearSubProfession(long characterId)
    {
        if (characterId <= 0)
        {
            return;
        }

        lock (_sync)
        {
            if (_entriesByCharacterId.TryGetValue(characterId, out var entry))
            {
                entry.SubProfessionId = 0;
                entry.SubProfessionObservedAt = default;
            }
        }
    }

    public bool TryGetSubProfession(long characterId, out int subProfessionId)
    {
        subProfessionId = 0;
        if (!IsCacheableMember(characterId))
        {
            return false;
        }

        lock (_sync)
        {
            if (!_entriesByCharacterId.TryGetValue(characterId, out var entry)
                || entry.SubProfessionId <= 0)
            {
                return false;
            }

            subProfessionId = entry.SubProfessionId;
            return true;
        }
    }

    public void SetSkillLevels(long characterId, IReadOnlyList<SkillLevelInfo> skillLevels)
    {
        if (skillLevels is null || skillLevels.Count == 0 || !IsCacheableMember(characterId))
        {
            return;
        }

        lock (_sync)
        {
            var entry = GetOrCreateNoLock(characterId);
            entry.SkillLevels = [.. skillLevels];
            entry.SkillLevelsObservedAt = DateTime.Now;
        }
    }

    public bool TryGetSkillLevels(long characterId, out IReadOnlyList<SkillLevelInfo> skillLevels)
    {
        skillLevels = Array.Empty<SkillLevelInfo>();
        if (!IsCacheableMember(characterId))
        {
            return false;
        }

        lock (_sync)
        {
            if (!_entriesByCharacterId.TryGetValue(characterId, out var entry)
                || entry.SkillLevels.Count == 0)
            {
                return false;
            }

            skillLevels = entry.SkillLevels;
            return true;
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _entriesByCharacterId.Clear();
        }
    }

    /// <summary>
    /// パーティから外れたメンバーの分を破棄する。
    /// 編成が確定しているときだけ判断する。マップ切替などで一時的に「不明」になっている間に
    /// 破棄すると、本当は在籍しているメンバーの補完値まで失われる。
    /// </summary>
    public void PruneToCurrentParty()
    {
        var party = PartyStateStore.Instance.Current;
        if (!party.HasCompleteMembership)
        {
            return;
        }

        var selfCharacterId = AppState.PlayerUID;

        lock (_sync)
        {
            var staleCharacterIds = _entriesByCharacterId.Keys
                .Where(characterId => characterId == selfCharacterId
                    || party.GetMembership(characterId) == PartyMembershipState.NonMember)
                .ToArray();
            foreach (var characterId in staleCharacterIds)
            {
                _entriesByCharacterId.Remove(characterId);
            }
        }
    }


    /// <summary>
    /// キャッシュ対象かどうか。自分は対象外、パーティメンバー以外も対象外。
    /// 読み書きの両方でこれを通すので、パーティ外の情報が入ることも出ることもない。
    /// </summary>
    private static bool IsCacheableMember(long characterId)
    {
        if (characterId <= 0 || characterId == AppState.PlayerUID)
        {
            return false;
        }

        // 「パーティ外と分かっている」場合だけ弾く。
        // マップ切替直後などで編成が未確定(Unknown)の間は、在籍している可能性があるので通す。
        // ここで弾くと、補完が必要なまさにその場面で読み書きの両方が止まる。
        return PartyStateStore.Instance.Current.GetMembership(characterId)
            != PartyMembershipState.NonMember;
    }

    private CacheEntry GetOrCreateNoLock(long characterId)
    {
        if (!_entriesByCharacterId.TryGetValue(characterId, out var entry))
        {
            entry = new CacheEntry();
            _entriesByCharacterId.Add(characterId, entry);
        }

        return entry;
    }

    private sealed class CacheEntry
    {
        public int SubProfessionId { get; set; }

        public DateTime SubProfessionObservedAt { get; set; }

        public IReadOnlyList<SkillLevelInfo> SkillLevels { get; set; } = Array.Empty<SkillLevelInfo>();

        public DateTime SkillLevelsObservedAt { get; set; }
    }
}
