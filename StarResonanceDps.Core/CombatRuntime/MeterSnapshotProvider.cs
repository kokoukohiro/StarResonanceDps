using Newtonsoft.Json.Linq;
using Serilog;
using StarResonanceDps.Core.CombatRuntime.DataTypes;
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
    bool IsCompleted,
    bool IsEncounterSavingPaused);

public sealed record MetricTimelinePoint(double Seconds, double ValuePerSecond);

public sealed record MetricTimelineSnapshot(
    ulong TotalValue,
    IReadOnlyList<MetricTimelinePoint> Points);

public sealed record MetricSkillTableRowSnapshot(
    int SkillId,
    string Name,
    string IconName,
    bool IsImagine,
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
    string Key,
    string Name,
    string IconName,
    int Layer,
    double? RemainingSeconds);

public sealed record PlayerSkillInfoSnapshot(
    int SkillId,
    string Name,
    string IconName,
    int CurrentLevel,
    int Tier,
    bool IsImagine);

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

public sealed record PlayerImagineRoleSkillLoadoutSnapshot(
    long EntityUuid,
    IReadOnlyList<PlayerCooldownSkillSnapshot> ImagineSkills,
    IReadOnlyList<PlayerCooldownSkillSnapshot> RoleSkills);

internal sealed record PlayerBuffCandidate(
    PlayerBuffSnapshot Snapshot,
    TimeSpan EffectiveRemoveTime);

public sealed record MeterPlayerIdentity(string Name, long UserId);

public static class MeterSnapshotProvider
{
    public static MeterSnapshot GetSnapshot(MeterSnapshotKind kind)
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

        var totalValue = kind == MeterSnapshotKind.Damage
            ? encounter.TotalDamage
            : encounter.TotalHealing;
        var topValue = source.Length == 0
            ? 0UL
            : source[0].TotalValue;
        var players = source
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

        var userId = entity.UID != 0
            ? entity.UID
            : Utils.UuidToEntityId(entityUuid);
        return new MeterPlayerIdentity(entity.Name ?? string.Empty, userId);
    }


    public static IReadOnlyList<PlayerBuffSnapshot> GetPlayerBuffs(long characterId, PlayerBuffListKind kind)
    {
        var encounter = ResolveActiveEncounter();
        if (encounter is null
            || !TryResolvePlayerEntity(encounter, characterId, out _, out var entity))
        {
            return Array.Empty<PlayerBuffSnapshot>();
        }

        return CreateBuffSnapshots(encounter, entity, kind);
    }

    public static IReadOnlyList<PlayerBuffSnapshot> GetEntityBuffs(long entityUuid, PlayerBuffListKind kind)
    {
        var encounter = EncounterManager.Current;
        if (encounter is null
            || !encounter.Entities.TryGetValue(entityUuid, out var entity))
        {
            return Array.Empty<PlayerBuffSnapshot>();
        }

        return CreateBuffSnapshots(encounter, entity, kind);
    }

    private static IReadOnlyList<PlayerBuffSnapshot> CreateBuffSnapshots(
        Encounter encounter,
        Entity entity,
        PlayerBuffListKind kind)
    {
        var currentEncounterTime = encounter.GetDuration();
        var buffEvents = entity.BuffEvents.Values.ToArray();
        var entriesByKey = new Dictionary<string, PlayerBuffCandidate>(StringComparer.Ordinal);
        var entryKeys = new List<string>(buffEvents.Length);

        for (var index = buffEvents.Length - 1; index >= 0; index--)
        {
            var buffEvent = buffEvents[index];
            if (buffEvent.Duration < 0 || !IsIncludedBuff(kind, buffEvent))
            {
                continue;
            }

            var name = ResolveBuffName(buffEvent);
            if (string.IsNullOrWhiteSpace(name) && buffEvent.BaseId <= 0)
            {
                continue;
            }

            if (!TryResolveBuffTiming(buffEvent, currentEncounterTime, out var effectiveRemoveTime, out var remainingSeconds))
            {
                continue;
            }

            var key = ResolveBuffSnapshotKey(buffEvent);
            var snapshot = new PlayerBuffSnapshot(
                buffEvent.Uuid,
                key,
                name,
                ResolveBuffIconName(buffEvent),
                buffEvent.Layer,
                remainingSeconds);
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

    public static IReadOnlyList<PlayerSkillInfoSnapshot> GetPlayerSkillInfo(long characterId)
    {
        var encounter = ResolvePlayerDetailEncounter();
        if (encounter is null
            || !TryResolvePlayerEntity(encounter, characterId, out var entityUuid, out var entity))
        {
            return Array.Empty<PlayerSkillInfoSnapshot>();
        }

        var skillLevels = ResolvePlayerSkillLevels(entityUuid, entity);

        if (skillLevels.Count == 0)
        {
            return Array.Empty<PlayerSkillInfoSnapshot>();
        }

        var snapshots = new PlayerSkillInfoSnapshot[skillLevels.Count];
        for (var index = 0; index < skillLevels.Count; index++)
        {
            var skillLevel = skillLevels[index];
            var iconName = CombatDataCatalog.GetSkillIconName(skillLevel.SkillId, skillLevel.Icon);
            snapshots[index] = new PlayerSkillInfoSnapshot(
                skillLevel.SkillId,
                CombatDataCatalog.GetSkillName(skillLevel.SkillId, skillLevel.Name),
                iconName,
                skillLevel.CurrentLevel,
                skillLevel.Tier,
                CombatDataCatalog.IsSkillImagine(skillLevel.SkillId, iconName));
        }

        return snapshots;
    }

    public static PlayerImagineRoleSkillLoadoutSnapshot GetPlayerImagineRoleSkills(long characterId)
    {
        var encounter = ResolvePlayerDetailEncounter();
        if (encounter is null
            || !TryResolvePlayerEntity(encounter, characterId, out var entityUuid, out var entity))
        {
            return new PlayerImagineRoleSkillLoadoutSnapshot(
                0,
                Array.Empty<PlayerCooldownSkillSnapshot>(),
                Array.Empty<PlayerCooldownSkillSnapshot>());
        }

        var skillLevels = ResolvePlayerSkillLevels(entityUuid, entity);
        var imagineSkills = new List<PlayerCooldownSkillSnapshot>(2);
        var roleSkills = new List<PlayerCooldownSkillSnapshot>(4);
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

            if ((!isImagine || imagineSkills.Count >= 2)
                && (!isRole || roleSkills.Count >= 4))
            {
                continue;
            }

            var currentLevel = ResolvePlayerSkillCurrentLevel(entityUuid, skillLevel);
            var showLevel = isRole
                && CombatDataCatalog.HasLevelDependentCooldown(skillLevel.SkillId);
            var maxCharges = isImagine
                ? CombatDataCatalog.GetSkillMaxCharges(skillLevel.SkillId)
                : 0;
            var chargeCooldownSeconds = maxCharges > 1
                ? CombatDataCatalog.GetSkillChargeCooldownSeconds(
                    skillLevel.SkillId,
                    skillLevel.Tier)
                : 0d;
            var snapshot = new PlayerCooldownSkillSnapshot(
                skillLevel.SkillId,
                CombatDataCatalog.GetSkillName(skillLevel.SkillId, skillLevel.Name),
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
            else if (isRole && roleSkills.Count < 4)
            {
                roleSkills.Add(snapshot);
            }

            if (imagineSkills.Count == 2 && roleSkills.Count == 4)
            {
                break;
            }
        }

        return new PlayerImagineRoleSkillLoadoutSnapshot(
            entityUuid,
            imagineSkills,
            roleSkills);
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

        IReadOnlyList<KeyValuePair<int, CombatStats>> skillStats = kind switch
        {
            MeterSnapshotKind.Damage => (IReadOnlyList<KeyValuePair<int, CombatStats>>)entity.SkillMetrics
                .AsValueEnumerable()
                .Where(entry => entry.Value.Damage.ValueTotal > 0UL)
                .OrderByDescending(entry => entry.Value.Damage.ValueTotal)
                .Select(entry => new KeyValuePair<int, CombatStats>(entry.Key, entry.Value.Damage))
                .ToList(),
            MeterSnapshotKind.Healing => (IReadOnlyList<KeyValuePair<int, CombatStats>>)entity.SkillMetrics
                .AsValueEnumerable()
                .Where(entry => entry.Value.Healing.ValueTotal > 0UL)
                .OrderByDescending(entry => entry.Value.Healing.ValueTotal)
                .Select(entry => new KeyValuePair<int, CombatStats>(entry.Key, entry.Value.Healing))
                .ToList(),
            _ => Array.Empty<KeyValuePair<int, CombatStats>>()
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

            var iconName = CombatDataCatalog.GetSkillIconName(stat.Key);
            rows[index] = new MetricSkillTableRowSnapshot(
                stat.Key,
                value.Name ?? string.Empty,
                iconName,
                CombatDataCatalog.IsSkillImagine(stat.Key, iconName),
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
            AppState.IsBenchmarkCompleted,
            AppState.IsEncounterSavingPaused);
    }

    public static bool TryStartBenchmark(int durationSeconds)
    {
        if (durationSeconds < 5
            || AppState.IsEncounterSavingPaused
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

        if (AppState.IsEncounterSavingPaused)
        {
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
            : encounter.EndTime;
    }


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
            ? CombatDataCatalog.GetBuffName(buffEvent.BaseId, buffEvent.Name)
            : buffEvent.Name ?? string.Empty;
    }

    private static string ResolveBuffIconName(BuffEvent buffEvent)
    {
        return CombatDataCatalog.GetBuffIconName(
            buffEvent.BaseId,
            buffEvent.SourceConfigId,
            buffEvent.Icon);
    }

    private static string ResolveBuffSnapshotKey(BuffEvent buffEvent)
    {
        if (buffEvent.BaseId > 0)
        {
            return $"base:{buffEvent.BaseId}";
        }

        return $"uuid:{buffEvent.Uuid}";
    }

    private static bool TryResolveBuffTiming(
        BuffEvent buffEvent,
        TimeSpan currentEncounterTime,
        out TimeSpan effectiveRemoveTime,
        out double remainingSeconds)
    {
        if (buffEvent.EventRemoveTime > TimeSpan.Zero)
        {
            effectiveRemoveTime = buffEvent.EventRemoveTime;
        }
        else if (buffEvent.EventAddTime > TimeSpan.Zero && buffEvent.Duration > 0)
        {
            effectiveRemoveTime = buffEvent.EventAddTime + TimeSpan.FromMilliseconds(buffEvent.Duration);
        }
        else if (buffEvent.AddDateTime != DateTime.MinValue && buffEvent.Duration > 0)
        {
            var remainingWallClockSeconds = (buffEvent.AddDateTime + TimeSpan.FromMilliseconds(buffEvent.Duration) - DateTime.Now).TotalSeconds;
            if (remainingWallClockSeconds <= 0d)
            {
                effectiveRemoveTime = TimeSpan.Zero;
                remainingSeconds = 0d;
                return false;
            }

            effectiveRemoveTime = currentEncounterTime + TimeSpan.FromSeconds(remainingWallClockSeconds);
        }
        else
        {
            effectiveRemoveTime = TimeSpan.Zero;
            remainingSeconds = 0d;
            return false;
        }

        remainingSeconds = (effectiveRemoveTime - currentEncounterTime).TotalSeconds;
        if (remainingSeconds <= 0d)
        {
            remainingSeconds = 0d;
            return false;
        }

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
        Entity entity)
    {
        if (IsSelfEntity(entityUuid)
            && PlayerSkillLevelStateStore.TryGetSelfCurrentSkillLevels(
                out var currentSkillLevels))
        {
            return currentSkillLevels;
        }

        var receivedSkillLevels = entity.GetAttrKV("AttrSkillLevelIdList") switch
        {
            List<DataTypes.Skills.SkillLevelInfo> typedList => typedList,
            JArray jsonArray =>
                (IReadOnlyList<DataTypes.Skills.SkillLevelInfo>?)
                jsonArray.ToObject<List<DataTypes.Skills.SkillLevelInfo>>()
                ?? Array.Empty<DataTypes.Skills.SkillLevelInfo>(),
            _ => Array.Empty<DataTypes.Skills.SkillLevelInfo>()
        };

        if (IsSelfEntity(entityUuid)
            || !PlayerSkillLevelStateStore.TryGetRoleSkillIdsForProfession(
                entity.ProfessionId,
                out var currentRoleSkillIds))
        {
            return receivedSkillLevels;
        }

        return receivedSkillLevels
            .Where(skill => !CombatDataCatalog.IsSkillRole(skill.SkillId)
                || currentRoleSkillIds.Contains(skill.SkillId))
            .ToArray();
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

    private static Encounter? ResolvePlayerDetailEncounter()
    {
        return AppState.OpenedHistoricalEncounter ?? EncounterManager.Current;
    }

    private static Encounter? ResolveActiveEncounter()
    {
        Encounter? activeEncounter = AppState.OpenedHistoricalEncounter;
        var currentEncounter = EncounterManager.Current;

        if (Settings.Instance.KeepPastEncounterInMeterUntilNextDamage)
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
        return new MeterPlayerSnapshot(
            characterId,
            entity.UID,
            entity.Name ?? string.Empty,
            entity.ProfessionId,
            entity.SubProfessionId,
            entity.AbilityScore,
            ToInt32(entity.SeasonStrength),
            entity.Level,
            ToInt32(entity.SeasonLevel),
            isSelf,
            !isSelf && entity.HasNpcEvidence,
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

    private static int ToInt32(long value)
    {
        return value switch
        {
            > int.MaxValue => int.MaxValue,
            < int.MinValue => int.MinValue,
            _ => (int)value
        };
    }
}
