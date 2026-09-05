using StarResonanceDps.Core.Services;
using Zproto;

namespace StarResonanceDps.Core.CombatRuntime;

/// <summary>
/// ダメージ・回復イベントの発生源を、表示に使うIDへ畳む。
///
/// <para>
/// イベントの <c>OwnerId</c> は <c>DamageSource</c> によって中身が変わる。
/// スキルID・弾ID・バフIDのどれかで、そのままではメーターの行にならない。
/// </para>
///
/// <list type="number">
///   <item>対応表 — <c>Data/Mappings/</c> の手書きの表に載っていれば、そこへ畳む</item>
///   <item>4言語テーブル — キーがあるIDは「素のまま出すと決めたID」。鎖へ入れない</item>
///   <item>弾 — <c>SkillFightLevelTable</c> で親スキルへ</item>
///   <item>スキル — そのまま(コンボの段なら1段目へ)</item>
///   <item>バフ — 生きている実体の <c>FightSourceInfo</c> から鎖を辿る</item>
///   <item>召喚体 — 上で表に載るIDへ着けなかったときだけ、その <c>AttrId</c> をスキルとして引く</item>
///   <item><b>最後に</b>ダメージ属性 — <c>SkillAttrDes</c> の式にその属性を書いているスキルへ</item>
/// </list>
///
/// <para>
/// <b>決め切れないときは畳まない。</b> どちらの親か絞れない回に片方を選ぶと、
/// 表示は正常に見えるのに中身が当て推量になり、誤りが見えなくなる。
/// 畳まずに生のバフIDで出し、<see cref="Diagnostics.UnknownSplitProbe"/> に残す。
/// </para>
/// </summary>
public static class SkillSourceResolver
{
    /// <summary>鎖を辿る段数の上限。循環しているデータが実在する(<c>3003211</c> ⇔ <c>3003212</c>)。</summary>
    private const int MaxChainDepth = 8;

    /// <summary>割れたときに「付与が近い実体」とみなす幅。</summary>
    private static readonly TimeSpan RecentAddWindow = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// コンボの段 → 1段目。<c>NextSkillId</c> の鎖を遡って作る。
    ///
    /// <para>
    /// 親が複数ある子(実データで3件)は <c>SkillLevelGroup</c> がどれか1つを名指しするので、
    /// それで解く。循環は0件。
    /// </para>
    /// </summary>
    private static Dictionary<int, int>? _comboStageToHead;

    private static readonly object IndexGate = new();

    /// <summary>畳んだ結果。</summary>
    /// <param name="Id">表示に使うID。</param>
    /// <param name="IsBuffSource">true ならバフID(名前は <c>GetBuffName</c> で引く)。</param>
    public readonly record struct Result(int Id, bool IsBuffSource);

    /// <summary>
    /// 発生源を畳む。<paramref name="casterUuid"/> は召喚元へ寄せる前の実際の攻撃者ではなく、
    /// バフを掛けた人として突き合わせる相手(<c>TopSummonerId</c> 適用後)を渡す。
    /// </summary>
    public static Result Resolve(
        EDamageSource damageSource,
        int ownerId,
        long casterUuid,
        int summonAttrId,
        DateTime now)
    {
        // 1段目 — 手書きの対応表。実行時の鎖より先に引く。
        // 鎖はバフ実体が届いているかに左右されるが、表は届いていなくても答えを持っている。
        if (ownerId != 0)
        {
            var mapped = damageSource == EDamageSource.Buff
                ? CombatDataCatalog.TryResolveBuffSource(ownerId, out var buffParent)
                    ? buffParent
                    : 0
                : CombatDataCatalog.TryResolveSkillSource(ownerId, out var skillParent)
                    ? skillParent
                    : 0;
            if (mapped != 0)
            {
                return new Result(ToComboHead(mapped), IsBuffSource: false);
            }
        }

        // 2段目 — 4言語テーブルにキーがあるものは「素のまま出すと決めたID」。鎖へ入れない。
        // 訳が空でも止める。載せるかどうかはテーブルを作るときの判断で、実行時には理由を問わない。
        if (ownerId != 0)
        {
            if (damageSource == EDamageSource.Buff)
            {
                if (CombatDataCatalog.HasBuffKey(ownerId))
                {
                    return new Result(ownerId, IsBuffSource: true);
                }
            }
            else if (CombatDataCatalog.HasSkillKey(ownerId))
            {
                return new Result(ToComboHead(ownerId), IsBuffSource: false);
            }
        }

        // 3段目 — 種別ごとの解決。どこまで登るかは「4言語テーブルにIDがあるか」で決まる。
        var resolved = ResolveByDamageSource(damageSource, ownerId, casterUuid, now);
        if (resolved is { } candidate && HasTableKey(candidate))
        {
            return candidate;
        }

        // 4段目 — 召喚体の種別IDをスキルとして引く。
        //
        // この経路が答えるのは<b>その召喚体が何のスキルか</b>であって、それを出したスキルではない。
        // 先に置くと ownerId の弾解決・バフの鎖・コンボ先頭が丸ごと飛ばされる
        // (実測: 発火37,693件のうち10,615件で答えが変わり、その大半は名前の無い召喚体IDへ
        // 吸われていた)。表に載るIDへ着いたときだけ採る。
        if (summonAttrId != 0
            && TryResolveSkill(summonAttrId, out var summonSkillId))
        {
            var summonResult = new Result(ToComboHead(summonSkillId), IsBuffSource: false);
            if (HasTableKey(summonResult))
            {
                return summonResult;
            }
        }

        // 5段目 — 最後の手段。ダメージ属性IDから、その式を持つスキルへ。
        // ここまでのどれでも表へ届かなかったときだけ引く。
        if (ownerId != 0
            && CombatDataCatalog.TryResolveAttrSource(ownerId, out var attrParentSkillId))
        {
            return new Result(ToComboHead(attrParentSkillId), IsBuffSource: false);
        }

        // どれも表に届かなかった。3段目の結果があればそれを出す(畳まないのと同じ)。
        return resolved ?? new Result(ToComboHead(ownerId), IsBuffSource: false);
    }

    /// <summary>畳んだ結果が4言語テーブルに載っているか。載っていれば確定として扱う。</summary>
    private static bool HasTableKey(Result result)
        => result.IsBuffSource
            ? CombatDataCatalog.HasBuffKey(result.Id)
            : CombatDataCatalog.HasSkillKey(result.Id);

    /// <summary>
    /// 種別ごとの解決。<b>解決できなければ null。</b>
    /// 呼び出し元が「次の手へ進むか」を判断できるように、成功と失敗を区別して返す。
    /// </summary>
    private static Result? ResolveByDamageSource(
        EDamageSource damageSource,
        int ownerId,
        long casterUuid,
        DateTime now)
    {
        switch (damageSource)
        {
            case EDamageSource.Bullet:
            case EDamageSource.FakeBullet:
                return TryResolveSkill(ownerId, out var bulletSkillId)
                    ? new Result(ToComboHead(bulletSkillId), IsBuffSource: false)
                    : null;

            case EDamageSource.Buff:
                return ResolveBuff(ownerId, casterUuid, now);

            default:
                return new Result(ToComboHead(ownerId), IsBuffSource: false);
        }
    }

    private static Dictionary<int, int> ComboStageToHead
    {
        get
        {
            lock (IndexGate)
            {
                if (_comboStageToHead is not null)
                {
                    return _comboStageToHead;
                }

                var skills = HelperMethods.DataTables.Skills.Data;
                var parents = new Dictionary<int, List<int>>();
                var levelGroup = new Dictionary<int, int>();

                foreach (var pair in skills)
                {
                    if (!int.TryParse(pair.Key, out var id))
                    {
                        continue;
                    }

                    if (pair.Value.SkillLevelGroup != 0)
                    {
                        levelGroup[id] = pair.Value.SkillLevelGroup;
                    }

                    var next = pair.Value.NextSkillId;
                    if (next == 0)
                    {
                        continue;
                    }

                    if (!parents.TryGetValue(next, out var list))
                    {
                        list = [];
                        parents[next] = list;
                    }

                    list.Add(id);
                }

                var map = new Dictionary<int, int>();
                foreach (var child in parents.Keys)
                {
                    var current = child;
                    var guard = new HashSet<int> { current };
                    while (parents.TryGetValue(current, out var candidates))
                    {
                        int chosen;
                        if (candidates.Count == 1)
                        {
                            chosen = candidates[0];
                        }
                        else if (levelGroup.TryGetValue(current, out var group)
                            && candidates.Contains(group))
                        {
                            // 親が複数のときは SkillLevelGroup がどれか1つを名指しする。
                            chosen = group;
                        }
                        else
                        {
                            break;
                        }

                        if (!guard.Add(chosen))
                        {
                            break;
                        }

                        current = chosen;
                    }

                    if (current != child)
                    {
                        map[child] = current;
                    }
                }

                _comboStageToHead = map;
                return map;
            }
        }
    }

    /// <summary>コンボの段なら1段目へ寄せる。</summary>
    private static int ToComboHead(int skillId)
    {
        // 手書きの対応表に載っていれば、まずそちらへ寄せる。
        if (CombatDataCatalog.TryResolveSkillSource(skillId, out var mappedSkillId))
        {
            skillId = mappedSkillId;
        }

        return ComboStageToHead.TryGetValue(skillId, out var head) ? head : skillId;
    }

    /// <summary>弾ID・召喚体の種別IDからスキルIDへ。</summary>
    private static bool TryResolveSkill(int id, out int skillId)
    {
        var key = id.ToString();
        if (HelperMethods.DataTables.Skills.Data.ContainsKey(key))
        {
            skillId = id;
            return true;
        }

        // SkillFightLevelTable.Id = SkillId × 100 + Level
        if (HelperMethods.DataTables.SkillFightLevels.Data.TryGetValue(key, out var fightLevel)
            && fightLevel.SkillId != 0
            && HelperMethods.DataTables.Skills.Data.ContainsKey(fightLevel.SkillId.ToString()))
        {
            skillId = fightLevel.SkillId;
            return true;
        }

        skillId = 0;
        return false;
    }

    private static Result ResolveBuff(int buffId, long casterUuid, DateTime now)
    {
        var unfolded = new Result(buffId, IsBuffSource: true);
        if (buffId == 0 || casterUuid == 0)
        {
            return unfolded;
        }

        var live = BuffSourceIndex.Instance.GetLive(casterUuid, buffId, now);
        if (live.Count == 0)
        {
            return unfolded;
        }

        Result? single = null;
        var isSplit = false;
        for (var index = 0; index < live.Count; index++)
        {
            if (!TryFoldChain(buffId, live[index].FightSourceType, live[index].SourceConfigId, out var folded))
            {
                continue;
            }

            if (single is null)
            {
                single = folded;
            }
            else if (!single.Value.Equals(folded))
            {
                isSplit = true;
            }
        }

        if (!isSplit)
        {
            return single ?? unfolded;
        }

        // 割れた。実体は付与の直後に発火するので(実測: 単独実体642個の100%が0.5秒以内、
        // 90.5%が0.05秒以内)、付与が直近のものだけに絞る。
        Result? recent = null;
        var stillSplit = false;
        for (var index = 0; index < live.Count; index++)
        {
            if (now - live[index].AddTime > RecentAddWindow)
            {
                continue;
            }

            if (!TryFoldChain(buffId, live[index].FightSourceType, live[index].SourceConfigId, out var folded))
            {
                continue;
            }

            if (recent is null)
            {
                recent = folded;
            }
            else if (!recent.Value.Equals(folded))
            {
                stillSplit = true;
            }
        }

        if (recent is not null && !stillSplit)
        {
            return recent.Value;
        }

        Diagnostics.UnknownSplitProbe.CaptureSplit(buffId, casterUuid, live);
        return unfolded;
    }

    /// <summary>
    /// 実体の発生源から鎖を辿る。4言語テーブルにあるバフに当たったらそこで止め、
    /// スキルに当たったらスキルにする。それ以外は畳まない。
    /// </summary>
    private static bool TryFoldChain(int buffId, int fightSourceType, int sourceConfigId, out Result result)
    {
        // 対応表に載っているバフなら、そこへ寄せる。
        // 4言語テーブルの判定より先に見る。表が名指ししている親の方が確かなため。
        if (CombatDataCatalog.TryResolveBuffSource(buffId, out var mappedSkillId))
        {
            result = new Result(ToComboHead(mappedSkillId), IsBuffSource: false);
            return true;
        }

        // 4言語テーブルに載っているバフは、それ自体が表示に値する。親まで登らない。
        if (CombatDataCatalog.HasBuffKey(buffId))
        {
            result = new Result(buffId, IsBuffSource: true);
            return true;
        }

        var currentType = fightSourceType;
        var currentId = sourceConfigId;
        Span<int> visited = stackalloc int[MaxChainDepth + 1];
        visited[0] = buffId;
        var visitedCount = 1;

        for (var depth = 0; depth < MaxChainDepth; depth++)
        {
            // ftype=Skill ならそこがスキル。ftype=Buff なら親バフへもう一段登る。
            // それ以外(タレント・シーズンタレント等)は終端で、畳み先が無い。
            if (currentType == (int)EFightSource.Skill)
            {
                if (currentId == 0)
                {
                    break;
                }

                result = new Result(ToComboHead(currentId), IsBuffSource: false);
                return true;
            }

            if (currentType != (int)EFightSource.Buff || currentId == 0)
            {
                break;
            }

            var seen = false;
            for (var index = 0; index < visitedCount; index++)
            {
                if (visited[index] == currentId)
                {
                    seen = true;
                    break;
                }
            }

            if (seen)
            {
                break;
            }

            visited[visitedCount++] = currentId;

            if (CombatDataCatalog.TryResolveBuffSource(currentId, out var chainMappedSkillId))
            {
                result = new Result(ToComboHead(chainMappedSkillId), IsBuffSource: false);
                return true;
            }

            if (CombatDataCatalog.HasBuffKey(currentId))
            {
                result = new Result(currentId, IsBuffSource: true);
                return true;
            }

            if (!BuffSourceIndex.Instance.TryGetLastSource(currentId, out currentType, out currentId))
            {
                break;
            }
        }

        result = default;
        return false;
    }
}
