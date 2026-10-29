using StarResonanceDps.Core.CombatRuntime;
using Zproto;

namespace StarResonanceDps.Core.Services;

/// <summary>
/// 見出し表で名前を持たないメーターの行について、付与元をたどって出どころ(着地先)を決める。
///
/// <para>
/// 着地は2種類。オプションのバフ(<c>RogueEntryTable.BuffId</c>)に着けばオプション、着かずにプレイヤーが使った技に着けば技。
/// 技は技の名前(<c>SkillTalkNames</c> → <c>SkillNames</c>)があるときだけで、空なら着かない。どちらにも着かなければ <see cref="SourceLanding.None"/>。
/// 名前の引き先は <see cref="CombatDataCatalog.GetSourceDisplayName"/>。
/// </para>
///
/// <para>
/// バフの段は、同じ番号の生きている実体の付与元を <see cref="BuffInstanceIndex"/> から引く(候補の選び方も同じ)。
/// 実体(弾・仮想体・召喚モンスター)の段は、出現した瞬間に付与元と召喚者から決めて UUID ごとに控える。
/// 弾は消えたあとにダメージが届くことがあるので、<b>控えは消える通知では消さない</b>。
/// 同じ UUID の出現で上書きし、マップ切替と計測の停止で消す。
/// </para>
///
/// <para>
/// 控えには着地先だけでなく付与元と召喚者もそのまま持つ。出現の瞬間には鎖の途中のバフがまだ付いていないことがあるので、
/// 着地できなかった控えは、ダメージが届いてその実体を引いたときにたどり直す。着けば控えを更新する。
/// </para>
///
/// <para>
/// バフの付与元が弾のときは、付与元に弾の番号しか載らない。術者がプレイヤーなら、
/// そのプレイヤーが出した同じ番号の弾(弾の実体の <c>AttrId</c>)の着地先を使う。
/// 同じプレイヤー・同じ番号で着地先が割れたら決めずに止まる。
/// </para>
///
/// <para>
/// バフ/デバフリストで表に名前の無いバフの由来も、同じたどり方で決める(<see cref="ResolveBuffOrigin"/>)。
/// こちらだけはモジュールの根のバフとイマジンのパッシブの根のバフでも止まる。
/// </para>
/// </summary>
public sealed class SourceLandingResolver
{
    private const int MaxBuffSteps = 8;

    private static readonly Lazy<SourceLandingResolver> LazyInstance = new(() => new SourceLandingResolver());

    private readonly object _sync = new();
    private readonly Dictionary<long, (EntityOrigin Origin, SourceLanding Landing, string Trace)> _entities = [];

    /// <summary>
    /// (持ち主のプレイヤー, 弾の番号) → 着地先。<c>IsSplit</c> は同じ組で別の着地先が出たこと。
    /// <c>LastUuid</c> は最後に出たその弾の実体で、まだ着地していなければそこからたどり直す。
    /// </summary>
    private readonly Dictionary<(long Player, int BulletId), (SourceLanding Landing, bool IsSplit, long LastUuid)> _bulletsByPlayer = [];

    private SourceLandingResolver()
    {
    }

    public static SourceLandingResolver Instance => LazyInstance.Value;

    /// <summary>
    /// プレイヤー以外の実体の出どころを控える。属性を当てた後に呼ぶ(同じ束の召喚者を使う)。
    /// 出現で付与元が無ければ、同じ UUID の前の控えを消す。差分で付与元が無いときは何もしない。
    /// </summary>
    public void RecordEntity(
        long uuid,
        int attrId,
        FightSourceInfo? source,
        long summonerUuid,
        long topSummonerUuid,
        DateTime arrivalTime,
        bool isAppear)
    {
        if (source == null)
        {
            if (isAppear)
            {
                lock (_sync)
                {
                    _entities.Remove(uuid);
                }
            }

            return;
        }

        var origin = new EntityOrigin(attrId, source.FightSourceType, source.SourceConfigId, summonerUuid, topSummonerUuid);
        var walk = new Walk();
        var landing = ResolveEntity(walk, uuid, origin, arrivalTime);
        Store(uuid, origin, landing, walk.ToString());
    }

    /// <summary>実体の控えを書き、弾ならプレイヤー×弾の番号の控えも更新する。着地先は先に着いたものを残し、別の先が出たら割れた印を付ける。</summary>
    private void Store(long uuid, EntityOrigin origin, SourceLanding landing, string trace)
    {
        var owner = IsPlayer(origin.TopSummonerUuid) ? origin.TopSummonerUuid : IsPlayer(origin.SummonerUuid) ? origin.SummonerUuid : 0;
        lock (_sync)
        {
            _entities[uuid] = (origin, landing, trace);

            if (owner == 0
                || origin.AttrId <= 0
                || Utils.UuidToEntityType(uuid) != (long)EEntityType.EntBullet)
            {
                return;
            }

            var key = (owner, origin.AttrId);
            _bulletsByPlayer.TryGetValue(key, out var known);
            var isSplit = known.IsSplit
                || (landing.Kind != SourceLandingKind.None && known.Landing.Kind != SourceLandingKind.None && known.Landing != landing);
            var kept = known.Landing.Kind != SourceLandingKind.None ? known.Landing : landing;
            _bulletsByPlayer[key] = (kept, isSplit, uuid);
        }
    }

    private SourceLanding ResolveEntity(Walk walk, long uuid, EntityOrigin origin, DateTime arrivalTime)
    {
        walk.Add($"実体 {uuid} 付与元 {SourceTypeName(origin.SourceType)} {origin.SourceId}");
        if (!walk.EnterEntity(uuid))
        {
            return walk.Stop("実体の循環");
        }

        return (EFightSource)origin.SourceType switch
        {
            EFightSource.Buff => ResolveBuff(
                walk, origin.SourceId, uuid, origin.TopSummonerUuid != 0 ? origin.TopSummonerUuid : origin.SummonerUuid, arrivalTime),
            EFightSource.Skill => IsPlayer(origin.SummonerUuid)
                ? LandSkill(walk, origin.SourceId)
                : origin.SummonerUuid != 0
                    ? FromEntity(walk, origin.SummonerUuid, arrivalTime)
                    : walk.Stop("召喚者なし"),
            EFightSource.Bullet => origin.SummonerUuid != 0
                ? FromEntity(walk, origin.SummonerUuid, arrivalTime)
                : walk.Stop("召喚者なし"),
            _ => walk.Stop("付与元の種類が対象外")
        };
    }

    public void Clear()
    {
        lock (_sync)
        {
            _entities.Clear();
            _bulletsByPlayer.Clear();
        }
    }

    /// <summary>
    /// ダメージ・回復1件の出どころを決める。<paramref name="attackerUuid"/> は記録先のプレイヤー、
    /// <paramref name="attackerRawUuid"/> はダメージを出した実体そのもの。<paramref name="trace"/> はたどった鎖。
    /// </summary>
    public SourceLanding Resolve(
        EDamageSource damageSource,
        int ownerId,
        long attackerRawUuid,
        long attackerUuid,
        DateTime arrivalTime,
        out string trace)
    {
        var walk = new Walk();
        SourceLanding landing;
        if (damageSource == EDamageSource.Buff)
        {
            landing = ResolveBuff(walk, ownerId, attackerRawUuid, attackerUuid, arrivalTime);
        }
        else if (attackerRawUuid != 0 && !IsPlayer(attackerRawUuid))
        {
            landing = FromEntity(walk, attackerRawUuid, arrivalTime);
        }
        else if (damageSource == EDamageSource.Skill && attackerRawUuid == attackerUuid)
        {
            walk.Add($"技 {ownerId}(本人)");
            landing = LandSkill(walk, ownerId);
        }
        else
        {
            landing = walk.Stop($"起点なし(発生源 {damageSource})");
        }

        trace = walk.ToString();
        return landing;
    }

    /// <summary>
    /// バフ1つの由来を決める。バフ/デバフリストで、表に名前の無いバフに出す名前の出どころ(<c>CombatDataCatalog.GetBuffDisplayName</c>)。
    ///
    /// <para>
    /// そのバフ自身がモジュールの根のバフならモジュール効果、イマジンのパッシブの根のバフならそのイマジンの技に着く。
    /// そうでなければ、そのバフの付与元と付与者からたどる。たどり方はダメージの出どころと同じ
    /// (親のバフ → その付与元、技 → 術者がプレイヤーならその技・召喚体なら実体の控え、弾 → プレイヤーの弾の控え)。
    /// ダメージと違い、たどる途中のバフがモジュールの根のバフならモジュール効果、
    /// イマジンのパッシブの根のバフならそのイマジンの技で止まる(メーターの行名はこの2つに着かない)。
    /// </para>
    /// </summary>
    public SourceLanding ResolveBuffOrigin(int buffId, int fightSourceType, int sourceConfigId, long fireUuid, DateTime arrivalTime)
    {
        var walk = new Walk();
        if (TryLandBuffOriginRoot(walk, buffId, out var rootLanding))
        {
            return rootLanding;
        }

        return fightSourceType == (int)EFightSource.Buff && sourceConfigId > 0
            ? ResolveBuff(walk, sourceConfigId, fireUuid, fireUuid, arrivalTime, stopAtBuffOriginRoots: true)
            : LandNonBuffSource(walk, fightSourceType, sourceConfigId, fireUuid, arrivalTime);
    }

    /// <summary>
    /// バフの由来の止まり先(モジュールの根のバフ → モジュール効果、イマジンのパッシブの根のバフ → そのイマジンの技)なら着く。
    /// 止まり先でなければ <paramref name="walk"/> に何も足さずに false。
    /// </summary>
    private static bool TryLandBuffOriginRoot(Walk walk, int buffId, out SourceLanding landing)
    {
        if (CombatDataCatalog.TryGetModEffectIdByRootBuff(buffId, out _))
        {
            walk.Add($"モジュールの根 {buffId}");
            landing = walk.Land(new SourceLanding(SourceLandingKind.Module, buffId));
            return true;
        }

        if (CombatDataCatalog.TryGetImagineSkillIdByPassiveRoot(buffId, out var imagineSkillId))
        {
            walk.Add($"イマジンのパッシブの根 {buffId} → 技 {imagineSkillId}");
            landing = LandSkill(walk, imagineSkillId);
            return true;
        }

        landing = SourceLanding.None;
        return false;
    }

    /// <param name="stopAtBuffOriginRoots">
    /// モジュールの根のバフとイマジンのパッシブの根のバフで止めるか。バフの由来(<see cref="ResolveBuffOrigin"/>)だけが立てる。
    /// </param>
    private SourceLanding ResolveBuff(Walk walk, int buffId, long rawUuid, long attackerUuid, DateTime arrivalTime, bool stopAtBuffOriginRoots = false)
    {
        var visited = new HashSet<int>();
        for (var step = 0; step < MaxBuffSteps; step++)
        {
            walk.Add($"バフ {buffId}");
            if (CombatDataCatalog.IsRogueEntryBuff(buffId))
            {
                return walk.Land(new SourceLanding(SourceLandingKind.RogueEntry, buffId));
            }

            if (stopAtBuffOriginRoots && TryLandBuffOriginRoot(walk, buffId, out var rootLanding))
            {
                return rootLanding;
            }

            if (!visited.Add(buffId))
            {
                return walk.Stop("循環");
            }

            if (!BuffInstanceIndex.Instance.TryResolveSource(buffId, rawUuid, attackerUuid, arrivalTime, out var sourceType, out var sourceId, out var fireUuid))
            {
                return walk.Stop("生きている実体が無いか、付与元がそろわない");
            }

            if (sourceType == (int)EFightSource.Buff)
            {
                buffId = sourceId;
                continue;
            }

            return LandNonBuffSource(walk, sourceType, sourceId, fireUuid, arrivalTime);
        }

        return walk.Stop("段数の上限");
    }

    /// <summary>付与元が親のバフ以外の段。技 → 術者がプレイヤーならその技・召喚体なら実体の控え、弾 → プレイヤーの弾の控え、それ以外は止まる。</summary>
    private SourceLanding LandNonBuffSource(Walk walk, int sourceType, int sourceId, long fireUuid, DateTime arrivalTime)
    {
        if (sourceType == (int)EFightSource.Skill && sourceId > 0)
        {
            walk.Add($"技 {sourceId}(術者 {fireUuid})");
            return IsPlayer(fireUuid)
                ? LandSkill(walk, sourceId)
                : fireUuid != 0
                    ? FromEntity(walk, fireUuid, arrivalTime)
                    : walk.Stop("術者なし");
        }

        if (sourceType == (int)EFightSource.Bullet && sourceId > 0)
        {
            walk.Add($"弾 {sourceId}(術者 {fireUuid})");
            return IsPlayer(fireUuid)
                ? FromBulletOfPlayer(walk, fireUuid, sourceId, arrivalTime)
                : fireUuid != 0
                    ? FromEntity(walk, fireUuid, arrivalTime)
                    : walk.Stop("術者なし");
        }

        return walk.Stop($"付与元 {SourceTypeName(sourceType)} {sourceId}");
    }

    private SourceLanding FromBulletOfPlayer(Walk walk, long playerUuid, int bulletId, DateTime arrivalTime)
    {
        (SourceLanding Landing, bool IsSplit, long LastUuid) known;
        bool found;
        lock (_sync)
        {
            found = _bulletsByPlayer.TryGetValue((playerUuid, bulletId), out known);
        }

        if (!found)
        {
            return walk.Stop($"プレイヤーの弾 {bulletId} の控えなし");
        }

        if (known.IsSplit)
        {
            return walk.Stop($"プレイヤーの弾 {bulletId} の着地先が割れている");
        }

        if (known.Landing.Kind != SourceLandingKind.None)
        {
            walk.Add($"[プレイヤーの弾 {bulletId} → {known.Landing}]");
            return known.Landing;
        }

        // まだ着地していない。最後に出たその弾の実体をたどり直す。
        walk.Add($"プレイヤーの弾 {bulletId} は未着地 → 実体 {known.LastUuid}");
        return FromEntity(walk, known.LastUuid, arrivalTime);
    }

    private SourceLanding FromEntity(Walk walk, long uuid, DateTime arrivalTime)
    {
        (EntityOrigin Origin, SourceLanding Landing, string Trace) entry;
        bool found;
        lock (_sync)
        {
            found = _entities.TryGetValue(uuid, out entry);
        }

        if (!found)
        {
            return walk.Stop($"実体 {uuid} の控えなし");
        }

        if (entry.Landing.Kind != SourceLandingKind.None)
        {
            walk.Add($"[{entry.Trace}]");
            return entry.Landing;
        }

        // 出現の瞬間には着地できなかった控え。いまの状態でたどり直し、着けば控えを更新する。
        var retry = walk.Child();
        var landing = ResolveEntity(retry, uuid, entry.Origin, arrivalTime);
        walk.Add($"[たどり直し {retry}]");
        if (landing.Kind != SourceLandingKind.None)
        {
            Store(uuid, entry.Origin, landing, retry.ToString());
        }

        return landing;
    }

    private static SourceLanding LandSkill(Walk walk, int skillId)
    {
        return string.IsNullOrEmpty(CombatDataCatalog.GetSkillNameWithoutInternalId(skillId))
            ? walk.Stop($"技 {skillId} の名前が空")
            : walk.Land(new SourceLanding(SourceLandingKind.Skill, skillId));
    }

    private static bool IsPlayer(long uuid)
        => uuid != 0 && Utils.UuidToEntityType(uuid) == (long)EEntityType.EntChar;

    private static string SourceTypeName(int fightSourceType)
        => Enum.IsDefined(typeof(EFightSource), fightSourceType)
            ? ((EFightSource)fightSourceType).ToString()
            : fightSourceType.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>実体の控えの元になった値。着地できなかったときのたどり直しに使う。</summary>
    private readonly record struct EntityOrigin(int AttrId, int SourceType, int SourceId, long SummonerUuid, long TopSummonerUuid);

    /// <summary>たどった鎖を1行にまとめる。ログにそのまま書く。通った実体は子の鎖と共有して、循環で止める。</summary>
    private sealed class Walk
    {
        private readonly List<string> _steps = [];
        private readonly HashSet<long> _entities;

        public Walk()
            : this([])
        {
        }

        private Walk(HashSet<long> entities)
        {
            _entities = entities;
        }

        public Walk Child() => new(_entities);

        public bool EnterEntity(long uuid) => _entities.Add(uuid);

        public void Add(string step) => _steps.Add(step);

        public SourceLanding Land(SourceLanding landing)
        {
            _steps.Add($"→ 着地 {landing}");
            return landing;
        }

        public SourceLanding Stop(string reason)
        {
            _steps.Add($"→ 止まる: {reason}");
            return SourceLanding.None;
        }

        public override string ToString() => string.Join(" ", _steps);
    }
}
