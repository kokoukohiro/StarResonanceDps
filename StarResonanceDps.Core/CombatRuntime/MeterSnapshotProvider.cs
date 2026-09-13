using Newtonsoft.Json.Linq;
using Serilog;
using StarResonanceDps.Core.CombatRuntime.DataTypes;
using StarResonanceDps.Core.Models;
using StarResonanceDps.Core.Services;
using ZLinq;
using Zproto;

namespace StarResonanceDps.Core.CombatRuntime;

public enum MeterSnapshotKind
{
    Damage,
    Healing
}

public enum PlayerBuffListKind
{
    Buff,
    Debuff
}

public sealed record MeterPlayerSnapshot(
    long CharacterId,
    long UserId,
    string Name,
    int ProfessionId,
    int SubProfessionId,
    PlayerClassSpec ClassSpec,
    int AbilityScore,
    int SeasonStrength,
    int Level,
    int SeasonLevel,
    bool IsSelf,
    bool IsNpc,
    ulong TotalValue,
    double ValuePerSecond,
    double Contribution,
    double BarRatio);

public sealed record MeterSnapshot(
    MeterSnapshotKind Kind,
    TimeSpan Duration,
    ulong TotalValue,
    double ValuePerSecond,
    IReadOnlyList<MeterPlayerSnapshot> Players);

public sealed record BenchmarkStateSnapshot(
    bool IsActive,
    bool HasBegun,
    bool IsCompleted);

public sealed record MetricTimelinePoint(double Seconds, double ValuePerSecond);

public sealed record MetricTimelineSnapshot(
    ulong TotalValue,
    IReadOnlyList<MetricTimelinePoint> Points);

public sealed record MetricSkillTableRowSnapshot(
    long SkillId,
    string SkillIdText,
    string Name,
    ulong TotalValue,
    double ValuePerSecondActive,
    double ValuePerSecond,
    ulong HitCount,
    double CritRate,
    double AverageValue,
    double Percentage);

public sealed record MetricSkillTableSnapshot(
    ulong TotalValue,
    IReadOnlyList<MetricSkillTableRowSnapshot> Entries);

public sealed record PlayerMetricSummarySnapshot(
    ulong TotalValue,
    double ValuePerSecondActive,
    double ValuePerSecond,
    ulong ExtraTotalValue,
    ulong HitsCount,
    double CritRate,
    double LuckyRate,
    uint CritCount,
    ulong ImmuneCount,
    bool ShowsImmuneCount,
    ulong NormalValue,
    ulong CritValue,
    ulong LuckyValue,
    uint LuckyCount,
    double AverageValue,
    ulong CastsCount,
    double? CastsPerMinute,
    double? CastsPerSecond);


public sealed record PlayerBuffSnapshot(
    long Uuid,
    int BaseId,
    string Key,
    string Name,
    string IconName,
    int Layer,
    double? RemainingSeconds,
    /// <summary>
    /// 残り時間を名乗れない状態。<b><see cref="RemainingSeconds"/> の null(持続時間なし)とは別物。</b>
    /// 表示側は空欄ではなく「?」を出す。
    /// </summary>
    bool IsRemainingUnknown = false);

public sealed record PlayerCooldownSkillSnapshot(
    int SkillId,
    string Name,
    string IconName,
    int CurrentLevel,
    int Tier,
    bool IsImagine,
    bool ShowLevel,
    double CooldownSeconds,
    int MaxCharges,
    double ChargeCooldownSeconds);

public sealed record PlayerSkillEffectSnapshot(
    PlayerBuffSnapshot? Buff,
    PlayerBuffSnapshot? Debuff);

/// <param name="RoleSlotCount">
/// ロールスキルの枠数。<b>自分と他人で意味が違う。</b>
///
/// <para>
/// 自分は装備スロットが分かるので 4 枠固定。装備していない枠は空欄として出す。
/// 他人は AOI の <c>AttrSkillLevelIdList</c>(習得済みの集合)しか届かず、
/// そこから装備状態を判別できない(CLAUDE.md「他プレイヤーの装備中ロールスキルも取得不可能」)。
/// 空欄を出す根拠が無いので、習得できていた数だけ枠を作る。
/// </para>
/// </param>
public sealed record PlayerImagineRoleSkillLoadoutSnapshot(
    long EntityUuid,
    IReadOnlyList<PlayerCooldownSkillSnapshot?> ImagineSkills,
    IReadOnlyList<PlayerCooldownSkillSnapshot?> RoleSkills,
    int RoleSlotCount);

internal sealed record PlayerBuffCandidate(
    PlayerBuffSnapshot Snapshot,
    TimeSpan EffectiveRemoveTime);

public sealed record MeterPlayerIdentity(string Name, long UserId);

public static class MeterSnapshotProvider
{
    /// <summary>
    /// 蘇生不可デバフ(<c>虚弱·祈愿禁止</c> / <c>Weakened: Wish Sealed</c>)。
    ///
    /// <para>
    /// 説明文が「この間は 奥義！ライフブレス と 復活の祈り を再度かけられない」。
    /// ワイヤの <c>Duration</c> は 60000ms(実測)。テーブルの <c>DestroyParam</c> は
    /// <c>[[0,0]]</c> なので、持続時間はパケット側からしか分からない。
    /// </para>
    ///
    /// <para>
    /// 2026-08-29 の実測で、復活系スキル 3つ(2900240 / 2900241 / 3312)すべてから
    /// <b>同じ BaseId</b> で届くことを確認した(22件、他の発生元からは0件)。
    /// 3027(Blessing of Life)は発火機会が無く未確認。
    /// </para>
    ///
    /// <para>
    /// 同時に届く <c>2110032</c> / <c>2110093</c> / <c>2100412</c> は
    /// いずれも自前アイコンが無く表示経路で落ちるので、扱う必要がない。
    /// </para>
    /// </summary>
    private const int ReviveBlockDebuffBaseId = 2110057;

    public static MeterSnapshot GetSnapshot(
        MeterSnapshotKind kind,
        PartyDisplayMode partyDisplayMode = PartyDisplayMode.All)
    {
        var encounter = ResolveActiveEncounter();
        if (encounter is null)
        {
            return new MeterSnapshot(kind, TimeSpan.Zero, 0, 0, Array.Empty<MeterPlayerSnapshot>());
        }

        var source = encounter.Entities
            .Where(pair => pair.Value.EntityType == EEntityType.EntChar)
            .Select(pair => CreatePlayerValue(pair.Key, pair.Value, kind))
            .Where(player => player.TotalValue > 0UL)
            .OrderByDescending(player => player.TotalValue)
            .ThenBy(player => player.Name, StringComparer.Ordinal)
            .ThenBy(player => player.CharacterId)
            .ToArray();

        UpdatePlayerMeterState(source);

        var party = PartyStateStore.Instance.Current;
        var visibleSource = source
            .Where(player => party.ShouldInclude(player.UserId, player.IsSelf, partyDisplayMode))
            .ToArray();
        var totalValue = partyDisplayMode == PartyDisplayMode.All
            ? kind == MeterSnapshotKind.Damage
                ? encounter.TotalDamage
                : encounter.TotalHealing
            : visibleSource.Aggregate(0UL, (total, player) => total + player.TotalValue);
        var topValue = visibleSource.Length == 0
            ? 0UL
            : visibleSource[0].TotalValue;
        var players = visibleSource
            .Select(player => player with
            {
                Contribution = totalValue == 0
                    ? 0d
                    : player.TotalValue / (double)totalValue * 100d,
                BarRatio = topValue == 0
                    ? 0d
                    : player.TotalValue / (double)topValue
            })
            .ToArray();

        return new MeterSnapshot(
            kind,
            ResolveMetricDuration(encounter),
            totalValue,
            players.Sum(player => player.ValuePerSecond),
            players);
    }

    public static MeterPlayerIdentity? GetPlayerIdentity(long characterId)
    {
        var encounter = ResolveActiveEncounter();
        if (encounter is null
            || !TryResolvePlayerEntity(encounter, characterId, out var entityUuid, out var entity))
        {
            return null;
        }

        var source = PlayerDataSourceResolver.Resolve(entity, IsSelf(entity));
        return new MeterPlayerIdentity(source.Name, source.CharacterId);
    }


    /// <summary>
    /// 自分の素性。
    ///
    /// <para>
    /// <b>ロスターに載るのを待たない。</b>自分のIDは <c>AppState.PlayerUID</c> が常に持っているので、
    /// プレイヤー一覧の投影が空でも(街で待機中など)名前を引ける。
    /// 自分を対象にした窓のタイトルがロスター次第で出たり出なかったりするのを避ける。
    /// </para>
    /// </summary>
    public static MeterPlayerIdentity? GetSelfPlayerIdentity()
    {
        var characterId = AppState.PlayerUID;
        return characterId == 0 ? null : GetPlayerIdentity(characterId);
    }

    /// <summary>
    /// バフ/デバフリストとカードが読む。<b>履歴を開いても固めない</b> —
    /// バフの変化はDBから復元できないので、止めても意味のある絵にならない。
    /// </summary>
    public static IReadOnlyList<PlayerBuffSnapshot> GetPlayerBuffs(long characterId, PlayerBuffListKind kind)
    {
        var encounter = ResolvePlayerDetailEncounter();
        if (encounter is null
            || !TryResolvePlayerEntity(encounter, characterId, out _, out var entity))
        {
            return Array.Empty<PlayerBuffSnapshot>();
        }

        return CreateBuffSnapshots(entity, kind);
    }

    public static IReadOnlyList<PlayerBuffSnapshot> GetEntityBuffs(long entityUuid, PlayerBuffListKind kind)
    {
        var encounter = EncounterManager.Current;
        if (encounter is null
            || !encounter.Entities.TryGetValue(entityUuid, out var entity))
        {
            return Array.Empty<PlayerBuffSnapshot>();
        }

        return CreateBuffSnapshots(entity, kind);
    }

    private static IReadOnlyList<PlayerBuffSnapshot> CreateBuffSnapshots(
        Entity entity,
        PlayerBuffListKind kind)
    {
        var buffEvents = ResolveDisplayBuffEvents(entity);
        var entriesByKey = new Dictionary<string, PlayerBuffCandidate>(StringComparer.Ordinal);
        var entryKeys = new List<string>(buffEvents.Length);

        // 初出順(古い順)で回す。ソートせず、新しいバフが末尾に付く並びにする。
        // ストアが初出順で返すので、ここで逆順にすると並びが反転する。
        for (var index = 0; index < buffEvents.Length; index++)
        {
            var buffEvent = buffEvents[index];
            if (buffEvent.Duration < 0
                || !IsIncludedBuff(kind, buffEvent)
                || IsHiddenFromBuffBar(buffEvent))
            {
                continue;
            }

            var iconName = ResolveBuffOwnIconName(buffEvent);
            if (string.IsNullOrWhiteSpace(iconName))
            {
                continue;
            }

            var name = ResolveBuffName(buffEvent);
            if (string.IsNullOrWhiteSpace(name) && buffEvent.BaseId <= 0)
            {
                continue;
            }

            TimeSpan effectiveRemoveTime;
            double? remainingSeconds;
            bool remainingUnknown;
            var timingResolved = TryResolveLiveBuffTiming(
                entity.UUID, buffEvent, out effectiveRemoveTime, out remainingSeconds, out remainingUnknown);
            if (!timingResolved)
            {
                continue;
            }

            var key = ResolveBuffSnapshotKey(buffEvent);
            var snapshot = new PlayerBuffSnapshot(
                buffEvent.Uuid,
                buffEvent.BaseId,
                key,
                name,
                iconName,
                buffEvent.Layer,
                remainingSeconds,
                remainingUnknown);
            var candidate = new PlayerBuffCandidate(snapshot, effectiveRemoveTime);

            if (!entriesByKey.TryGetValue(key, out var existing))
            {
                entriesByKey.Add(key, candidate);
                entryKeys.Add(key);
                continue;
            }

            if (candidate.EffectiveRemoveTime > existing.EffectiveRemoveTime)
            {
                entriesByKey[key] = candidate;
            }
        }

        return entryKeys
            .Select(key => entriesByKey[key].Snapshot)
            .ToArray();
    }

    public static PlayerImagineRoleSkillLoadoutSnapshot GetPlayerImagineRoleSkills(long characterId)
    {
        var encounter = ResolvePlayerDetailEncounter();
        if (encounter is null
            || !TryResolvePlayerEntity(encounter, characterId, out var entityUuid, out var entity))
        {
            return EmptyPlayerImagineRoleSkillLoadout();
        }

        return CreatePlayerImagineRoleSkillLoadout(entityUuid, entity, characterId);
    }

    public static bool TryGetPlayerListImagineRoleSkillSourceState(
        long characterId,
        long preferredEntityUuid,
        out long entityUuid,
        out int professionId,
        out object? skillSourceToken,
        out object? roleFilterToken)
    {
        entityUuid = 0;
        professionId = 0;
        skillSourceToken = null;
        roleFilterToken = null;

        // ライブが null でも打ち切らない。下のメーター側からの拾い直しへ進む。
        var encounter = ResolvePlayerDetailEncounter();

        Entity? entity = null;
        if (encounter is not null
            && preferredEntityUuid != 0
            && TryResolvePlayerEntityByUuid(
                encounter,
                characterId,
                preferredEntityUuid,
                out entity))
        {
            entityUuid = preferredEntityUuid;
        }
        else if (encounter is not null
            && PlayerRosterProjection.TryGetPlayerEntityUuid(characterId, out var rosterEntityUuid)
            && TryResolvePlayerEntityByUuid(
                encounter,
                characterId,
                rosterEntityUuid,
                out entity))
        {
            entityUuid = rosterEntityUuid;
        }
        else if (ResolveFallbackDetailEncounter() is { } fallbackEncounter
            && TryResolvePlayerEntity(fallbackEncounter, characterId, out var fallbackEntityUuid, out entity))
        {
            // ライブに居ない灰色の行。メーターが映しているエンカウンターに枠が残っている。
            entityUuid = fallbackEntityUuid;
        }
        else if (PartyMemberCache.Instance.TryGetSkillLevels(characterId, out _))
        {
            // AOI外のパーティメンバーは Entity が存在しないことがある。
            // 保持しているスキル一覧があるなら、それを表示元として続行する。
            entity = null;
            entityUuid = preferredEntityUuid;
        }
        else
        {
            return false;
        }

        professionId = entity?.ProfessionId ?? 0;
        if (professionId <= 0
            && PartyStateStore.Instance.Current.TryGetSupplement(characterId, out var supplement))
        {
            professionId = supplement.ProfessionId;
        }
        if (IsSelfEntity(entityUuid)
            && PlayerSkillLevelStateStore.TryGetSelfCurrentSkillLevels(
                out var currentSkillLevels))
        {
            skillSourceToken = currentSkillLevels;
            roleFilterToken = currentSkillLevels;
            return true;
        }

        // 変更検知のトークンは実際の表示元と一致させる。
        // Entity 側に一覧が無いときは保持している一覧が表示元になるので、そちらを指す。
        skillSourceToken = entity?.GetAttrKV("AttrSkillLevelIdList");
        if (skillSourceToken is null
            && PartyMemberCache.Instance.TryGetSkillLevels(characterId, out var cachedSkillLevels))
        {
            skillSourceToken = cachedSkillLevels;
        }

        PlayerSkillLevelStateStore.TryGetSelfCurrentSkillLevels(
            out var roleFilterSkillLevels);
        roleFilterToken = roleFilterSkillLevels;
        return true;
    }

    public static PlayerImagineRoleSkillLoadoutSnapshot GetPlayerListImagineRoleSkills(
        long characterId,
        long entityUuid)
    {
        // ライブに居なければメーターのエンカウンターから拾う。
        // 履歴にしか居ない灰色の行でも、枠はDBに残っているので出せる。
        TryResolveDetailEntity(characterId, entityUuid, out var entity, out _);

        // AOI外のパーティメンバーは Entity が存在しないことがある。
        // その場合でも保持しているスキル一覧から組み立てる。
        return CreatePlayerImagineRoleSkillLoadout(entityUuid, entity, characterId);
    }

    private static PlayerImagineRoleSkillLoadoutSnapshot CreatePlayerImagineRoleSkillLoadout(
        long entityUuid,
        Entity? entity,
        long characterId)
    {
        // 自分はアクションバー(AttrSlot)から枠番号ごと組み立てる。
        // 並び順と空欄がゲームと一致するのはこの経路だけ。
        var isSelf = IsSelfEntity(entityUuid);
        if (isSelf)
        {
            return CreateSelfActionBarLoadout(entityUuid);
        }

        var skillLevels = ResolvePlayerSkillLevels(entityUuid, entity, characterId);

        // 他人は習得済みの集合しか届かず装備を判別できないので、上限を設けず取れた数だけ返す。
        const int SelfRoleSlotCount = 4;

        var imagineSkills = new List<PlayerCooldownSkillSnapshot>(2);
        var roleSkills = new List<PlayerCooldownSkillSnapshot>();
        var includedSkillIds = new HashSet<int>();

        foreach (var skillLevel in skillLevels)
        {
            if (skillLevel.SkillId <= 0
                || !includedSkillIds.Add(skillLevel.SkillId))
            {
                continue;
            }

            var iconName = CombatDataCatalog.GetSkillIconName(skillLevel.SkillId, skillLevel.Icon);
            var isImagine = CombatDataCatalog.IsSkillImagine(skillLevel.SkillId, iconName);
            var isRole = CombatDataCatalog.IsSkillRole(skillLevel.SkillId);

            var roleFull = isSelf && roleSkills.Count >= SelfRoleSlotCount;
            if ((!isImagine || imagineSkills.Count >= 2)
                && (!isRole || roleFull))
            {
                continue;
            }

            var currentLevel = ResolvePlayerSkillCurrentLevel(entityUuid, skillLevel);
            var showLevel = isRole
                && CombatDataCatalog.HasLevelDependentCooldown(skillLevel.SkillId);
            // スタック数はイマジン固有ではない。ロールスキルにも複数チャージのものがある
            // (例: 3612 不屈の闘志 = MaxEnergyChargeNum 2)。
            var maxCharges = CombatDataCatalog.GetSkillMaxCharges(skillLevel.SkillId);
            var chargeCooldownSeconds = maxCharges > 1
                ? CombatDataCatalog.GetSkillChargeCooldownSeconds(
                    skillLevel.SkillId,
                    skillLevel.Tier)
                : 0d;
            var snapshot = new PlayerCooldownSkillSnapshot(
                skillLevel.SkillId,
                CombatDataCatalog.GetSkillName(skillLevel.SkillId),
                iconName,
                currentLevel,
                skillLevel.Tier,
                isImagine,
                showLevel,
                CombatDataCatalog.GetSkillPveCooldownSeconds(
                    skillLevel.SkillId,
                    currentLevel,
                    skillLevel.Tier),
                maxCharges,
                chargeCooldownSeconds);

            if (isImagine && imagineSkills.Count < 2)
            {
                imagineSkills.Add(snapshot);
            }
            else if (isRole && !roleFull)
            {
                roleSkills.Add(snapshot);
            }

            if (isSelf
                && imagineSkills.Count == 2
                && roleSkills.Count == SelfRoleSlotCount)
            {
                break;
            }

            // 他人はロールスキルに上限が無いので打ち切らない。
        }

        return new PlayerImagineRoleSkillLoadoutSnapshot(
            entityUuid,
            imagineSkills,
            roleSkills,
            isSelf ? SelfRoleSlotCount : roleSkills.Count);
    }

    /// <summary>
    /// イマジンの枠番号。実測(2026-08-28)で 7 と 8。
    /// クライアントの enum にある <c>ResonanceSkillSlot_left / _right</c> と数が一致する。
    /// </summary>
    private static readonly int[] SelfImagineSlotIds = [7, 8];

    /// <summary>ロールスキルの枠番号。実測(2026-08-28)で 21〜24。</summary>
    private static readonly int[] SelfRoleSlotIds = [21, 22, 23, 24];

    /// <summary>
    /// 自分のイマジン/ロール枠を、アクションバーの枠番号どおりに組み立てる。
    ///
    /// <para>
    /// 空枠は <c>null</c> のまま返す。<b>詰めない。</b> 詰めると並び順がゲームとずれる。
    /// </para>
    ///
    /// <para>
    /// <c>AttrSkillLevelIdList</c>(習得済みの集合)へは落ちない。同じ AOI 属性でありながら
    /// 枠も装備状態も持たない下位互換なので、自分の枠を作るのに使う理由が無い。
    /// アクションバーが未受信なら空で返し、埋めない。
    /// </para>
    /// </summary>
    private static PlayerImagineRoleSkillLoadoutSnapshot CreateSelfActionBarLoadout(long entityUuid)
    {
        if (!PlayerSkillLevelStateStore.HasSelfActionBarSlots)
        {
            // 未受信。それらしい値で埋めると、受信できていないことが空欄と同じ見た目になる。
            return new PlayerImagineRoleSkillLoadoutSnapshot(
                entityUuid,
                Array.Empty<PlayerCooldownSkillSnapshot?>(),
                Array.Empty<PlayerCooldownSkillSnapshot?>(),
                0);
        }

        var imagineSkills = BuildSelfActionBarSlots(entityUuid, SelfImagineSlotIds);
        var roleSkills = BuildSelfActionBarSlots(entityUuid, SelfRoleSlotIds);
        return new PlayerImagineRoleSkillLoadoutSnapshot(
            entityUuid,
            imagineSkills,
            roleSkills,
            roleSkills.Length);
    }

    private static PlayerCooldownSkillSnapshot?[] BuildSelfActionBarSlots(
        long entityUuid,
        IReadOnlyList<int> slotIds)
    {
        var result = new PlayerCooldownSkillSnapshot?[slotIds.Count];
        for (var index = 0; index < slotIds.Count; index++)
        {
            var skillId = PlayerSkillLevelStateStore.GetSelfActionBarSkillId(slotIds[index]);
            result[index] = skillId > 0
                ? CreateSelfSlotSnapshot(entityUuid, skillId)
                : null;
        }

        return result;
    }

    private static PlayerCooldownSkillSnapshot CreateSelfSlotSnapshot(long entityUuid, int skillId)
    {
        PlayerSkillLevelStateStore.TryGetSelfLearnedSkill(skillId, out var learnedLevel, out var tier);
        var skillLevel = new DataTypes.Skills.SkillLevelInfo
        {
            SkillId = skillId,
            CurrentLevel = learnedLevel,
            Tier = tier
        };

        var iconName = CombatDataCatalog.GetSkillIconName(skillId, string.Empty);
        var isImagine = CombatDataCatalog.IsSkillImagine(skillId, iconName);
        var isRole = CombatDataCatalog.IsSkillRole(skillId);
        var currentLevel = ResolvePlayerSkillCurrentLevel(entityUuid, skillLevel);
        var maxCharges = CombatDataCatalog.GetSkillMaxCharges(skillId);
        return new PlayerCooldownSkillSnapshot(
            skillId,
            CombatDataCatalog.GetSkillName(skillId),
            iconName,
            currentLevel,
            tier,
            isImagine,
            isRole && CombatDataCatalog.HasLevelDependentCooldown(skillId),
            CombatDataCatalog.GetSkillPveCooldownSeconds(skillId, currentLevel, tier),
            maxCharges,
            maxCharges > 1
                ? CombatDataCatalog.GetSkillChargeCooldownSeconds(skillId, tier)
                : 0d);
    }

    private static PlayerImagineRoleSkillLoadoutSnapshot EmptyPlayerImagineRoleSkillLoadout()
    {
        return new PlayerImagineRoleSkillLoadoutSnapshot(
            0,
            Array.Empty<PlayerCooldownSkillSnapshot>(),
            Array.Empty<PlayerCooldownSkillSnapshot>(),
            0);
    }

    public static IReadOnlyDictionary<int, PlayerSkillEffectSnapshot> GetPlayerSkillEffects(
        long characterId,
        IReadOnlyCollection<int> skillIds)
    {
        if (skillIds.Count == 0)
        {
            return new Dictionary<int, PlayerSkillEffectSnapshot>();
        }

        var encounter = ResolvePlayerDetailEncounter();
        if (encounter is null
            || !TryResolvePlayerEntity(encounter, characterId, out _, out var entity))
        {
            return new Dictionary<int, PlayerSkillEffectSnapshot>();
        }

        return CreateSkillEffectSnapshots(
            encounter,
            entity,
            skillIds.ToHashSet());
    }

    public static IReadOnlyDictionary<int, PlayerSkillEffectSnapshot> GetPlayerListSkillEffects(
        long characterId,
        long entityUuid,
        IReadOnlySet<int> skillIds)
    {
        if (skillIds.Count == 0 || entityUuid == 0)
        {
            return new Dictionary<int, PlayerSkillEffectSnapshot>();
        }

        var encounter = ResolvePlayerDetailEncounter();
        if (encounter is null
            || !TryResolvePlayerEntityByUuid(
                encounter,
                characterId,
                entityUuid,
                out var entity))
        {
            return new Dictionary<int, PlayerSkillEffectSnapshot>();
        }

        return CreateSkillEffectSnapshots(
            encounter,
            entity,
            skillIds,
            hideReviveBlockDebuff: true);
    }

    /// <summary>
    /// 蘇生不可デバフの残り秒。
    ///
    /// <para>
    /// <b>スキル枠を経由しない。</b> エンティティが保持しているバフを直接見るので、
    /// そのプレイヤーが復活スキルを装備しているかに関係なく拾える。
    /// スキル枠経由(<see cref="CreateSkillEffectSnapshots"/>)は
    /// <c>trackedSkillIds</c> に当たったものしか残さないため、装備者しか出せない。
    /// </para>
    ///
    /// <para>
    /// 残り秒が出せないバフは <c>false</c> を返す。持続が分からないものを
    /// 蘇生不可として表示すると、切れた後も出しっぱなしになる。
    /// </para>
    /// </summary>
    public static bool TryGetReviveBlockSeconds(
        long characterId,
        long entityUuid,
        out double remainingSeconds)
    {
        remainingSeconds = 0d;
        var encounter = ResolvePlayerDetailEncounter();
        if (encounter is null
            || entityUuid == 0
            || !TryResolvePlayerEntityByUuid(encounter, characterId, entityUuid, out var entity))
        {
            return false;
        }

        var buffEvents = ResolveDisplayBuffEvents(entity);
        var found = false;
        var latestRemoveTime = TimeSpan.MinValue;

        for (var index = 0; index < buffEvents.Length; index++)
        {
            var buffEvent = buffEvents[index];
            if (buffEvent.BaseId != ReviveBlockDebuffBaseId || buffEvent.Duration < 0)
            {
                continue;
            }

            TimeSpan effectiveRemoveTime;
            double? seconds;
            var timingResolved = TryResolveLiveBuffTiming(
                entity.UUID, buffEvent, out effectiveRemoveTime, out seconds, out var secondsUnknown);
            // 経過が分からないものはカウントに使わない。出すと切れた後も残る。
            if (!timingResolved || secondsUnknown || seconds is null)
            {
                continue;
            }

            // 重ね掛けされたら遅く切れるほうを採る。バッジの選び方と同じ。
            if (found && effectiveRemoveTime <= latestRemoveTime)
            {
                continue;
            }

            latestRemoveTime = effectiveRemoveTime;
            remainingSeconds = seconds.Value;
            found = true;
        }

        return found;
    }

    private static IReadOnlyDictionary<int, PlayerSkillEffectSnapshot> CreateSkillEffectSnapshots(
        Encounter encounter,
        Entity entity,
        IReadOnlySet<int> trackedSkillIds,
        bool hideReviveBlockDebuff = false)
    {
        var buffEvents = ResolveDisplayBuffEvents(entity);
        var runtimeSourceParentsByBaseId = BuildRuntimeSourceParentsByBaseId(
            entity,
            buffEvents);
        var candidatesBySkill = new Dictionary<
            int,
            (PlayerBuffCandidate? Buff, PlayerBuffCandidate? Debuff)>();
        var resolvedTrackedSkillIdsBySourceConfigId = new Dictionary<int, int>();
        var resolvedTrackedSkillIdsBySummonUuid = new Dictionary<long, int>();

        foreach (var buffEvent in buffEvents)
        {
            if (buffEvent.Duration < 0
                || buffEvent.SourceConfigId <= 0
                || IsHiddenFromBuffBar(buffEvent))
            {
                continue;
            }

            // 蘇生不可はスキル枠のバッジには出さない。HPテキストのほうで扱う。
            if (hideReviveBlockDebuff && buffEvent.BaseId == ReviveBlockDebuffBaseId)
            {
                continue;
            }

            if (!resolvedTrackedSkillIdsBySourceConfigId.TryGetValue(
                    buffEvent.SourceConfigId,
                    out var trackedSkillId))
            {
                trackedSkillId = ResolveTrackedSourceSkillId(
                    buffEvent.SourceConfigId,
                    trackedSkillIds,
                    runtimeSourceParentsByBaseId);
                resolvedTrackedSkillIdsBySourceConfigId[buffEvent.SourceConfigId] = trackedSkillId;
            }

            if (trackedSkillId <= 0)
            {
                // 召喚したエンティティが付けたバフは、鎖が装備中スキルまで届かない。
                // 術者側から辿り直す。
                trackedSkillId = ResolveTrackedSkillFromSummonCaster(
                    encounter,
                    buffEvent.FireUuid,
                    trackedSkillIds,
                    resolvedTrackedSkillIdsBySummonUuid);
            }

            if (trackedSkillId <= 0)
            {
                continue;
            }

            var isBuff = IsIncludedBuff(PlayerBuffListKind.Buff, buffEvent);
            var isDebuff = IsIncludedBuff(PlayerBuffListKind.Debuff, buffEvent);
            if (!isBuff && !isDebuff)
            {
                continue;
            }

            var iconName = ResolveBuffOwnIconName(buffEvent);
            if (string.IsNullOrWhiteSpace(iconName))
            {
                continue;
            }

            var name = ResolveBuffName(buffEvent);
            if (string.IsNullOrWhiteSpace(name) && buffEvent.BaseId <= 0)
            {
                continue;
            }

            TimeSpan effectiveRemoveTime;
            double? remainingSeconds;
            bool remainingUnknown;
            var timingResolved = TryResolveLiveBuffTiming(
                entity.UUID, buffEvent, out effectiveRemoveTime, out remainingSeconds, out remainingUnknown);
            if (!timingResolved)
            {
                continue;
            }

            candidatesBySkill.TryGetValue(trackedSkillId, out var candidates);

            var key = ResolveBuffSnapshotKey(buffEvent);
            var snapshot = new PlayerBuffSnapshot(
                buffEvent.Uuid,
                buffEvent.BaseId,
                key,
                name,
                iconName,
                buffEvent.Layer,
                remainingSeconds,
                remainingUnknown);
            var candidate = new PlayerBuffCandidate(snapshot, effectiveRemoveTime);

            if (isDebuff)
            {
                if (candidates.Debuff is null
                    || candidate.EffectiveRemoveTime > candidates.Debuff.EffectiveRemoveTime)
                {
                    candidates.Debuff = candidate;
                }
            }
            else if (candidates.Buff is null
                     || candidate.EffectiveRemoveTime > candidates.Buff.EffectiveRemoveTime)
            {
                candidates.Buff = candidate;
            }

            candidatesBySkill[trackedSkillId] = candidates;
        }

        return candidatesBySkill.ToDictionary(
            pair => pair.Key,
            pair => new PlayerSkillEffectSnapshot(
                pair.Value.Buff?.Snapshot,
                pair.Value.Debuff?.Snapshot));
    }

    private static int ResolveTrackedSourceSkillId(
        int sourceConfigId,
        IReadOnlySet<int> trackedSkillIds,
        IReadOnlyDictionary<int, HashSet<int>> runtimeSourceParentsByBaseId)
    {
        if (sourceConfigId <= 0)
        {
            return 0;
        }

        var directTrackedSkillId = ResolveTrackedSourceSkillDirectly(
            sourceConfigId,
            trackedSkillIds);
        if (directTrackedSkillId > 0)
        {
            return directTrackedSkillId;
        }

        var pendingSourceIds = new Queue<int>();
        var visitedSourceIds = new HashSet<int>();
        var resolvedTrackedSkillIds = new HashSet<int>();
        pendingSourceIds.Enqueue(sourceConfigId);

        while (pendingSourceIds.Count > 0)
        {
            var currentSourceId = pendingSourceIds.Dequeue();
            if (currentSourceId <= 0 || !visitedSourceIds.Add(currentSourceId))
            {
                continue;
            }

            if (currentSourceId != sourceConfigId)
            {
                var resolvedTrackedSkillId = ResolveTrackedSourceSkillDirectly(
                    currentSourceId,
                    trackedSkillIds);
                if (resolvedTrackedSkillId > 0)
                {
                    resolvedTrackedSkillIds.Add(resolvedTrackedSkillId);
                    if (resolvedTrackedSkillIds.Count > 1)
                    {
                        return 0;
                    }
                }
            }

            if (HelperMethods.DataTables.Skills.Data.TryGetValue(
                    currentSourceId.ToString(),
                    out var sourceSkill))
            {
                EnqueuePositiveSourceId(
                    pendingSourceIds,
                    sourceSkill.SkillLevelGroup,
                    currentSourceId);
                EnqueuePositiveSourceId(
                    pendingSourceIds,
                    sourceSkill.SwitchSkillId,
                    currentSourceId);
                EnqueuePositiveSourceId(
                    pendingSourceIds,
                    sourceSkill.NextSkillId,
                    currentSourceId);
            }

            if (runtimeSourceParentsByBaseId.TryGetValue(
                    currentSourceId,
                    out var runtimeParents))
            {
                foreach (var runtimeParent in runtimeParents)
                {
                    EnqueuePositiveSourceId(
                        pendingSourceIds,
                        runtimeParent,
                        currentSourceId);
                }
            }
        }

        return resolvedTrackedSkillIds.Count == 1
            ? resolvedTrackedSkillIds.First()
            : 0;
    }

    /// <summary>
    /// 召喚エンティティが付けたバフを、召喚元のスキルへ帰属させる。
    ///
    /// <para>
    /// 実測(2026-09-01、3903 奥义！炽炎战斧)では、バフ <c>2110065</c> の
    /// <c>SourceConfigId</c> が <c>2110064</c>(スキルではなく<b>バフ</b>ID)で、
    /// <c>SkillLevelGroup</c> 等の鎖もそこで途切れる。<c>BuffTable.SkillId</c> も 0。
    /// 静的にも実行時にも装備中スキルへ繋がる経路が無い。
    /// </para>
    ///
    /// <para>
    /// 一方、召喚体側のテーブルは元スキルを指している。
    /// <c>MonsterTable[3000009].BornSkillId = 390301</c> で、これは
    /// <c>SkillTable[3903].EffectIDs</c> に入っているエフェクトID。ここを辿る。
    /// </para>
    ///
    /// <para>
    /// 術者が召喚体かどうかは <b>UUIDのビット</b>(<see cref="Utils.IsSummonByUuid"/>)で判る。
    /// 推測ではなく、テーブルとワイヤに書かれた参照だけを辿る。
    /// 複数の装備中スキルに当たったときは<b>帰属させない</b>(既存の鎖と同じ方針)。
    /// </para>
    /// </summary>
    private static int ResolveTrackedSkillFromSummonCaster(
        Encounter encounter,
        long fireUuid,
        IReadOnlySet<int> trackedSkillIds,
        Dictionary<long, int> resolvedBySummonUuid)
    {
        if (fireUuid == 0 || !Utils.IsSummonByUuid(fireUuid))
        {
            return 0;
        }

        if (resolvedBySummonUuid.TryGetValue(fireUuid, out var cached))
        {
            return cached;
        }

        var resolved = 0;

        if (encounter.Entities.TryGetValue(fireUuid, out var caster)
            && caster.UID > 0
            && HelperMethods.DataTables.Monsters.Data.TryGetValue(
                caster.UID.ToString(),
                out var monster))
        {
            var summonSkillIds = new HashSet<int>();
            if (monster.BornSkillId > 0)
            {
                summonSkillIds.Add(monster.BornSkillId);
            }

            if (monster.SkillIds is not null)
            {
                foreach (var summonSkillId in monster.SkillIds)
                {
                    if (summonSkillId > 0)
                    {
                        summonSkillIds.Add(summonSkillId);
                    }
                }
            }

            foreach (var trackedSkillId in trackedSkillIds)
            {
                if (!SummonBelongsToSkill(trackedSkillId, summonSkillIds))
                {
                    continue;
                }

                if (resolved > 0 && resolved != trackedSkillId)
                {
                    resolved = 0;
                    break;
                }

                resolved = trackedSkillId;
            }
        }

        resolvedBySummonUuid[fireUuid] = resolved;
        return resolved;
    }

    /// <summary>
    /// 召喚体が持つスキル/エフェクトIDが、その装備中スキルのものか。
    /// エフェクトIDは <c>SkillTable.EffectIDs</c> の逆引きで確認する
    /// (<c>エフェクトID / 100</c> のような規則には頼らない)。
    /// </summary>
    private static bool SummonBelongsToSkill(int trackedSkillId, HashSet<int> summonSkillIds)
    {
        if (summonSkillIds.Contains(trackedSkillId))
        {
            return true;
        }

        if (!HelperMethods.DataTables.Skills.Data.TryGetValue(
                trackedSkillId.ToString(),
                out var trackedSkill)
            || trackedSkill.EffectIDs is null)
        {
            return false;
        }

        foreach (var effectId in trackedSkill.EffectIDs)
        {
            if (summonSkillIds.Contains(effectId))
            {
                return true;
            }
        }

        return false;
    }

    private static int ResolveTrackedSourceSkillDirectly(
        int sourceConfigId,
        IReadOnlySet<int> trackedSkillIds)
    {
        if (trackedSkillIds.Contains(sourceConfigId))
        {
            return sourceConfigId;
        }

        HelperMethods.DataTables.Skills.Data.TryGetValue(
            sourceConfigId.ToString(),
            out var sourceSkill);

        if (sourceSkill is not null)
        {
            if (sourceSkill.SkillLevelGroup > 0
                && trackedSkillIds.Contains(sourceSkill.SkillLevelGroup))
            {
                return sourceSkill.SkillLevelGroup;
            }

            if (sourceSkill.SwitchSkillId > 0
                && trackedSkillIds.Contains(sourceSkill.SwitchSkillId))
            {
                return sourceSkill.SwitchSkillId;
            }

            if (sourceSkill.NextSkillId > 0
                && trackedSkillIds.Contains(sourceSkill.NextSkillId))
            {
                return sourceSkill.NextSkillId;
            }
        }

        var reverseLinkedTrackedSkillId = 0;
        foreach (var trackedSkillId in trackedSkillIds)
        {
            if (!HelperMethods.DataTables.Skills.Data.TryGetValue(
                    trackedSkillId.ToString(),
                    out var trackedSkill)
                || (trackedSkill.SwitchSkillId != sourceConfigId
                    && trackedSkill.NextSkillId != sourceConfigId))
            {
                continue;
            }

            if (reverseLinkedTrackedSkillId > 0
                && reverseLinkedTrackedSkillId != trackedSkillId)
            {
                return 0;
            }

            reverseLinkedTrackedSkillId = trackedSkillId;
        }

        if (reverseLinkedTrackedSkillId > 0)
        {
            return reverseLinkedTrackedSkillId;
        }

        return ResolveTrackedSourceSkillBySharedImagineIcon(
            sourceSkill,
            trackedSkillIds);
    }

    private static int ResolveTrackedSourceSkillBySharedImagineIcon(
        DataTypes.Skill? sourceSkill,
        IReadOnlySet<int> trackedSkillIds)
    {
        if (sourceSkill is null)
        {
            return 0;
        }

        var sourceIcon = NormalizeSkillFamilyIcon(sourceSkill.GetIconName());
        if (string.IsNullOrWhiteSpace(sourceIcon)
            || !sourceIcon.Contains("skill_aoyi", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        var matchedTrackedSkillId = 0;
        foreach (var trackedSkillId in trackedSkillIds)
        {
            if (!HelperMethods.DataTables.Skills.Data.TryGetValue(
                    trackedSkillId.ToString(),
                    out var trackedSkill)
                || !string.Equals(
                    sourceIcon,
                    NormalizeSkillFamilyIcon(trackedSkill.GetIconName()),
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (matchedTrackedSkillId > 0 && matchedTrackedSkillId != trackedSkillId)
            {
                return 0;
            }

            matchedTrackedSkillId = trackedSkillId;
        }

        return matchedTrackedSkillId;
    }

    private static Dictionary<int, HashSet<int>> BuildRuntimeSourceParentsByBaseId(
        Entity entity,
        IReadOnlyCollection<BuffEvent> buffEvents)
    {
        var result = new Dictionary<int, HashSet<int>>();

        foreach (var buffEvent in buffEvents)
        {
            AddRuntimeSourceParent(result, buffEvent);
        }

        foreach (var buffEvent in entity.RecentBuffEventHistory.Values)
        {
            AddRuntimeSourceParent(result, buffEvent);
        }

        // 既に切れた/解除されたバフの対応も逆引きには必要。
        // エンカウンターが作り直されると entity 側の履歴は空になるため、
        // 境界を跨いで蓄積しているストア側の索引を合流させる。
        Services.ActiveBuffStore.Instance.CopySourceParentsInto(entity.UUID, result);

        return result;
    }

    private static void AddRuntimeSourceParent(
        Dictionary<int, HashSet<int>> runtimeSourceParentsByBaseId,
        BuffEvent buffEvent)
    {
        if (buffEvent.BaseId <= 0
            || buffEvent.SourceConfigId <= 0
            || buffEvent.BaseId == buffEvent.SourceConfigId)
        {
            return;
        }

        if (!runtimeSourceParentsByBaseId.TryGetValue(buffEvent.BaseId, out var sourceIds))
        {
            sourceIds = new HashSet<int>();
            runtimeSourceParentsByBaseId.Add(buffEvent.BaseId, sourceIds);
        }

        sourceIds.Add(buffEvent.SourceConfigId);
    }

    private static void EnqueuePositiveSourceId(
        Queue<int> sourceIds,
        int sourceId,
        int currentSourceId)
    {
        if (sourceId > 0 && sourceId != currentSourceId)
        {
            sourceIds.Enqueue(sourceId);
        }
    }

    private static string NormalizeSkillFamilyIcon(string? iconName)
    {
        if (string.IsNullOrWhiteSpace(iconName))
        {
            return string.Empty;
        }

        var normalized = iconName.Trim().Replace('\\', '/');
        var separatorIndex = normalized.LastIndexOf('/');
        return separatorIndex >= 0
            ? normalized[(separatorIndex + 1)..]
            : normalized;
    }

    public static MetricTimelineSnapshot GetPlayerTimeline(
        MeterSnapshotKind kind,
        long characterId,
        int aggregationIntervalSeconds)
    {
        var intervalSeconds = NormalizeTimelineAggregationIntervalSeconds(aggregationIntervalSeconds);
        var encounter = ResolveActiveEncounter();
        if (encounter is null
            || !TryResolvePlayerEntity(encounter, characterId, out _, out var entity))
        {
            return new MetricTimelineSnapshot(0UL, Array.Empty<MetricTimelinePoint>());
        }

        var stats = kind == MeterSnapshotKind.Damage
            ? entity.DamageStats
            : entity.HealingStats;
        var snapshots = stats.GetSkillSnapshotsCopy()
            .Where(snapshot => IsIncludedSnapshot(kind, snapshot))
            .Where(snapshot => snapshot.Timestamp.HasValue)
            .OrderBy(snapshot => snapshot.Timestamp)
            .ToArray();

        var totalValue = GetPlayerTotalValue(entity, kind);
        if (snapshots.Length == 0)
        {
            return new MetricTimelineSnapshot(totalValue, Array.Empty<MetricTimelinePoint>());
        }

        var startTime = encounter.ExData.FirstDamageTimeStamp
            ?? stats.StartTime
            ?? snapshots[0].Timestamp!.Value;
        var endTime = ResolveMetricEndTime(encounter);
        var lastSnapshotTime = snapshots[^1].Timestamp!.Value;

        if (endTime < lastSnapshotTime)
        {
            endTime = lastSnapshotTime;
        }

        if (endTime < startTime)
        {
            startTime = snapshots[0].Timestamp!.Value;
        }

        var elapsedSeconds = Math.Max((endTime - startTime).TotalSeconds, 0d);
        var sampleCount = (int)Math.Floor(elapsedSeconds);
        if (sampleCount == 0)
        {
            return new MetricTimelineSnapshot(totalValue, Array.Empty<MetricTimelinePoint>());
        }

        var perSecondValues = new double[sampleCount];
        foreach (var snapshot in snapshots)
        {
            var seconds = Math.Max((snapshot.Timestamp!.Value - startTime).TotalSeconds, 0d);
            var sampleIndex = Math.Max((int)Math.Ceiling(seconds) - 1, 0);
            if (sampleIndex < sampleCount)
            {
                perSecondValues[sampleIndex] += Math.Max(snapshot.Value, 0L);
            }
        }

        var points = new MetricTimelinePoint[sampleCount];
        var rollingValue = 0d;
        for (var index = 0; index < sampleCount; index++)
        {
            rollingValue += perSecondValues[index];
            if (index >= intervalSeconds)
            {
                rollingValue -= perSecondValues[index - intervalSeconds];
            }

            var windowSeconds = Math.Min(index + 1, intervalSeconds);
            points[index] = new MetricTimelinePoint(index + 1, rollingValue / windowSeconds);
        }

        return new MetricTimelineSnapshot(totalValue, points);
    }

    private static int NormalizeTimelineAggregationIntervalSeconds(int aggregationIntervalSeconds)
    {
        return aggregationIntervalSeconds is 5 or 3 or 2 or 1
            ? aggregationIntervalSeconds
            : 10;
    }

    public static PlayerMetricSummarySnapshot GetPlayerMetricSummary(MeterSnapshotKind kind, long characterId)
    {
        var encounter = ResolveActiveEncounter();
        if (encounter is null
            || !TryResolvePlayerEntity(encounter, characterId, out _, out var entity))
        {
            return CreateEmptyMetricSummary(kind);
        }

        var stats = kind == MeterSnapshotKind.Damage
            ? entity.DamageStats
            : entity.HealingStats;
        var extraTotal = kind == MeterSnapshotKind.Damage
            ? entity.TotalShieldBreak
            : entity.TotalOverhealing;
        var castsPerMinute = default(double?);
        var castsPerSecond = default(double?);

        if (entity.FirstCombatActionTime is { } firstAction
            && entity.LastCombatActionTime is { } lastAction)
        {
            var activeDuration = lastAction - firstAction;
            if (activeDuration.TotalSeconds > 0d)
            {
                var totalCasts = (double)entity.TotalCasts;
                castsPerSecond = Math.Round(totalCasts / activeDuration.TotalSeconds, 2);
                castsPerMinute = Math.Round(totalCasts / activeDuration.TotalMinutes, 2);
            }
        }

        return new PlayerMetricSummarySnapshot(
            stats.ValueTotal,
            stats.ValuePerSecondActive,
            stats.ValuePerSecond,
            extraTotal,
            stats.HitsCount,
            stats.CritRate,
            stats.LuckyRate,
            stats.CritCount,
            stats.ImmuneCount,
            kind == MeterSnapshotKind.Damage,
            stats.ValueNormalTotal,
            stats.ValueCritTotal,
            stats.ValueLuckyTotal,
            stats.LuckyCount,
            stats.ValueAverage,
            entity.TotalCasts,
            castsPerMinute,
            castsPerSecond);
    }

    private static PlayerMetricSummarySnapshot CreateEmptyMetricSummary(MeterSnapshotKind kind)
    {
        return new PlayerMetricSummarySnapshot(
            0UL,
            0d,
            0d,
            0UL,
            0UL,
            0d,
            0d,
            0U,
            0UL,
            kind == MeterSnapshotKind.Damage,
            0UL,
            0UL,
            0UL,
            0U,
            0d,
            0UL,
            null,
            null);
    }

    public static MetricSkillTableSnapshot GetPlayerSkillTable(MeterSnapshotKind kind, long characterId)
    {
        var encounter = ResolveActiveEncounter();
        if (encounter is null
            || !TryResolvePlayerEntity(encounter, characterId, out _, out var entity))
        {
            return new MetricSkillTableSnapshot(0UL, Array.Empty<MetricSkillTableRowSnapshot>());
        }

        IReadOnlyList<KeyValuePair<long, CombatStats>> skillStats = kind switch
        {
            MeterSnapshotKind.Damage => (IReadOnlyList<KeyValuePair<long, CombatStats>>)entity.SkillMetrics
                .AsValueEnumerable()
                .Where(entry => entry.Value.Damage.ValueTotal > 0UL)
                .OrderByDescending(entry => entry.Value.Damage.ValueTotal)
                .Select(entry => new KeyValuePair<long, CombatStats>(entry.Key, entry.Value.Damage))
                .ToList(),
            MeterSnapshotKind.Healing => (IReadOnlyList<KeyValuePair<long, CombatStats>>)entity.SkillMetrics
                .AsValueEnumerable()
                .Where(entry => entry.Value.Healing.ValueTotal > 0UL)
                .OrderByDescending(entry => entry.Value.Healing.ValueTotal)
                .Select(entry => new KeyValuePair<long, CombatStats>(entry.Key, entry.Value.Healing))
                .ToList(),
            _ => Array.Empty<KeyValuePair<long, CombatStats>>()
        };

        var entityTotalValue = GetPlayerTotalValue(entity, kind);
        if (skillStats.Count == 0 || entityTotalValue == 0UL)
        {
            return new MetricSkillTableSnapshot(entityTotalValue, Array.Empty<MetricSkillTableRowSnapshot>());
        }

        var rows = new MetricSkillTableRowSnapshot[skillStats.Count];
        for (var index = 0; index < skillStats.Count; index++)
        {
            var stat = skillStats[index];
            var value = stat.Value;
            var percentage = value.ValueTotal > 0UL
                ? Math.Round(((double)value.ValueTotal / entityTotalValue) * 100d, 0)
                : 0d;

            // バフとして届いたかどうか。名前には影響しない(見出し表は種別を区別しない)。
            // 内部ID注記の表示区分にだけ効く。
            var isBuffSource = entity.SkillMetrics.TryGetValue(stat.Key, out var sourceContainer)
                && sourceContainer.IsBuffSource;
            var rowKeyText = CombatDataCatalog.FormatSourceKey(stat.Key);

            // 名前が入っていない行を常設で拾う。スキル詳細ウィジェットの行はここでしか
            // 作られないので、ここに置けば取りこぼしが構造的に起きない。
            //
            // 判定は注記を付ける前の生名で行う。表示名は空欄でも "(2203531:1)" の注記が付いて
            // 空文字にならず、しかも注記は表示設定で消えるので、表示名で見ると設定次第で検知が変わる。
            if (string.IsNullOrEmpty(CombatDataCatalog.GetSourceName(stat.Key)))
            {
                Diagnostics.BlankSourceNameProbe.Capture(
                    stat.Key,
                    rowKeyText,
                    isBuffSource,
                    kind == MeterSnapshotKind.Healing,
                    value.ValueTotal,
                    value.HitsCount,
                    characterId);
            }

            rows[index] = new MetricSkillTableRowSnapshot(
                stat.Key,
                rowKeyText,
                // 記録時の名前(英語)ではなく、表示中の言語で引き直す。
                CombatDataCatalog.GetSourceDisplayName(stat.Key, isBuffSource),
                value.ValueTotal,
                value.ValuePerSecondActive,
                value.ValuePerSecond,
                value.HitsCount,
                value.CritRate,
                value.ValueAverage,
                percentage);
        }

        return new MetricSkillTableSnapshot(entityTotalValue, rows);
    }

    public static BenchmarkStateSnapshot GetBenchmarkState()
    {
        return new BenchmarkStateSnapshot(
            AppState.IsBenchmarkMode,
            AppState.HasBenchmarkBegun,
            AppState.IsBenchmarkCompleted);
    }

    public static bool TryStartBenchmark(int durationSeconds)
    {
        if (durationSeconds < 5
            || AppState.IsBenchmarkMode)
        {
            return false;
        }

        BattleStateMachine.CancelBenchmarkCompletionTimer();
        AppState.BenchmarkTime = durationSeconds;
        AppState.BenchmarkSingleTargetUUID = 0;
        AppState.IsBenchmarkCompleting = false;
        AppState.IsBenchmarkCompleted = false;
        AppState.BenchmarkCompletionTime = null;
        AppState.IsBenchmarkMode = true;
        ResetCurrentEncounter();
        return true;
    }

    public static bool TryStopBenchmark()
    {
        if (!AppState.IsBenchmarkMode)
        {
            return false;
        }

        var wasCompleted = AppState.IsBenchmarkCompleted;
        var completionTime = AppState.BenchmarkCompletionTime;
        BattleStateMachine.CancelBenchmarkCompletionTimer();

        if (wasCompleted && completionTime is { } completedAt)
        {
            EncounterManager.SetCurrentBenchmarkEndTime(completedAt);
        }

        AppState.HasBenchmarkBegun = false;
        AppState.IsBenchmarkMode = false;

        try
        {
            EncounterManager.EnterDungeon(wasCompleted, EncounterStartReason.BenchmarkEnd);
        }
        finally
        {
            AppState.IsBenchmarkCompleting = false;
            AppState.IsBenchmarkCompleted = false;
            AppState.BenchmarkCompletionTime = null;
        }

        return true;
    }

    public static void ResetCurrentEncounter()
    {
        if (AppState.IsBenchmarkMode && AppState.HasBenchmarkBegun)
        {
            Log.Information($"Manual early ending of Benchmark at {DateTime.Now}");
            TryStopBenchmark();
            return;
        }


        var isOpenWorld = BattleStateMachine.IsInOpenWorld();
        Task.Factory.StartNew(() =>
        {
            if (AppState.IsBenchmarkMode)
            {
                Log.Information($"Starting new Benchmark encounter at {DateTime.Now}");
                EncounterManager.EnterDungeon(true, EncounterStartReason.BenchmarkStart);
            }
            else if (isOpenWorld)
            {
                EncounterManager.EnterDungeon(true, EncounterStartReason.Force);
            }
            else
            {
                EncounterManager.EnterDungeon(true, EncounterStartReason.NewObjective);
            }
        });
    }


    private static TimeSpan ResolveMetricDuration(Encounter encounter)
    {
        if (ReferenceEquals(encounter, EncounterManager.Current)
            && AppState.IsBenchmarkMode
            && AppState.IsBenchmarkCompleted
            && AppState.BenchmarkCompletionTime is { } completionTime)
        {
            return completionTime.Subtract(encounter.StartTime).Duration();
        }

        return encounter.GetDuration();
    }

    /// <summary>
    /// タイムラインの終端。<b>必ず UTC で返す。</b>
    ///
    /// <para>
    /// <see cref="Encounter"/> は<b>基準の違う時刻を同居させている</b>。
    /// <c>StartTime</c> / <c>EndTime</c> は <c>DateTime.Now</c>(ローカル)、
    /// <c>ExData.FirstDamageTimeStamp</c> と <c>SkillSnapshot.Timestamp</c> は
    /// パケットの到着時刻(UTC)。<see cref="GetPlayerTimeline"/> は後者を起点にするので、
    /// 終端をローカルのまま渡すと<b>差が時差ぶん膨らむ</b>。
    /// </para>
    ///
    /// <para>
    /// 実測(2026-09-12、JST): 3分計測の記録が
    /// <c>EndTime 16:13:53(ローカル) − FirstDamageTimeStamp 07:10:53(UTC) = 32,580秒</c> になり、
    /// 180点のはずのグラフが 32,580 点を毎回作っていた(横軸も 32580s と表示)。
    /// ライブは <c>DateTime.UtcNow</c>、計測完了は <c>ToUniversalTime()</c> で
    /// 元から UTC だったため、<b>履歴を開いたときだけ</b>起きる。
    /// </para>
    ///
    /// <para>
    /// <c>Encounter.GetDuration()</c> は同じ引き算を
    /// <c>EndTime.ToUniversalTime()</c> で揃えている。こちらだけ変換が抜けていた。
    /// </para>
    /// </summary>
    private static DateTime ResolveMetricEndTime(Encounter encounter)
    {
        if (ReferenceEquals(encounter, EncounterManager.Current)
            && AppState.IsBenchmarkMode
            && AppState.IsBenchmarkCompleted
            && AppState.BenchmarkCompletionTime is { } completionTime)
        {
            return completionTime.ToUniversalTime();
        }

        return encounter.EndTime == DateTime.MinValue
            ? DateTime.UtcNow
            : encounter.EndTime.ToUniversalTime();
    }


    /// <summary>
    /// ゲーム内のバフバーが出さないバフか。
    ///
    /// <para>
    /// ゲーム内のバフ表示は <c>BuffPriority</c> が <c>NotShow</c> のバフを出さない。
    /// 自分のバーもボスHPバーのバーも同じ条件で、対象エンティティが違うだけ。
    /// </para>
    ///
    /// <para>
    /// 以前はアイコンの有無だけで判定しており、ゲームが出さないもの
    /// (計数・マーカー・移動アクションの有効化など)まで出していた。
    /// 逆にゲームが出してこちらが出さないものは無く、ゲーム側は厳密な部分集合。
    /// </para>
    /// </summary>
    private static bool IsHiddenFromBuffBar(BuffEvent buffEvent)
        => buffEvent.BuffPriority == DataTypes.Enum.EBuffPriority.NotShow;

    private static bool IsIncludedBuff(PlayerBuffListKind kind, BuffEvent buffEvent)
    {
        return kind switch
        {
            PlayerBuffListKind.Buff => buffEvent.BuffType is DataTypes.Enum.EBuffType.Gain
                or DataTypes.Enum.EBuffType.GainRecovery,
            PlayerBuffListKind.Debuff => buffEvent.BuffType == DataTypes.Enum.EBuffType.Debuff,
            _ => false
        };
    }

    private static string ResolveBuffName(BuffEvent buffEvent)
    {
        return buffEvent.BaseId > 0
            ? CombatDataCatalog.GetBuffName(buffEvent.BaseId)
            : string.Empty;
    }

    private static string ResolveBuffOwnIconName(BuffEvent buffEvent)
    {
        return buffEvent.BaseId > 0
            ? CombatDataCatalog.GetBuffOwnIconName(buffEvent.BaseId)
            : string.Empty;
    }

    private static string ResolveBuffSnapshotKey(BuffEvent buffEvent)
    {
        if (buffEvent.BaseId > 0)
        {
            return $"base:{buffEvent.BaseId}";
        }

        return $"uuid:{buffEvent.Uuid}";
    }

    /// <summary>
    /// 表示用のバフ取得。<b>常に <see cref="Services.ActiveBuffStore"/> だけを見る。</b>
    /// あのストアはエンカウンターに閉じ込められていないので、境界で表示が消えない。
    ///
    /// <para>
    /// ストアが空なら空のまま返す。エンカウンター経路へのフォールバックは意図的に持たない
    /// (故障時に従来経路で正常に見えてしまうと、ストア側の不具合が発見できなくなるため)。
    /// </para>
    /// </summary>
    private static BuffEvent[] ResolveDisplayBuffEvents(Entity entity)
    {
        return [.. Services.ActiveBuffStore.Instance.GetActive(entity.UUID)];
    }

    /// <summary>
    /// ライブ表示用の残り時間。基準は <see cref="Services.ActiveBuffStore"/> が持つ観測時刻。
    /// <see cref="BuffEvent.AddDateTime"/> はサーバ由来の時刻が入る経路があってローカル時計とずれ、
    /// <see cref="BuffEvent.EventAddTime"/> はエンカウンター相対で境界を跨げないため、どちらも使えない。
    /// </summary>
    private static bool TryResolveLiveBuffTiming(
        long entityUuid,
        BuffEvent buffEvent,
        out TimeSpan orderingKey,
        out double? remainingSeconds,
        out bool remainingUnknown)
    {
        if (!Services.ActiveBuffStore.Instance.TryGetRemainingSeconds(
                entityUuid,
                (ulong)buffEvent.Uuid,
                out remainingSeconds,
                out remainingUnknown))
        {
            orderingKey = TimeSpan.Zero;
            remainingSeconds = null;
            remainingUnknown = false;
            return false;
        }

        // 持続時間なし(null)のものと経過が分からないものは残り時間を出さないが、
        // 表示は残すので最後尾に並べる。
        orderingKey = remainingSeconds is > 0d
            ? TimeSpan.FromSeconds(remainingSeconds.Value)
            : TimeSpan.MaxValue;
        return true;
    }

    private static void UpdatePlayerMeterState(IReadOnlyList<MeterPlayerSnapshot> players)
    {
        if (AppState.PlayerUUID == 0)
        {
            return;
        }

        for (var index = 0; index < players.Count; index++)
        {
            var player = players[index];
            if (!player.IsSelf)
            {
                continue;
            }

            AppState.PlayerMeterPlacement = index + 1;
            AppState.PlayerTotalMeterValue = player.TotalValue;
            AppState.PlayerMeterValuePerSecond = player.ValuePerSecond;
            return;
        }
    }

    private static bool IsIncludedSnapshot(MeterSnapshotKind kind, SkillSnapshot snapshot)
    {
        return snapshot.Value > 0
            && (kind != MeterSnapshotKind.Damage || snapshot.DamageType != EDamageType.Immune);
    }

    private static int ResolvePlayerSkillCurrentLevel(
        long entityUuid,
        DataTypes.Skills.SkillLevelInfo skillLevel)
    {
        if (IsSelfEntity(entityUuid)
            && !CombatDataCatalog.IsSkillRole(skillLevel.SkillId)
            && PlayerSkillLevelStateStore.TryGetSelfSkillLevel(
                skillLevel.SkillId,
                out var selfLevel))
        {
            return selfLevel;
        }

        return skillLevel.CurrentLevel;
    }

    private static bool IsSelfEntity(long entityUuid)
    {
        return entityUuid != 0
            && (entityUuid == MessageManager.currentUserUuid
                || entityUuid == AppState.PlayerUUID
                || (AppState.PlayerUID != 0
                    && Utils.UuidToEntityId(entityUuid) == AppState.PlayerUID));
    }

    private static IReadOnlyList<DataTypes.Skills.SkillLevelInfo> ResolvePlayerSkillLevels(
        long entityUuid,
        Entity? entity,
        long characterId)
    {
        if (IsSelfEntity(entityUuid)
            && PlayerSkillLevelStateStore.TryGetSelfCurrentSkillLevels(
                out var currentSkillLevels))
        {
            return currentSkillLevels;
        }

        var receivedSkillLevels = entity?.GetAttrKV("AttrSkillLevelIdList") switch
        {
            List<DataTypes.Skills.SkillLevelInfo> typedList => typedList,
            JArray jsonArray =>
                (IReadOnlyList<DataTypes.Skills.SkillLevelInfo>?)
                jsonArray.ToObject<List<DataTypes.Skills.SkillLevelInfo>>()
                ?? Array.Empty<DataTypes.Skills.SkillLevelInfo>(),
            _ => Array.Empty<DataTypes.Skills.SkillLevelInfo>()
        };

        // AOI外へ出ると AttrSkillLevelIdList が届かなくなるので、
        // 自分以外のパーティメンバーは保持しておいた分で補完する。
        if (receivedSkillLevels.Count == 0
            && !IsSelfEntity(entityUuid)
            && PartyMemberCache.Instance.TryGetSkillLevels(
                characterId,
                out var cachedSkillLevels))
        {
            receivedSkillLevels = cachedSkillLevels;
        }

        // AOI外だと Entity が無い場合がある。職業はパーティのsocial dataからライブで引ける。
        var professionId = entity?.ProfessionId ?? 0;
        if (professionId <= 0
            && PartyStateStore.Instance.Current.TryGetSupplement(characterId, out var supplement))
        {
            professionId = supplement.ProfessionId;
        }

        // ここでは絞らない。この戻り値はスキルリストウィジェット(全スキル一覧)にも使われ、
        // そこでは習得済みを全部出すのが正しい。
        // 「装備中の4枠」を出すのはプレイヤーリスト側だけなので、
        // ロールスキルの絞り込みは CreatePlayerImagineRoleSkillLoadout で行う。
        return receivedSkillLevels;
    }

    private static bool TryResolvePlayerEntityByUuid(
        Encounter encounter,
        long characterId,
        long entityUuid,
        out Entity entity)
    {
        if (entityUuid == 0
            || !encounter.Entities.TryGetValue(entityUuid, out var resolvedEntity)
            || resolvedEntity.EntityType != EEntityType.EntChar)
        {
            entity = null!;
            return false;
        }

        var playerId = resolvedEntity.UID != 0
            ? resolvedEntity.UID
            : Utils.UuidToEntityId(entityUuid);
        if (playerId != characterId)
        {
            entity = null!;
            return false;
        }

        entity = resolvedEntity;
        return true;
    }

    private static bool TryResolvePlayerEntity(
        Encounter encounter,
        long characterId,
        out long entityUuid,
        out Entity entity)
    {
        if (encounter.Entities.TryGetValue(characterId, out var resolvedEntity)
            && resolvedEntity.EntityType == EEntityType.EntChar)
        {
            entityUuid = characterId;
            entity = resolvedEntity;
            return true;
        }

        foreach (var pair in encounter.Entities)
        {
            if (pair.Value.EntityType != EEntityType.EntChar)
            {
                continue;
            }

            var playerId = pair.Value.UID != 0
                ? pair.Value.UID
                : Utils.UuidToEntityId(pair.Key);
            if (playerId != characterId)
            {
                continue;
            }

            entityUuid = pair.Key;
            entity = pair.Value;
            return true;
        }

        entityUuid = 0;
        entity = null!;
        return false;
    }

    private static ulong GetPlayerTotalValue(Entity entity, MeterSnapshotKind kind)
    {
        return kind == MeterSnapshotKind.Damage
            ? entity.TotalDamage
            : entity.TotalHealing;
    }

    /// <summary>
    /// プレイヤーリストとバフ系が見るエンカウンター。<b>常にライブ。</b>
    ///
    /// <para>
    /// HP・バフ・スキル枠はDBから復元できないので、履歴を開いても固める意味がない。
    /// 履歴に追従するのは <see cref="ResolveActiveEncounter"/> を通る4系統だけ
    /// (メーター2種 / スキル詳細 / ヒール・ダメージ詳細 / グラフ)。
    /// </para>
    /// </summary>
    private static Encounter? ResolvePlayerDetailEncounter()
    {
        return EncounterManager.Current;
    }

    /// <summary>
    /// ライブに居ない人の受け皿。<b>メーターが映しているエンカウンター。</b>
    ///
    /// <para>
    /// 履歴にしか居ない灰色の行でも、イマジン/ロールと特化はDBに残っている
    /// (<c>Entity.Attributes</c> は public プロパティ、<c>SubProfessionId</c> は private setter 付きで
    /// どちらも blob に載る)。ライブで引けなかったときだけここから拾う。
    /// <b>ライブが先。</b> 逆にすると、AOIに居る人まで履歴の値で固まる。
    /// </para>
    /// </summary>
    private static Encounter? ResolveFallbackDetailEncounter()
    {
        return AppState.OpenedHistoricalEncounter;
    }

    /// <summary>
    /// ライブ → メーターのエンカウンター の順で実体を引く。
    /// <paramref name="isFromFallback"/> は「ライブでは見つからず、履歴から拾った」を表す。
    /// </summary>
    private static bool TryResolveDetailEntity(
        long characterId,
        long entityUuid,
        out Entity? entity,
        out bool isFromFallback)
    {
        entity = null;
        isFromFallback = false;

        var live = ResolvePlayerDetailEncounter();
        if (live is not null
            && TryResolvePlayerEntityByUuid(live, characterId, entityUuid, out entity))
        {
            return true;
        }

        var fallback = ResolveFallbackDetailEncounter();
        if (fallback is not null
            && TryResolvePlayerEntity(fallback, characterId, out _, out entity))
        {
            isFromFallback = true;
            return true;
        }

        entity = null;
        return false;
    }

    private static Encounter? ResolveActiveEncounter()
    {
        Encounter? activeEncounter = AppState.OpenedHistoricalEncounter;
        var currentEncounter = EncounterManager.Current;

        if (CombatRuntimeSettings.KeepPastEncounterInMeterUntilNextDamage)
        {
            if ((AppState.ActiveEncounter is null && currentEncounter is not null)
                || (AppState.ActiveEncounter is not null && AppState.ActiveEncounter.Entities.IsEmpty))
            {
                AppState.ActiveEncounter = currentEncounter;
            }
            else if (AppState.ActiveEncounter?.BattleId != currentEncounter?.BattleId)
            {
                AppState.ActiveEncounter = currentEncounter;
            }
            else if (AppState.ActiveEncounter?.EncounterId != currentEncounter?.EncounterId
                || AppState.ActiveEncounter?.StartTime != currentEncounter?.StartTime)
            {
                if (currentEncounter is not null && currentEncounter.HasStatsBeenRecorded())
                {
                    AppState.ActiveEncounter = currentEncounter;
                }
            }
        }
        else if (AppState.ActiveEncounter?.EncounterId != currentEncounter?.EncounterId
            || AppState.ActiveEncounter?.BattleId != currentEncounter?.BattleId
            || AppState.ActiveEncounter?.StartTime != currentEncounter?.StartTime)
        {
            AppState.ActiveEncounter = currentEncounter;
        }

        return activeEncounter ?? AppState.ActiveEncounter;
    }

    private static MeterPlayerSnapshot CreatePlayerValue(long characterId, Entity entity, MeterSnapshotKind kind)
    {
        var totalValue = kind == MeterSnapshotKind.Damage
            ? entity.TotalDamage
            : entity.TotalHealing;
        var valuePerSecond = kind == MeterSnapshotKind.Damage
            ? entity.DamageStats.ValuePerSecond
            : entity.HealingStats.ValuePerSecond;
        var isSelf = IsSelf(entity);
        var source = PlayerDataSourceResolver.Resolve(entity, isSelf);
        return new MeterPlayerSnapshot(
            characterId,
            source.CharacterId,
            source.Name,
            source.ProfessionId,
            source.SubProfessionId,
            PlayerClassSpecResolver.Resolve(
                source.ProfessionId,
                source.SubProfessionId,
                source.IsSpecAbilityUnequipped),
            source.CombatPower,
            source.SeasonStrength,
            source.Level,
            source.SeasonLevel,
            isSelf,
            source.IsNpc,
            totalValue,
            valuePerSecond,
            0d,
            0d);
    }

    private static bool IsSelf(Entity entity)
    {
        var uuid = entity.UUID;
        return uuid != 0
            && (uuid == MessageManager.currentUserUuid
                || uuid == AppState.PlayerUUID
                || (AppState.PlayerUID != 0 && Utils.UuidToEntityId(uuid) == AppState.PlayerUID));
    }

}
