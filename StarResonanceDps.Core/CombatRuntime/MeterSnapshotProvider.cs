using StarResonanceDps.Core.CombatRuntime.DataTypes;
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
