using ProtoBuf;
using Zproto;

namespace StarResonanceDps.Core.CombatRuntime;

/// <summary>
/// スナップショットを作るときに押す印。
/// </summary>
/// <param name="Sequence">
/// エンカウンター内の通し番号。エンティティをまたいで並べ直すのに使う。
/// <c>Timestamp</c> はパケットの到着時刻なので、同じパケットのイベントどうしで順序が決まらない。
/// </param>
/// <param name="TargetHp">
/// 対象の HP。同期で届く HP は、その同期の被弾・回復を全部当てた後の1つだけなので、
/// 同期の中で被ダメログに載る最後の被弾にだけ入れ、それ以外の被弾は null。
/// <c>TargetMaxHp</c> と <c>TargetShield</c> も同じ。
/// </param>
/// <param name="OwnerId">
/// <c>SyncDamageInfo.OwnerId</c> の生の値。<c>DamageSource</c> によってスキルIDかバフIDになる。
/// スナップショットの <c>Id</c> はメーター用に畳んだ鍵なので、技名はこちらで引く。
/// </param>
/// <param name="BuffSourceSkillId">
/// バフ由来のとき、そのバフを付けた技ID。記録した瞬間の実体からしか決まらない。決まらなければ 0。
/// </param>
/// <param name="SummonSourceSkillId">
/// ダメージを出した実体(仮想体など)を出した技ID。実体の出現時に決めて控えたもの。決まらなければ 0。
/// </param>
public readonly record struct SkillSnapshotStamp(
    long Sequence,
    long? TargetHp,
    long? TargetMaxHp,
    long? TargetShield,
    int OwnerId,
    EDamageSource DamageSource,
    int BuffSourceSkillId,
    int SummonSourceSkillId);

/// <summary>
/// 詠唱の開始1件。<b>詠唱バーを持つ技</b>(<see cref="CombatDataCatalog.HasSingOrGuideTime"/>)と
/// 戦闘画面の警告の技(<see cref="CombatDataCatalog.IsWarningSkill"/>)を、プレイヤー以外が始めたときだけ残す。
/// </summary>
public sealed class SkillCastRecord
{
    public int SkillId { get; set; }

    /// <summary>技の開始が届いたパケットの到着時刻(UTC)。</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>被ダメと同じ通し番号。並べ直しに使う。</summary>
    public long Sequence { get; set; }
}

/// <summary>
/// ボス大技の予告1件。予告の通知(<c>BossDbm</c>)が届いたら、持続が 0 のものも含めて全部残す。
/// 予告のバーが終わった時刻にも1件残す(<see cref="IsBarEnd"/>)。
///
/// <para>
/// 通知は誰が構えたかを運ばないので、<see cref="OwnerMonsterId"/> は通知が届いた時点で
/// 周囲にいるモンスターのうち、その技を <c>MonsterTable.SkillIds</c> に持つものの種別ID。
/// 見つからなければ 0。名前は表示のときにこの種別IDから引く。
/// </para>
/// </summary>
[ProtoContract]
public sealed class SkillAnnouncementRecord
{
    /// <summary>通知の番号から引いた技ID(<see cref="CombatDataCatalog.TryResolveDbmSkillId"/>)。引けなければ 0。</summary>
    [ProtoMember(1)]
    public int SkillId { get; set; }

    [ProtoMember(2)]
    public int OwnerMonsterId { get; set; }

    /// <summary>通知が届いたパケットの到着時刻(UTC)。<see cref="IsBarEnd"/> ならバーが終わった時刻(UTC)。</summary>
    [ProtoMember(3)]
    public DateTime Timestamp { get; set; }

    /// <summary>被ダメと同じ通し番号。並べ直しに使う。</summary>
    [ProtoMember(4)]
    public long Sequence { get; set; }

    /// <summary>予告のバーが終わった時刻の行か(<c>BossDbmBarStore</c> のバーの終わり)。偽なら予告の通知が届いた時刻の行。</summary>
    [ProtoMember(5)]
    public bool IsBarEnd { get; set; }
}

/// <summary>
/// ダメージの無い死亡(ダンジョンの仕掛けの即死など)1件。死亡の印つきの被弾がある死亡は被弾の行で分かるので残さない。
/// 画面では「システムの攻撃」の技の行と、その下の死亡の行になる。
/// </summary>
[ProtoContract]
public sealed class PlayerDeathRecord
{
    [ProtoMember(1)]
    public long PlayerUuid { get; set; }

    /// <summary>死亡を見た時点の最大HP。属性が届いていなければ null。</summary>
    [ProtoMember(2)]
    public long? MaxHp { get; set; }

    /// <summary>死亡が届いたパケットの到着時刻(UTC)。</summary>
    [ProtoMember(3)]
    public DateTime Timestamp { get; set; }

    /// <summary>被ダメと同じ通し番号。並べ直しに使う。</summary>
    [ProtoMember(4)]
    public long Sequence { get; set; }
}

public enum TakenDamageLogRecordKind
{
    Cast,
    Hit,
    Announcement,
    Death,

    /// <summary>予告のバーが終わった時刻の行(<see cref="SkillAnnouncementRecord.IsBarEnd"/>)。</summary>
    AnnouncementEnd
}

/// <summary>
/// 被ダメログの1件。詠唱なら <see cref="EntityUuid"/> は詠唱した敵、被弾と死亡ならそのプレイヤー。
/// 予告は実体を持たないので 0。
/// </summary>
public readonly record struct TakenDamageLogRecord(
    TakenDamageLogRecordKind Kind,
    long EntityUuid,
    SkillSnapshot? Hit,
    SkillCastRecord? Cast,
    SkillAnnouncementRecord? Announcement,
    PlayerDeathRecord? Death)
{
    public long Sequence => Kind switch
    {
        TakenDamageLogRecordKind.Hit => Hit!.Sequence,
        TakenDamageLogRecordKind.Cast => Cast!.Sequence,
        TakenDamageLogRecordKind.Death => Death!.Sequence,
        _ => Announcement!.Sequence
    };

    public static TakenDamageLogRecord ForHit(long targetUuid, SkillSnapshot snapshot)
    {
        return new TakenDamageLogRecord(TakenDamageLogRecordKind.Hit, targetUuid, snapshot, null, null, null);
    }

    public static TakenDamageLogRecord ForCast(long casterUuid, SkillCastRecord cast)
    {
        return new TakenDamageLogRecord(TakenDamageLogRecordKind.Cast, casterUuid, null, cast, null, null);
    }

    public static TakenDamageLogRecord ForAnnouncement(SkillAnnouncementRecord announcement)
    {
        var kind = announcement.IsBarEnd ? TakenDamageLogRecordKind.AnnouncementEnd : TakenDamageLogRecordKind.Announcement;
        return new TakenDamageLogRecord(kind, 0, null, null, announcement, null);
    }

    public static TakenDamageLogRecord ForDeath(PlayerDeathRecord death)
    {
        return new TakenDamageLogRecord(TakenDamageLogRecordKind.Death, death.PlayerUuid, null, null, null, death);
    }
}
