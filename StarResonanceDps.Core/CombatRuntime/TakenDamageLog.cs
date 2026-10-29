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
    int OwnerId,
    EDamageSource DamageSource,
    int BuffSourceSkillId,
    int SummonSourceSkillId);

/// <summary>
/// 対象の HP・最大HP・バリア量の組。ダメージの無い戦闘不能の記録に、同期を当てる前の値として渡す。
/// 値を持っていない項目は null(バリア量は控えが無ければ 0)。
/// </summary>
public readonly record struct TargetHealth(long? Hp, long? MaxHp, long? Shield);

/// <summary>
/// 詠唱の開始1件。<b>詠唱バーを持つ技</b>(<see cref="CombatDataCatalog.HasSingOrGuideTime"/>)と
/// 戦闘画面の警告の技(<see cref="CombatDataCatalog.IsWarningSkill"/>)を、プレイヤー以外が始めたときだけ残す。
/// タイムラインに出すのは警告の技と、敵の行動の記録がつながった(<c>CastSequence</c> がこの詠唱を指す)詠唱だけ。
/// </summary>
public sealed class SkillCastRecord
{
    public int SkillId { get; set; }

    /// <summary>技の開始が届いたパケットの到着時刻(UTC)。</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>被ダメと同じ通し番号。並べ直しに使う。</summary>
    public long Sequence { get; set; }

    /// <summary>戦闘画面の警告の技か(技レベルで決まるので、記録の瞬間に決める)。警告の技は行動がつながらなくても出す。</summary>
    public bool IsWarning { get; set; }
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
/// ダメージの無い死亡(ダンジョンの仕掛けの即死など)1件。死亡の印つきの被弾がある死亡は被弾の記録で分かるので残さない。
/// プレイヤーリストの顔ぶれ(その回に出てきた人か)の判定が読む。
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

    /// <summary>死亡を伝えた同期を当てる前の HP。持っていなければ null。</summary>
    [ProtoMember(5)]
    public long? HpBefore { get; set; }

    /// <summary>死亡を伝えた同期を当てる前の最大HP。持っていなければ null。</summary>
    [ProtoMember(6)]
    public long? MaxHpBefore { get; set; }

    /// <summary>死亡を伝えた同期を当てる前のバリア量。持っていなければ null。</summary>
    [ProtoMember(7)]
    public long? ShieldBefore { get; set; }
}

/// <summary>
/// 敵の回復1件。回復した側がプレイヤー(とその召喚)でない、実際に HP が増えた回復だけを残す。回復された側は敵でもプレイヤーでもよい
/// (どれを残すかは <c>MessageManager.ProcessAoiSyncDelta</c> が決める)。画面では回復した側の技の行の下の回復の行になる。
/// </summary>
[ProtoContract]
public sealed class EnemyHealRecord
{
    /// <summary>回復した側。召喚体は被弾の加害者と同じく寄せる(<c>Encounter.ResolveTakenDamageLogActor</c>)。</summary>
    [ProtoMember(1)]
    public long HealerUuid { get; set; }

    [ProtoMember(2)]
    public long TargetUuid { get; set; }

    /// <summary><c>SyncDamageInfo.OwnerId</c> の生の値。<see cref="DamageSource"/> によって技・弾・バフの番号になる。</summary>
    [ProtoMember(3)]
    public int OwnerId { get; set; }

    [ProtoMember(4)]
    public EDamageSource DamageSource { get; set; }

    /// <summary>バフ由来のとき、そのバフを付けた技ID。記録した瞬間の実体からしか決まらない。決まらなければ 0。</summary>
    [ProtoMember(5)]
    public int BuffSourceSkillId { get; set; }

    /// <summary>回復した実体(仮想体など)を出した技ID。実体の出現時に決めて控えたもの。決まらなければ 0。</summary>
    [ProtoMember(6)]
    public int SummonSourceSkillId { get; set; }

    /// <summary>実際に増えた HP(<c>ActualValue</c>)。</summary>
    [ProtoMember(7)]
    public long Value { get; set; }

    /// <summary>回復が届いたパケットの到着時刻(UTC)。</summary>
    [ProtoMember(8)]
    public DateTime Timestamp { get; set; }

    /// <summary>被ダメと同じ通し番号。並べ直しに使う。</summary>
    [ProtoMember(9)]
    public long Sequence { get; set; }

    /// <summary>この回復を起こした詠唱(<see cref="SkillCastRecord.Sequence"/>)。詠唱から起きたのでなければ 0。決め方は <c>Encounter.FindLinkedCastSequence</c>。</summary>
    [ProtoMember(16)]
    public long CastSequence { get; set; }
}

/// <summary>
/// 敵から敵へのダメージ1件。加害者も対象もプレイヤー(とその召喚)でない、値が 0 でないダメージだけを残す(自分へのダメージ・自爆も入る)。
/// 敵からプレイヤーへのダメージは被ダメログ(識別子は被ダメリスト)が出すので残さない。画面では加害者の技の行の下のダメージの行になる。
/// </summary>
[ProtoContract]
public sealed class EnemyDamageRecord
{
    /// <summary>加害者。召喚体は被弾の加害者と同じく寄せる(<c>Encounter.ResolveTakenDamageLogActor</c>)。</summary>
    [ProtoMember(1)]
    public long AttackerUuid { get; set; }

    [ProtoMember(2)]
    public long TargetUuid { get; set; }

    /// <summary><c>SyncDamageInfo.OwnerId</c> の生の値。<see cref="DamageSource"/> によって技・弾・バフの番号になる。</summary>
    [ProtoMember(3)]
    public int OwnerId { get; set; }

    [ProtoMember(4)]
    public EDamageSource DamageSource { get; set; }

    /// <summary>バフ由来のとき、そのバフを付けた技ID。決まらなければ 0。</summary>
    [ProtoMember(5)]
    public int BuffSourceSkillId { get; set; }

    /// <summary>ダメージを出した実体(仮想体など)を出した技ID。決まらなければ 0。</summary>
    [ProtoMember(6)]
    public int SummonSourceSkillId { get; set; }

    /// <summary>ダメージの値(被弾の記録と同じ値)。</summary>
    [ProtoMember(7)]
    public long Value { get; set; }

    /// <summary>ダメージが届いたパケットの到着時刻(UTC)。</summary>
    [ProtoMember(8)]
    public DateTime Timestamp { get; set; }

    /// <summary>被ダメと同じ通し番号。並べ直しに使う。</summary>
    [ProtoMember(9)]
    public long Sequence { get; set; }

    /// <summary>このダメージを起こした詠唱(<see cref="SkillCastRecord.Sequence"/>)。詠唱から起きたのでなければ 0。</summary>
    [ProtoMember(10)]
    public long CastSequence { get; set; }
}

/// <summary>
/// 敵が付けたバフ1件(バフかデバフかも、相手が敵・自分・プレイヤーのどれかも問わない)。差分の付与(<c>BuffInfo</c>)だけを残し、
/// 層の変化・延長・除去と、周りに入ったときにすでに付いていたバフ(出現時の一覧)は残さない。
///
/// <para>
/// 付与元は通信の <c>FightSourceInfo</c> を、ほかの記録と同じ発生源の形(<see cref="OwnerId"/> と <see cref="DamageSource"/>)に置き換えて持つ:
/// 技 → <c>Skill</c> と技ID、弾 → <c>Bullet</c> と弾ID、親のバフ → <c>Buff</c> とそのバフID(<see cref="BuffSourceSkillId"/> は親のバフを付けた技)、
/// それ以外(タレント・シーンなど)と番号の無いものは <c>Other</c> と 0。
/// </para>
/// </summary>
[ProtoContract]
public sealed class EnemyBuffRecord
{
    /// <summary>付けた敵(<c>FireUuid</c>)。召喚体は被弾の加害者と同じく寄せる(<c>Encounter.ResolveTakenDamageLogActor</c>)。</summary>
    [ProtoMember(1)]
    public long CasterUuid { get; set; }

    [ProtoMember(2)]
    public long TargetUuid { get; set; }

    [ProtoMember(3)]
    public int BuffId { get; set; }

    /// <summary>通信の持続(ミリ秒)。0 以下は持続なし。</summary>
    [ProtoMember(4)]
    public int DurationMs { get; set; }

    [ProtoMember(5)]
    public int OwnerId { get; set; }

    [ProtoMember(6)]
    public EDamageSource DamageSource { get; set; }

    [ProtoMember(7)]
    public int BuffSourceSkillId { get; set; }

    /// <summary>付けた実体(仮想体など)を出した技ID。決まらなければ 0。</summary>
    [ProtoMember(8)]
    public int SummonSourceSkillId { get; set; }

    /// <summary>付与が届いたパケットの到着時刻(UTC)。</summary>
    [ProtoMember(9)]
    public DateTime Timestamp { get; set; }

    /// <summary>被ダメと同じ通し番号。並べ直しに使う。</summary>
    [ProtoMember(10)]
    public long Sequence { get; set; }

    /// <summary>このバフを付けた詠唱(<see cref="SkillCastRecord.Sequence"/>)。詠唱から付いたのでなければ 0。</summary>
    [ProtoMember(11)]
    public long CastSequence { get; set; }
}

public enum TakenDamageLogRecordKind
{
    Cast,
    Hit,
    Announcement,
    Death,

    /// <summary>予告のバーが終わった時刻の行(<see cref="SkillAnnouncementRecord.IsBarEnd"/>)。</summary>
    AnnouncementEnd,

    /// <summary>敵の回復(<see cref="EnemyHealRecord"/>)。</summary>
    EnemyHeal,

    /// <summary>敵から敵へのダメージ(<see cref="EnemyDamageRecord"/>)。</summary>
    EnemyDamage,

    /// <summary>敵が付けたバフ(<see cref="EnemyBuffRecord"/>)。</summary>
    EnemyBuff
}

/// <summary>
/// 被ダメログの1件。詠唱なら <see cref="EntityUuid"/> は詠唱した敵、被弾と死亡ならそのプレイヤー、敵の回復・ダメージ・バフなら相手。
/// 予告は実体を持たないので 0。タイムラインに出すのは予告・予告のバーの終わり・詠唱・敵の回復・ダメージ・バフで、
/// 被弾は被ダメリスト、被弾と死亡はプレイヤーリストの顔ぶれの判定が読む。
/// </summary>
public readonly record struct TakenDamageLogRecord(
    TakenDamageLogRecordKind Kind,
    long EntityUuid,
    SkillSnapshot? Hit,
    SkillCastRecord? Cast,
    SkillAnnouncementRecord? Announcement,
    PlayerDeathRecord? Death,
    EnemyHealRecord? EnemyHeal = null,
    EnemyDamageRecord? EnemyDamage = null,
    EnemyBuffRecord? EnemyBuff = null)
{
    public long Sequence => Kind switch
    {
        TakenDamageLogRecordKind.Hit => Hit!.Sequence,
        TakenDamageLogRecordKind.Cast => Cast!.Sequence,
        TakenDamageLogRecordKind.Death => Death!.Sequence,
        TakenDamageLogRecordKind.EnemyHeal => EnemyHeal!.Sequence,
        TakenDamageLogRecordKind.EnemyDamage => EnemyDamage!.Sequence,
        TakenDamageLogRecordKind.EnemyBuff => EnemyBuff!.Sequence,
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

    public static TakenDamageLogRecord ForEnemyHeal(EnemyHealRecord heal)
    {
        return new TakenDamageLogRecord(TakenDamageLogRecordKind.EnemyHeal, heal.TargetUuid, null, null, null, null, heal);
    }

    public static TakenDamageLogRecord ForEnemyDamage(EnemyDamageRecord damage)
    {
        return new TakenDamageLogRecord(TakenDamageLogRecordKind.EnemyDamage, damage.TargetUuid, null, null, null, null, EnemyDamage: damage);
    }

    public static TakenDamageLogRecord ForEnemyBuff(EnemyBuffRecord buff)
    {
        return new TakenDamageLogRecord(TakenDamageLogRecordKind.EnemyBuff, buff.TargetUuid, null, null, null, null, EnemyBuff: buff);
    }
}
