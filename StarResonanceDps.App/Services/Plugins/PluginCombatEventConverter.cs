// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.PluginSdk;
using Zproto;

namespace StarResonanceDps.App.Services;

/// <summary>
/// Core の戦闘のできごと・記録を SDK の型へ写す。流れ(<see cref="PluginPacketHub"/>)と読み口(<see cref="PluginEncounterRecord"/>)の両方が使い、
/// 同じ記録は同じ型・同じ値になる。列挙はゲームの番号のまま写す(SDK の列挙に無い番号もそのままの数)。
/// キャラ番号は UUID の上位(<see cref="Utils.UuidToEntityId"/>、<c>PluginBuff.CasterCharacterId</c> と同じ)。
/// </summary>
internal static class PluginCombatEventConverter
{
    public static PluginCombatEvent ToDamageEvent(long sequenceNumber, DamageObservation observation)
    {
        return new PluginDamageEvent(
            sequenceNumber,
            observation.ArrivalUtc,
            observation.EncounterKey,
            Utils.UuidToEntityId(observation.AttackerUuid),
            observation.SourceEntityUuid,
            observation.TargetUuid,
            observation.TargetMonsterId,
            CombatDataCatalog.FormatSourceKey(observation.SkillKey),
            (PluginDamageSource)observation.Source,
            observation.SourceId,
            observation.Value,
            observation.HpLessen,
            observation.ShieldBreak,
            (PluginDamageElement)observation.Element,
            (PluginDamageMode)observation.Mode,
            observation.IsCrit,
            observation.IsLucky,
            observation.IsMiss,
            observation.IsImmune,
            observation.IsKill);
    }

    public static PluginCombatEvent ToSkillActivationEvent(long sequenceNumber, SkillActivationObservation observation)
    {
        var record = ToSkillActivation(observation.Activation);
        return new PluginSkillActivationEvent(
            sequenceNumber,
            record.ArrivalTimeUtc,
            observation.EncounterKey,
            Utils.UuidToEntityId(observation.PlayerUuid),
            observation.RecordIndex,
            record);
    }

    public static PluginSkillActivation ToSkillActivation(SkillActivation activation)
    {
        return new PluginSkillActivation(
            activation.SkillId,
            (PluginSkillFrame)CombatDataCatalog.GetSkillIconFrame(activation.SkillId),
            activation.Timestamp);
    }

    public static PluginCombatEvent ToTakenHitEvent(long sequenceNumber, TakenHitObservation observation)
    {
        var record = ToTakenHit(observation);
        return new PluginTakenHitEvent(sequenceNumber, record.ArrivalTimeUtc, observation.EncounterKey, observation.RecordIndex, record);
    }

    public static PluginTakenHit ToTakenHit(TakenHitObservation observation)
    {
        var hit = observation.Record;
        return new PluginTakenHit(
            hit.Sequence,
            hit.Timestamp,
            Utils.UuidToEntityId(hit.TargetUuid),
            ToParty(observation.Attacker),
            (PluginDamageSource)hit.DamageSource,
            hit.OwnerId,
            hit.BuffSourceSkillId,
            hit.SummonSourceSkillId,
            hit.Value,
            hit.DamageElement is { } element ? (PluginDamageElement)element : null,
            (PluginDamageMode)hit.DamageMode,
            hit.IsKill);
    }

    public static PluginCombatEvent ToCombatLogEvent(long sequenceNumber, CombatLogObservation observation)
    {
        var record = ToCombatLogRecord(observation);
        return new PluginCombatLogEvent(sequenceNumber, record.ArrivalTimeUtc, observation.EncounterKey, observation.RecordIndex, record);
    }

    public static PluginCombatLogRecord ToCombatLogRecord(CombatLogObservation observation)
    {
        var record = observation.Record;
        switch (record.Kind)
        {
            case CombatEventLogRecordKind.Cast:
                var cast = record.Cast!;
                return new PluginCastRecord(
                    cast.Sequence,
                    cast.Timestamp,
                    ToParty(observation.Actor!),
                    cast.SkillId,
                    cast.IsWarning);
            case CombatEventLogRecordKind.Announcement:
            case CombatEventLogRecordKind.AnnouncementEnd:
                var announcement = record.Announcement!;
                return new PluginAnnouncementRecord(
                    announcement.Sequence,
                    announcement.Timestamp,
                    announcement.SkillId,
                    announcement.OwnerMonsterId > 0 ? announcement.OwnerMonsterId : null,
                    announcement.IsBarEnd);
            case CombatEventLogRecordKind.Death:
                var death = record.Death!;
                return new PluginPlayerDeathRecord(death.Sequence, death.Timestamp, Utils.UuidToEntityId(death.PlayerUuid));
            case CombatEventLogRecordKind.EnemyHeal:
                var heal = record.EnemyHeal!;
                return new PluginEnemyHealRecord(
                    heal.Sequence,
                    heal.Timestamp,
                    ToParty(observation.Actor!),
                    ToParty(observation.Target!),
                    (PluginDamageSource)heal.DamageSource,
                    heal.OwnerId,
                    heal.BuffSourceSkillId,
                    heal.SummonSourceSkillId,
                    heal.Value,
                    heal.CastSequence);
            case CombatEventLogRecordKind.EnemyDamage:
                var damage = record.EnemyDamage!;
                return new PluginEnemyDamageRecord(
                    damage.Sequence,
                    damage.Timestamp,
                    ToParty(observation.Actor!),
                    ToParty(observation.Target!),
                    (PluginDamageSource)damage.DamageSource,
                    damage.OwnerId,
                    damage.BuffSourceSkillId,
                    damage.SummonSourceSkillId,
                    damage.Value,
                    damage.CastSequence);
            case CombatEventLogRecordKind.EnemyBuff:
                var buff = record.EnemyBuff!;
                return new PluginEnemyBuffRecord(
                    buff.Sequence,
                    buff.Timestamp,
                    ToParty(observation.Actor!),
                    ToParty(observation.Target!),
                    buff.BuffId,
                    buff.DurationMs,
                    (PluginDamageSource)buff.DamageSource,
                    buff.OwnerId,
                    buff.BuffSourceSkillId,
                    buff.SummonSourceSkillId,
                    buff.CastSequence);
            default:
                throw new ArgumentOutOfRangeException(nameof(observation), record.Kind, "Unknown Timeline record kind.");
        }
    }

    /// <summary>登場人物の種類は UUID の種類で決める(実体が無くても決まる)。モンスターの番号は受けていなければ null。</summary>
    public static PluginCombatParty ToParty(CombatEventLogParty party)
    {
        return (EEntityType)Utils.UuidToEntityType(party.Uuid) switch
        {
            EEntityType.EntChar => new PluginCombatParty(party.Uuid, PluginCombatPartyKind.Player, Utils.UuidToEntityId(party.Uuid), null),
            EEntityType.EntMonster => new PluginCombatParty(party.Uuid, PluginCombatPartyKind.Monster, null, party.MonsterId > 0 ? party.MonsterId : null),
            EEntityType.EntDummy => new PluginCombatParty(party.Uuid, PluginCombatPartyKind.System, null, null),
            _ => new PluginCombatParty(party.Uuid, PluginCombatPartyKind.Other, null, null)
        };
    }

    /// <summary>バフの持ち主の種類(プレイヤーかモンスター。Core はほかの実体のバフを上げない)。</summary>
    public static PluginCombatEventKinds GetBuffEventKind(BuffObservation observation)
    {
        return (EEntityType)Utils.UuidToEntityType(observation.HolderUuid) switch
        {
            EEntityType.EntChar => PluginCombatEventKinds.PlayerBuff,
            EEntityType.EntMonster => PluginCombatEventKinds.MonsterBuff,
            _ => PluginCombatEventKinds.None
        };
    }

    public static PluginCombatEvent ToBuffEvent(long sequenceNumber, BuffObservation observation)
    {
        return new PluginBuffEvent(
            sequenceNumber,
            observation.ArrivalUtc,
            observation.Kind switch
            {
                BuffObservationKind.Added => PluginBuffEventKind.Added,
                BuffObservationKind.Changed => PluginBuffEventKind.Changed,
                BuffObservationKind.Removed => PluginBuffEventKind.Removed,
                BuffObservationKind.Present => PluginBuffEventKind.Present,
                BuffObservationKind.ListEnd => PluginBuffEventKind.AppearListEnd,
                _ => throw new ArgumentOutOfRangeException(nameof(observation), observation.Kind, "Unknown buff observation kind.")
            },
            observation.HolderUuid,
            ToCharacterIdOrNull(observation.HolderUuid),
            observation.BuffUuid,
            observation.BuffId,
            observation.Level,
            observation.Layer,
            observation.DurationMilliseconds,
            observation.ServerCreateUtc,
            observation.CasterUuid,
            ToCharacterIdOrNull(observation.CasterUuid),
            observation.FightSourceType is { } sourceType ? (PluginFightSourceType)sourceType : null,
            observation.SourceConfigId,
            observation.IsRecorded);
    }

    public static PluginCombatEvent ToMonsterSkillEvent(long sequenceNumber, MonsterSkillObservation observation)
    {
        return new PluginMonsterSkillEvent(
            sequenceNumber,
            observation.ArrivalUtc,
            observation.EntityUuid,
            observation.MonsterId,
            observation.PreviousSkillId,
            observation.SkillId,
            observation.SkillLevel,
            observation.IsRecorded);
    }

    public static PluginCombatEvent ToMonsterHpZeroEvent(long sequenceNumber, MonsterHpZeroObservation observation)
    {
        return new PluginMonsterHpZeroEvent(
            sequenceNumber,
            observation.ArrivalUtc,
            observation.EntityUuid,
            observation.MonsterId,
            observation.IsZero,
            observation.PreviousHp,
            observation.MaxHp,
            observation.IsOnAppear,
            observation.IsRecorded);
    }

    /// <summary>プレイヤーの UUID ならキャラ番号、それ以外(0 を含む)は null。</summary>
    private static long? ToCharacterIdOrNull(long uuid)
    {
        return uuid != 0 && (EEntityType)Utils.UuidToEntityType(uuid) == EEntityType.EntChar
            ? Utils.UuidToEntityId(uuid)
            : null;
    }
}
