using StarResonanceDps.Core.CombatRuntime.DataTypes;
using ZLinq;
using Zproto;

namespace StarResonanceDps.Core.CombatRuntime;

public enum MeterSnapshotKind
{
    Damage,
    Healing
}

public sealed record MeterPlayerSnapshot(
    long CharacterId,
    long UserId,
    string Name,
    int ProfessionId,
    int SubProfessionId,
    int AbilityScore,
    int SeasonStrength,
    int SeasonLevel,
    bool IsSelf,
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

public sealed record MetricTimelinePoint(double Seconds, double ValuePerSecond);

public sealed record MetricTimelineSnapshot(
    ulong TotalValue,
    IReadOnlyList<MetricTimelinePoint> Points);

public sealed record MetricSkillTableRowSnapshot(
    int SkillId,
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
            encounter.GetDuration(),
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
        var endTime = encounter.EndTime == DateTime.MinValue
            ? DateTime.UtcNow
            : encounter.EndTime;
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

            rows[index] = new MetricSkillTableRowSnapshot(
                stat.Key,
                value.Name ?? string.Empty,
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

    public static void ResetCurrentEncounter()
    {
        if (AppState.IsBenchmarkMode && AppState.HasBenchmarkBegun)
        {
            AppState.HasBenchmarkBegun = false;
            AppState.IsBenchmarkMode = false;
            EncounterManager.EnterDungeon(false, EncounterStartReason.BenchmarkEnd);
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
        return new MeterPlayerSnapshot(
            characterId,
            entity.UID,
            entity.Name ?? string.Empty,
            entity.ProfessionId,
            entity.SubProfessionId,
            entity.AbilityScore,
            ToInt32(entity.SeasonStrength),
            ToInt32(entity.SeasonLevel),
            IsSelf(entity),
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
