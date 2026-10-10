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
    double BarRatio,
    // 有効化しているシーズンタレントの型の根ノードのバフID(0 = 不明か無効)と、無効が確定しているか。
    int SeasonTalentBuffId = 0,
    bool IsSeasonTalentInactive = false);

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

/// <summary>
/// 推移の区切り1つ。<see cref="StartSeconds"/>〜<see cref="EndSeconds"/> は <c>FirstDamageTimeStamp</c> からの秒(右閉じ)、
/// <see cref="ValuePerSecond"/> はその区切りの合計 ÷ 区切りの長さ。
/// </summary>
public sealed record MetricTimelinePoint(double StartSeconds, double EndSeconds, double ValuePerSecond);

public sealed record MetricTimelineSnapshot(
    IReadOnlyList<MetricTimelinePoint> Points);

/// <summary>
/// 推移グラフの横軸の下に出す、プレイヤーの技の開始1件。<see cref="Seconds"/> は戦闘の時計の経過(<c>Encounter.ToCombatOffset</c>)で、
/// 起点より前の開始は 0、自動一時停止の中の開始は止まった時点の経過。<see cref="IconName"/> と <see cref="Name"/> は
/// メーターと同じ見出し表の行で畳んだもの(<c>CombatDataCatalog.GetSkillActivationDisplay</c>、内部IDの注記は設定どおり。名前が無ければ空)。
/// </summary>
public sealed record MetricTimelineSkillActivation(double Seconds, int SkillId, string IconName, string Name);

/// <summary>
/// スキル詳細の1行。
///
/// <para>
/// <see cref="ValueByElement"/> と <see cref="ValueByMode"/> は、この行の累計の内訳。
/// 足す条件は <see cref="TotalValue"/> と同じ(<c>Immune</c> と <c>Miss</c> は入らない)ので、
/// それぞれの合計は <see cref="TotalValue"/> に一致する。<b>並びは enum の値の順</b>で、
/// 表示の都合で並べ替えるのは読む側。<b>値が入った種類だけ</b>が載る。
/// </para>
///
/// <para>
/// 内訳を溜め始める前に保存した履歴では、どちらも空になる。
/// </para>
/// </summary>
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
    double Percentage,
    IReadOnlyList<KeyValuePair<EDamageProperty, ulong>> ValueByElement,
    IReadOnlyList<KeyValuePair<EDamageMode, ulong>> ValueByMode);

public sealed record MetricSkillTableSnapshot(
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

/// <summary>
/// ロスターを通らない表示(ウィンドウのタイトルなど)が使う相手の素性。名前の規則(NPC は職業名)を当てるので NPC の印と職業も持つ。
/// <see cref="ClassSpec"/> はクラスカラーの鍵を決めるため(変身中か。求め方はメーターの行と同じ)。
/// </summary>
public sealed record MeterPlayerIdentity(string Name, long UserId, bool IsNpc, int ProfessionId, PlayerClassSpec ClassSpec);

/// <summary>被ダメログの登場人物1人。プレイヤーなら名前は伏せ字にする前の生の名前。<see cref="ClassSpec"/> はプレイヤーのときだけ意味を持つ。</summary>
/// <param name="IsUnnamedEnemy">
/// 名前の無いモンスターで、ゲーム内で HP バーが見えるもの。表示側が「敵」と出す(名前は内部ID注記だけか空)。
/// </param>
/// <param name="IsUnknownEnemy">
/// プレイヤーでも仮想体でもなく、実体か種別ID(AttrId)が無くて何者か分からない加害者。表示側が「未知の敵」と出す(名前は空)。
/// AttrId は実体が現れたときの通知でしか届かないので、アプリの起動前から居た敵がこうなる。
/// </param>
public sealed record TakenDamageLogParty(long Uuid, long CharacterId, string Name, bool IsPlayer, bool IsSelf, bool IsNpc, int ProfessionId, PlayerClassSpec ClassSpec, bool IsSystem = false, bool IsUnnamedEnemy = false, bool IsUnknownEnemy = false);

/// <summary>
/// 被ダメログの1件(予告か詠唱か被弾か、ダメージの無い死亡)。
///
/// <para>
/// <see cref="Timestamp"/> はパケットの到着時刻(UTC。予告のバーの終わりはバーが消える時刻)。経過は持たず、表示側が
/// <see cref="Encounter.ToCombatOffset"/> で戦闘の経過(メーターの経過と同じ。自動一時停止で止まっていた区間を除く)に直す。
/// 起点が立つまでは直せない(<see cref="TakenDamageLogSnapshot.CombatStartUtc"/>)。同じ到着時刻の被弾は画面で1つの技の下にまとめる。
/// </para>
///
/// <para>
/// <see cref="Attacker"/> は予告を構えた敵(名前だけで UUID は 0)、詠唱した敵か、ダメージを与えた敵。<see cref="Target"/> と HP は被弾のときだけ入る
/// (HP は記録時点で属性を持っていなければ入らない)。
/// <see cref="SourceName"/> は内部ID注記を付けない名前で、名前が無ければ空。
/// <see cref="SourceId"/> は届いた <c>OwnerId</c> そのもの。<see cref="IsBuffSource"/> が真ならバフID、
/// 偽なら技IDか弾ID(弾のときの <see cref="SourceName"/> は親の技の名前)。
/// </para>
///
/// <para>
/// <see cref="DamageElement"/> は被弾の属性。予告・詠唱と、属性を保存していなかった頃の被弾は <c>null</c>。
/// </para>
///
/// <para>
/// 死亡は <see cref="Attacker"/> がシステム、<see cref="Target"/> が死亡したプレイヤー、HP は 0 と死亡時の最大HP。
/// <see cref="IsFall"/> は落下の被弾(技名は画面側の文言)。
/// </para>
///
/// <para>
/// <see cref="IsLethal"/> は「この被弾で死んだ」。表示は被弾の行に戦闘不能の語を付け、その時刻のまとめの行を戦闘不能にする。
/// </para>
///
/// <para>
/// <see cref="DamageMode"/> は物理と魔法の別。属性(<see cref="DamageElement"/>)とは独立していて、
/// <c>DamageNormal</c> はどちらでもない。予告・詠唱・死亡は <c>DamageNormal</c>。
/// </para>
///
/// <para>
/// <see cref="TargetHpBefore"/> / <see cref="TargetMaxHpBefore"/> / <see cref="TargetShieldBefore"/> は、その同期を当てる前の対象の値
/// (被弾は <see cref="TargetHp"/> を持つものだけ、死亡は死亡を伝えた同期の前)。持っていない記録(この項目を足す前の記録も)は null。
/// </para>
/// </summary>
public sealed record TakenDamageLogLine(
    TakenDamageLogRecordKind Kind,
    long Sequence,
    DateTime Timestamp,
    TakenDamageLogParty Attacker,
    TakenDamageLogParty? Target,
    int SourceId,
    bool IsBuffSource,
    string SourceName,
    long Value,
    long? TargetHp,
    long? TargetMaxHp,
    long? TargetShield,
    EDamageProperty? DamageElement,
    bool IsFall = false,
    bool IsLethal = false,
    EDamageMode DamageMode = EDamageMode.DamageNormal,
    long? TargetHpBefore = null,
    long? TargetMaxHpBefore = null,
    long? TargetShieldBefore = null);

/// <summary>
/// <see cref="MeterSnapshotProvider.GetTakenDamageLog"/> の結果。
/// <see cref="Encounter"/> が前回と違えば、呼び出し側は持っている行を捨てて読み直す。
/// </summary>
/// <param name="CombatStartUtc">
/// 読んだ時点の戦闘の時計の起点(メーターの経過と同じ)。まだ無ければ null。起点の前の行は負の経過になる。
/// 同じエンカウンターでも後から立つので、呼び出し側は変わったら持っている行の時刻を作り直す。
/// </param>
public sealed record TakenDamageLogSnapshot(
    Encounter? Encounter,
    int NextIndex,
    IReadOnlyList<TakenDamageLogLine> Lines,
    DateTime? CombatStartUtc);

public static class MeterSnapshotProvider
{
    /// <summary>
    /// 蘇生不可デバフ(<c>虚弱·祈愿禁止</c> / <c>Weakened: Wish Sealed</c>)。
    ///
    /// <para>
    /// 説明文が「この間は 奥義！ライフブレス と 復活の祈り を再度かけられない」。
    /// <b>持続時間はパケットの <c>Duration</c> からしか分からない</b> —
    /// テーブルの <c>DestroyParam</c> は <c>[[0,0]]</c>。
    /// </para>
    ///
    /// <para>
    /// 復活系スキルはどれもこの同じ BaseId を出すので、発生元ごとに分ける必要はない。
    /// 同時に届く <c>2110032</c> / <c>2110093</c> / <c>2100412</c> は
    /// いずれも自前アイコンが無く表示経路で落ちる。
    /// </para>
    /// </summary>
    private const int ReviveBlockDebuffBaseId = 2110057;

    // 推移グラフの記録が時計の終点より後ろにあったのを、どの回について残したか(回ごとに1回だけ)。
    private static Encounter? _timelineEndErrorEncounter;

    public static MeterSnapshot GetSnapshot(
        MeterSnapshotKind kind,
        PartyDisplayMode partyDisplayMode = PartyDisplayMode.All)
    {
        var encounter = ResolveActiveEncounter();
        if (encounter is null)
        {
            return new MeterSnapshot(kind, TimeSpan.Zero, 0, 0, Array.Empty<MeterPlayerSnapshot>());
        }

        // 経過・行の秒間値・フッターの合計を同じ終点で出すため、時計は1回だけ読む。
        var clock = encounter.ReadCombatClock();
        var source = encounter.Entities
            .Where(pair => pair.Value.EntityType == EEntityType.EntChar)
            .Select(pair => CreatePlayerValue(pair.Key, pair.Value, kind, clock))
            .Where(player => player.TotalValue > 0UL)
            .OrderByDescending(player => player.TotalValue)
            .ThenBy(player => player.Name, StringComparer.Ordinal)
            .ThenBy(player => player.CharacterId)
            .ToArray();

        UpdatePlayerMeterState(source);

        // 履歴表示中はフィルターを掛けない。判定に使えるのは今のパーティ状態だけで、
        // 履歴当時の所属は保存していない。
        var effectivePartyDisplayMode = AppState.OpenedHistoricalEncounter is null
            ? partyDisplayMode
            : PartyDisplayMode.All;
        var party = PartyStateStore.Instance.Current;
        var visibleSource = source
            .Where(player => party.ShouldInclude(player.UserId, player.IsSelf, effectivePartyDisplayMode))
            .ToArray();
        var totalValue = effectivePartyDisplayMode == PartyDisplayMode.All
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
            clock.Elapsed,
            totalValue,
            players.Sum(player => player.ValuePerSecond),
            players);
    }

    /// <summary>
    /// 被ダメログを <paramref name="startIndex"/> 件目から返す。表示中のエンカウンターは
    /// メーターと同じく <see cref="ResolveActiveEncounter"/> で決まるので、履歴表示にも追従する。
    ///
    /// <para>
    /// 表示中のエンカウンターが <paramref name="knownEncounter"/> と違えば先頭から返す。
    /// 名前はこの呼び出しの時点の言語と内部ID表示で引く。
    /// </para>
    /// </summary>
    public static TakenDamageLogSnapshot GetTakenDamageLog(Encounter? knownEncounter, int startIndex)
    {
        return BuildTakenDamageLog(ResolveActiveEncounter(), knownEncounter, startIndex);
    }

    /// <summary>技の名前。ボス大技の予告(<c>DbmTable</c>)の正式名を先に引き、無ければ <c>SkillNames</c>。被ダメログの予告の行・詠唱の行と同じ。</summary>
    public static string GetTakenDamageLogSkillName(int skillId)
    {
        return ResolveTakenDamageSkillName(skillId);
    }

    /// <summary>予告の主(予告が届いた時点で周囲にいる、その技を持つモンスターの種別ID。0 なら名前なし)。被ダメログの予告の行の加害者。</summary>
    public static TakenDamageLogParty CreateTakenDamageLogAnnouncementParty(int ownerMonsterId)
    {
        return new TakenDamageLogParty(0, 0, CombatDataCatalog.GetMonsterName(ownerMonsterId), false, false, false, 0, PlayerClassSpec.Unknown);
    }

    private static TakenDamageLogSnapshot BuildTakenDamageLog(Encounter? encounter, Encounter? knownEncounter, int startIndex)
    {
        if (encounter is null)
        {
            return new TakenDamageLogSnapshot(null, 0, Array.Empty<TakenDamageLogLine>(), null);
        }

        if (!ReferenceEquals(encounter, knownEncounter))
        {
            startIndex = 0;
        }

        var combatStartUtc = encounter.ExData.FirstDamageTimeStamp;
        var records = encounter.GetTakenDamageLogRecords(startIndex);
        var parties = new Dictionary<long, TakenDamageLogParty>();
        var lines = new TakenDamageLogLine[records.Length];

        for (var index = 0; index < records.Length; index++)
        {
            var record = records[index];
            if (record.Kind is TakenDamageLogRecordKind.Announcement or TakenDamageLogRecordKind.AnnouncementEnd)
            {
                var announcement = record.Announcement!;
                lines[index] = new TakenDamageLogLine(
                    record.Kind,
                    announcement.Sequence,
                    announcement.Timestamp,
                    CreateTakenDamageLogAnnouncementParty(announcement.OwnerMonsterId),
                    null,
                    announcement.SkillId,
                    false,
                    ResolveTakenDamageSkillName(announcement.SkillId),
                    0,
                    null,
                    null,
                    null,
                    null);
                continue;
            }

            if (record.Kind == TakenDamageLogRecordKind.Cast)
            {
                var cast = record.Cast!;
                lines[index] = new TakenDamageLogLine(
                    record.Kind,
                    cast.Sequence,
                    cast.Timestamp,
                    ResolveTakenDamageLogParty(encounter, record.EntityUuid, parties),
                    null,
                    cast.SkillId,
                    false,
                    ResolveTakenDamageSkillName(cast.SkillId),
                    0,
                    null,
                    null,
                    null,
                    null);
                continue;
            }

            if (record.Kind == TakenDamageLogRecordKind.Death)
            {
                var death = record.Death!;
                lines[index] = new TakenDamageLogLine(
                    record.Kind,
                    death.Sequence,
                    death.Timestamp,
                    new TakenDamageLogParty(0, 0, string.Empty, false, false, false, 0, PlayerClassSpec.Unknown, IsSystem: true),
                    ResolveTakenDamageLogParty(encounter, death.PlayerUuid, parties),
                    0,
                    false,
                    string.Empty,
                    0,
                    0,
                    death.MaxHp,
                    null,
                    null,
                    TargetHpBefore: death.HpBefore,
                    TargetMaxHpBefore: death.MaxHpBefore,
                    TargetShieldBefore: death.ShieldBefore);
                continue;
            }

            var snapshot = record.Hit!;
            var isBuffSource = snapshot.DamageSource == Zproto.EDamageSource.Buff;
            var attacker = ResolveTakenDamageLogParty(encounter, snapshot.OtherUUID, parties);
            lines[index] = new TakenDamageLogLine(
                record.Kind,
                snapshot.Sequence,
                snapshot.Timestamp!.Value,
                attacker,
                ResolveTakenDamageLogParty(encounter, record.EntityUuid, parties),
                snapshot.OwnerId,
                isBuffSource,
                ResolveTakenDamageSourceName(attacker.IsPlayer, snapshot.Id, snapshot.DamageSource, snapshot.OwnerId, snapshot.BuffSourceSkillId, snapshot.SummonSourceSkillId),
                snapshot.Value,
                snapshot.TargetHp,
                snapshot.TargetMaxHp,
                snapshot.TargetShield,
                snapshot.DamageElement,
                snapshot.DamageSource == Zproto.EDamageSource.Fall,
                snapshot.IsKill,
                snapshot.DamageMode,
                snapshot.TargetHpBefore,
                snapshot.TargetMaxHpBefore,
                snapshot.TargetShieldBefore);
        }

        return new TakenDamageLogSnapshot(encounter, startIndex + records.Length, lines, combatStartUtc);
    }

    /// <summary>
    /// 被ダメの発生源の名前。<c>OwnerId</c> の中身は <c>DamageSource</c> で変わる。
    ///
    /// <para>
    /// <b>加害者がプレイヤーなら、先にメーターの見出し表(手修正込み)を <paramref name="meterKey"/> で引く。</b>
    /// プレイヤーの技の派生(イマジンのパッシブ・幸運の一撃など)はバフ名が空で、名前は見出し表にしか無い。
    /// 見出し表が空か、鍵を保存していなかった頃の記録(0)なら下の引き方に戻る。
    /// </para>
    ///
    /// <list type="bullet">
    ///   <item>バフ — バフID。記録時に付与元の技(<paramref name="buffSourceSkillId"/>)が決まっていれば技として引き、
    ///   無いか名前が空なら <c>BuffNames</c> で引く</item>
    ///   <item>弾(<c>Bullet</c> / <c>FakeBullet</c>) — <c>BulletTable</c> の番号。親の技へ辿って技として引き、辿れなければ空</item>
    ///   <item>それ以外 — 技ID</item>
    /// </list>
    ///
    /// <para>
    /// ここまでで名前が空なら、ダメージを出した実体(仮想体など)を出した技(<paramref name="summonSourceSkillId"/>)の名前にする。
    /// ボスの予告の技は仮想体を出すだけで、ダメージは名前の無い仮想体の技で届くことが多い。
    /// </para>
    ///
    /// <para>
    /// 弾の番号を <c>SkillNames</c> でそのまま引くと、同じ番号の無関係な技の名前が出る
    /// (<c>3920</c> は弾 普攻假子弹 / 技 奥義！ライフブレス)。
    /// </para>
    /// </summary>
    private static string ResolveTakenDamageSourceName(bool isPlayerAttacker, long meterKey, Zproto.EDamageSource damageSource, int ownerId, int buffSourceSkillId, int summonSourceSkillId)
    {
        if (isPlayerAttacker && meterKey != 0)
        {
            var meterName = CombatDataCatalog.GetSourceName(meterKey);
            if (!string.IsNullOrEmpty(meterName))
            {
                return meterName;
            }
        }

        var name = damageSource switch
        {
            Zproto.EDamageSource.Buff => ResolveTakenDamageBuffName(ownerId, buffSourceSkillId),
            Zproto.EDamageSource.Bullet or Zproto.EDamageSource.FakeBullet =>
                CombatDataCatalog.TryResolveBulletParentSkillId(ownerId, out var parentSkillId)
                    ? ResolveTakenDamageSkillName(parentSkillId)
                    : string.Empty,
            _ => ResolveTakenDamageSkillName(ownerId)
        };

        // 当たった技そのものに名前が無いときだけ、ダメージを出した実体(仮想体など)を出した技の名前にする。
        return string.IsNullOrEmpty(name) && summonSourceSkillId > 0
            ? ResolveTakenDamageSkillName(summonSourceSkillId)
            : name;
    }

    private static string ResolveTakenDamageBuffName(int buffId, int buffSourceSkillId)
    {
        var sourceSkillName = buffSourceSkillId > 0
            ? ResolveTakenDamageSkillName(buffSourceSkillId)
            : string.Empty;
        return string.IsNullOrEmpty(sourceSkillName)
            ? CombatDataCatalog.GetBuffNameWithoutInternalId(buffId)
            : sourceSkillName;
    }

    /// <summary>技の名前。ボス大技の予告(<c>DbmTable</c>)の正式名を先に引き、無ければ <c>SkillNames</c>。</summary>
    private static string ResolveTakenDamageSkillName(int skillId)
    {
        var dbmName = CombatDataCatalog.GetDbmNameWithoutInternalId(skillId);
        return string.IsNullOrEmpty(dbmName)
            ? CombatDataCatalog.GetSkillNameWithoutInternalId(skillId)
            : dbmName;
    }

    /// <summary>被ダメログの登場人物1人を、<paramref name="encounter"/> の実体から決める(被ダメログの行と同じ決め方)。</summary>
    internal static TakenDamageLogParty CreateTakenDamageLogParty(Encounter encounter, long uuid)
    {
        return ResolveTakenDamageLogParty(encounter, uuid, []);
    }

    private static TakenDamageLogParty ResolveTakenDamageLogParty(
        Encounter encounter,
        long uuid,
        Dictionary<long, TakenDamageLogParty> cache)
    {
        if (cache.TryGetValue(uuid, out var cached))
        {
            return cached;
        }

        var isPlayer = Utils.UuidToEntityType(uuid) == (long)EEntityType.EntChar;
        TakenDamageLogParty party;
        if (!encounter.Entities.TryGetValue(uuid, out var entity))
        {
            var isUnknownEnemy = !isPlayer && (EEntityType)Utils.UuidToEntityType(uuid) != EEntityType.EntDummy;
            party = new TakenDamageLogParty(uuid, isPlayer ? Utils.UuidToEntityId(uuid) : 0, string.Empty, isPlayer, IsSelfEntity(uuid), false, 0, PlayerClassSpec.Unknown, IsUnknownEnemy: isUnknownEnemy);
        }
        else if (isPlayer)
        {
            var isSelf = IsSelf(entity);
            var source = PlayerDataSourceResolver.Resolve(entity, isSelf);
            // NPC は名前ではなく職業名を出す(プレイヤーリストと同じ規則)。判定と職業IDは表示側が使う。
            party = new TakenDamageLogParty(
                uuid,
                source.CharacterId,
                source.Name,
                true,
                isSelf,
                source.IsNpc,
                source.ProfessionId,
                PlayerClassSpecResolver.Resolve(
                    source.ProfessionId,
                    source.SubProfessionId,
                    source.IsSpecAbilityUnequipped,
                    PlayerClassSpecResolver.HasMeanTransformBuff(uuid),
                    PlayerClassSpecResolver.HasGolemTransformBuff(uuid)));
        }
        else
        {
            // 名前はモンスターの実体だけ、種別ID(AttrId)から表示言語で引く。
            // 同じ番号が仮想体や NPC では別のものを指すので、モンスター以外と属性が無いものは名前を空のままにする。
            // 仮想体が加害者として残るのは親(召喚者)を特定できなかったときだけなので(Encounter.ResolveTakenDamageLogActor)、
            // 仕掛けそのものを指す名前を出す。
            // 名前の無いモンスターでも、ゲーム内で HP バーが見えるものは「敵」と出す(エンティティリストと同じ条件)。
            // 表に行が無い番号には付けない(表が古いことを、名前の無い雑魚と見分けるため)。
            // AttrId が届いていない加害者は何者か分からないので「未知の敵」と出す。
            var attrId = entity.GetAttrKV("AttrId");
            var entityType = (EEntityType)Utils.UuidToEntityType(uuid);
            var isSystem = entityType == EEntityType.EntDummy;
            var isUnknownEnemy = !isSystem && attrId is null;
            var monsterId = attrId is null || entityType != EEntityType.EntMonster ? 0 : Convert.ToInt64(attrId);
            var name = monsterId == 0
                ? string.Empty
                : CombatDataCatalog.GetMonsterName(monsterId);
            var isUnnamedEnemy = monsterId > 0
                && CombatDataCatalog.HasEntityRow(EEntityType.EntMonster, monsterId)
                && !CombatDataCatalog.HasEntityName(EEntityType.EntMonster, monsterId)
                && CombatDataCatalog.HasMonsterHpBar(monsterId);
            party = new TakenDamageLogParty(uuid, 0, name, false, false, false, 0, PlayerClassSpec.Unknown, isSystem, isUnnamedEnemy, isUnknownEnemy);
        }

        cache[uuid] = party;
        return party;
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
        return new MeterPlayerIdentity(
            source.Name,
            source.CharacterId,
            source.IsNpc,
            source.ProfessionId,
            PlayerClassSpecResolver.Resolve(
                source.ProfessionId,
                source.SubProfessionId,
                source.IsSpecAbilityUnequipped,
                PlayerClassSpecResolver.HasMeanTransformBuff(entity.UUID),
                PlayerClassSpecResolver.HasGolemTransformBuff(entity.UUID)));
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
            // 自分の枠はアクションバーから組むので、アクションバーの差し替えでも組み直す。
            skillSourceToken = PlayerSkillLevelStateStore.SelfActionBarToken;
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

        var imagineSkills = new List<PlayerCooldownSkillSnapshot?>(2);
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

        // 一覧に入るイマジンは装備中のものだけなので、一覧が届いていれば見つからない枠は未装着。
        // null で埋めて2枠にし、一覧が届いていない(不明)の0枠と分ける。自分のアクションバーの空枠と同じ形。
        if (skillLevels.Count > 0)
        {
            while (imagineSkills.Count < 2)
            {
                imagineSkills.Add(null);
            }
        }

        return new PlayerImagineRoleSkillLoadoutSnapshot(
            entityUuid,
            imagineSkills,
            roleSkills,
            isSelf ? SelfRoleSlotCount : roleSkills.Count);
    }

    /// <summary>イマジンの枠番号 7 / 8(左 / 右)。</summary>
    private static readonly int[] SelfImagineSlotIds = [7, 8];

    /// <summary>ロールスキルの枠番号 21〜24。</summary>
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
        var resolvedTrackedSkillIdsBySource = new Dictionary<Services.BuffSource, int>();
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

            var source = new Services.BuffSource(buffEvent.FightSourceType, buffEvent.SourceConfigId);
            if (!resolvedTrackedSkillIdsBySource.TryGetValue(source, out var trackedSkillId))
            {
                trackedSkillId = ResolveTrackedSourceSkillId(
                    source,
                    trackedSkillIds,
                    runtimeSourceParentsByBaseId);
                resolvedTrackedSkillIdsBySource[source] = trackedSkillId;
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
            // 持続なし・経過不明は、残り時間を持つバフに譲る。同点なら先に並ぶものが残る。
            var badgeOrderingKey = effectiveRemoveTime == TimeSpan.MaxValue ? TimeSpan.Zero : effectiveRemoveTime;
            var candidate = new PlayerBuffCandidate(snapshot, badgeOrderingKey);

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

    /// <summary>
    /// バフの付与元から、装備中のスキル(イマジン・ロール)を辿る。
    ///
    /// <para>
    /// <b>付与元は種類と番号の組で扱う。</b>スキルの表とバフの表は番号が重なるので、番号だけで引くと
    /// バフの番号をスキルとして、スキルの番号をバフとして辿ってしまう。
    /// スキルの表(装備中との一致・<c>SkillLevelGroup</c> などのつながり・同じイマジンアイコン)を引くのは技のときだけ、
    /// 実行時の親の対応を辿るのはバフのときだけ。弾・タレントなど他の種類はそこで止め、帰属させない。
    /// </para>
    /// </summary>
    private static int ResolveTrackedSourceSkillId(
        Services.BuffSource source,
        IReadOnlySet<int> trackedSkillIds,
        IReadOnlyDictionary<int, HashSet<Services.BuffSource>> runtimeSourceParentsByBaseId)
    {
        if (source.SourceConfigId <= 0 || !IsTraceableSourceType(source.FightSourceType))
        {
            return 0;
        }

        if (source.FightSourceType == (int)EFightSource.Skill)
        {
            var directTrackedSkillId = ResolveTrackedSourceSkillDirectly(
                source.SourceConfigId,
                trackedSkillIds);
            if (directTrackedSkillId > 0)
            {
                return directTrackedSkillId;
            }
        }

        var pendingSources = new Queue<Services.BuffSource>();
        var visitedSources = new HashSet<Services.BuffSource>();
        var resolvedTrackedSkillIds = new HashSet<int>();
        pendingSources.Enqueue(source);

        while (pendingSources.Count > 0)
        {
            var current = pendingSources.Dequeue();
            if (!visitedSources.Add(current))
            {
                continue;
            }

            if (current.FightSourceType == (int)EFightSource.Skill)
            {
                if (current != source)
                {
                    var resolvedTrackedSkillId = ResolveTrackedSourceSkillDirectly(
                        current.SourceConfigId,
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
                        current.SourceConfigId.ToString(),
                        out var sourceSkill))
                {
                    EnqueueLinkedSkill(pendingSources, sourceSkill.SkillLevelGroup, current.SourceConfigId);
                    EnqueueLinkedSkill(pendingSources, sourceSkill.SwitchSkillId, current.SourceConfigId);
                    EnqueueLinkedSkill(pendingSources, sourceSkill.NextSkillId, current.SourceConfigId);
                }

                continue;
            }

            if (runtimeSourceParentsByBaseId.TryGetValue(
                    current.SourceConfigId,
                    out var runtimeParents))
            {
                foreach (var runtimeParent in runtimeParents)
                {
                    if (runtimeParent.SourceConfigId > 0
                        && IsTraceableSourceType(runtimeParent.FightSourceType)
                        && runtimeParent != current)
                    {
                        pendingSources.Enqueue(runtimeParent);
                    }
                }
            }
        }

        return resolvedTrackedSkillIds.Count == 1
            ? resolvedTrackedSkillIds.First()
            : 0;
    }

    private static bool IsTraceableSourceType(int fightSourceType)
    {
        return fightSourceType == (int)EFightSource.Skill
            || fightSourceType == (int)EFightSource.Buff;
    }

    private static void EnqueueLinkedSkill(
        Queue<Services.BuffSource> sources,
        int linkedSkillId,
        int currentSkillId)
    {
        if (linkedSkillId > 0 && linkedSkillId != currentSkillId)
        {
            sources.Enqueue(new Services.BuffSource((int)EFightSource.Skill, linkedSkillId));
        }
    }

    /// <summary>
    /// 召喚エンティティが付けたバフを、召喚元のスキルへ帰属させる。
    ///
    /// <para>
    /// 召喚を伴うイマジンのバフは <c>SourceConfigId</c> が<b>バフID</b>を指すので、
    /// <c>SkillLevelGroup</c> 等の既存の鎖はそこで途切れる(<c>BuffTable.SkillId</c> も 0)。
    /// </para>
    ///
    /// <para>
    /// 繋がるのは召喚体側のテーブル。<c>MonsterTable.BornSkillId</c> が
    /// <c>SkillTable.EffectIDs</c> に入っているエフェクトIDなので、そこを逆引きする。
    /// <b><c>エフェクトID / 100</c> の規則には頼らない。</b>
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

    private static Dictionary<int, HashSet<Services.BuffSource>> BuildRuntimeSourceParentsByBaseId(
        Entity entity,
        IReadOnlyCollection<BuffEvent> buffEvents)
    {
        var result = new Dictionary<int, HashSet<Services.BuffSource>>();

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
        Dictionary<int, HashSet<Services.BuffSource>> runtimeSourceParentsByBaseId,
        BuffEvent buffEvent)
    {
        if (buffEvent.BaseId <= 0
            || buffEvent.SourceConfigId <= 0
            || (buffEvent.FightSourceType == (int)EFightSource.Buff && buffEvent.BaseId == buffEvent.SourceConfigId))
        {
            return;
        }

        if (!runtimeSourceParentsByBaseId.TryGetValue(buffEvent.BaseId, out var sources))
        {
            sources = new HashSet<Services.BuffSource>();
            runtimeSourceParentsByBaseId.Add(buffEvent.BaseId, sources);
        }

        sources.Add(new Services.BuffSource(buffEvent.FightSourceType, buffEvent.SourceConfigId));
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

    /// <summary>
    /// 推移を <paramref name="aggregationIntervalSeconds"/> 秒ごとの重ならない区切りで返す。
    ///
    /// <para>
    /// 区切り j は (jN, (j+1)N] 秒で、値は中の1秒の合計の和 ÷ N。1件のダメージは1つの区切りにだけ入る。
    /// ライブでは満ちていない最後の区切りを出さない。戦闘が閉じているときだけ、最後の半端な区切りを
    /// 実際の長さ(1秒未満の端数を含む)で割って出す。選べる N は App の一覧が決め、ここは1秒以上なら受ける。
    /// </para>
    /// </summary>
    public static MetricTimelineSnapshot GetPlayerTimeline(
        MeterSnapshotKind kind,
        long characterId,
        int aggregationIntervalSeconds)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(aggregationIntervalSeconds, 1);
        var intervalSeconds = aggregationIntervalSeconds;
        var encounter = ResolveActiveEncounter();
        if (encounter is null
            || !TryResolvePlayerEntity(encounter, characterId, out _, out var entity))
        {
            return new MetricTimelineSnapshot(Array.Empty<MetricTimelinePoint>());
        }

        var stats = kind == MeterSnapshotKind.Damage
            ? entity.DamageStats
            : entity.HealingStats;
        var totals = stats.GetPerSecondTotalsCopy(out var lastTimestamp);

        if (totals.Length == 0 || lastTimestamp is not { } lastValueTime)
        {
            return new MetricTimelineSnapshot(Array.Empty<MetricTimelinePoint>());
        }

        // 秒の区切りは記録時に戦闘の時計の起点から切ってある。起点が無いのに合計があるなら記録側が壊れている。
        var clock = encounter.ReadCombatClock();
        var endTime = clock.EndUtc
            ?? throw new InvalidOperationException(
                $"Per-second totals exist without FirstDamageTimeStamp (encounter={encounter.EncounterId}).");

        // 記録は時計の終点より前にしか入らないはず(ライブは今、閉じた回は閉じた時刻、計測は窓の終わり)。
        // 後ろにあれば時刻の出所が食い違っているので、伸ばさずに終点まで描き、回ごとに1回だけ残す。
        if (lastValueTime > endTime && !ReferenceEquals(_timelineEndErrorEncounter, encounter))
        {
            _timelineEndErrorEncounter = encounter;
            Log.Error("Timeline value at {LastValue:o} is after the combat clock end {End:o} (encounter={EncounterId}); values after the end are not drawn",
                lastValueTime, endTime, encounter.EncounterId);
        }

        var elapsedSeconds = clock.Elapsed.TotalSeconds;
        var fullBucketCount = (int)Math.Floor(elapsedSeconds / intervalSeconds);
        var partialBucketSeconds = elapsedSeconds - fullBucketCount * (double)intervalSeconds;
        var hasPartialBucket = clock.IsClosed && partialBucketSeconds > 0d;
        var bucketCount = fullBucketCount + (hasPartialBucket ? 1 : 0);
        if (bucketCount == 0)
        {
            return new MetricTimelineSnapshot(Array.Empty<MetricTimelinePoint>());
        }

        // 1秒の鍵 k は (k, k+1] 秒なので、区切り j に入るのは k / N == j の鍵。
        var bucketTotals = new double[bucketCount];
        foreach (var (second, value) in totals)
        {
            var bucket = second / intervalSeconds;
            if (bucket < bucketCount)
            {
                bucketTotals[bucket] += value;
            }
        }

        var points = new MetricTimelinePoint[bucketCount];
        for (var bucket = 0; bucket < fullBucketCount; bucket++)
        {
            points[bucket] = new MetricTimelinePoint(
                bucket * (double)intervalSeconds,
                (bucket + 1) * (double)intervalSeconds,
                bucketTotals[bucket] / intervalSeconds);
        }

        if (hasPartialBucket)
        {
            points[fullBucketCount] = new MetricTimelinePoint(
                fullBucketCount * (double)intervalSeconds,
                elapsedSeconds,
                bucketTotals[fullBucketCount] / partialBucketSeconds);
        }

        return new MetricTimelineSnapshot(points);
    }

    /// <summary>
    /// 推移グラフに出すプレイヤーの技の開始(届いた順)。回は線と同じ(<see cref="ResolveActiveEncounter"/>)。
    /// 戦闘の時計の起点がまだ無い回は空(線も無い)。起点より前の開始は 0 秒に置く
    /// (時計は戦闘していない時間を1点に縮める。自動一時停止の中の開始が止まった時点に重なるのと同じ扱い)。
    /// </summary>
    public static IReadOnlyList<MetricTimelineSkillActivation> GetPlayerSkillActivations(long characterId)
    {
        var encounter = ResolveActiveEncounter();
        if (encounter is null
            || !TryResolvePlayerEntity(encounter, characterId, out var entityUuid, out _))
        {
            return Array.Empty<MetricTimelineSkillActivation>();
        }

        var records = encounter.GetSkillActivationsCopy(entityUuid);
        var activations = new List<MetricTimelineSkillActivation>(records.Length);
        foreach (var record in records)
        {
            if (encounter.ToCombatOffset(record.Timestamp) is not { } offset)
            {
                return Array.Empty<MetricTimelineSkillActivation>();
            }

            var (name, iconName) = CombatDataCatalog.GetSkillActivationDisplay(record.SkillId);
            activations.Add(new MetricTimelineSkillActivation(
                Math.Max(offset.TotalSeconds, 0d),
                record.SkillId,
                iconName,
                name));
        }

        return activations;
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

        // 行動の間は戦闘の経過で測る(自動一時停止で止まっていた区間を除く)。
        if (entity.FirstCombatActionTime is { } firstAction
            && entity.LastCombatActionTime is { } lastAction
            && encounter.ToCombatOffset(firstAction) is { } firstOffset
            && encounter.ToCombatOffset(lastAction) is { } lastOffset)
        {
            var activeDuration = lastOffset - firstOffset;
            if (activeDuration.TotalSeconds > 0d)
            {
                var totalCasts = (double)entity.TotalCasts;
                castsPerSecond = Math.Round(totalCasts / activeDuration.TotalSeconds, 2);
                castsPerMinute = Math.Round(totalCasts / activeDuration.TotalMinutes, 2);
            }
        }

        var clock = encounter.ReadCombatClock();
        return new PlayerMetricSummarySnapshot(
            stats.ValueTotal,
            CombatClockReading.PerSecond(stats.ValueTotal, entity.GetActiveSeconds(clock, encounter.ToCombatOffset)),
            CombatClockReading.PerSecond(stats.ValueTotal, clock.Elapsed.TotalSeconds),
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
            return new MetricSkillTableSnapshot(Array.Empty<MetricSkillTableRowSnapshot>());
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
            return new MetricSkillTableSnapshot(Array.Empty<MetricSkillTableRowSnapshot>());
        }

        // 有効は、その人の有効な秒数で割る(行ごとに変わらない)。
        var clock = encounter.ReadCombatClock();
        var elapsedSeconds = clock.Elapsed.TotalSeconds;
        var activeSeconds = entity.GetActiveSeconds(clock, encounter.ToCombatOffset);
        var rows = new MetricSkillTableRowSnapshot[skillStats.Count];
        for (var index = 0; index < skillStats.Count; index++)
        {
            var stat = skillStats[index];
            var value = stat.Value;
            // 小数点以下2位まで持つ。表示側は F2 で固定2桁にする。
            var percentage = value.ValueTotal > 0UL
                ? Math.Round(((double)value.ValueTotal / entityTotalValue) * 100d, 2)
                : 0d;

            // バフとして届いたかどうか。名前には影響しない(見出し表は種別を区別しない)。
            // 内部ID注記の表示区分にだけ効く。
            var hasContainer = entity.SkillMetrics.TryGetValue(stat.Key, out var sourceContainer);
            var isBuffSource = hasContainer && sourceContainer!.IsBuffSource;
            // 見出し表で名前が空の行は、記録時に付与元をたどった着地先の名前を出す。
            var landing = hasContainer ? sourceContainer!.Landing : SourceLanding.None;
            var rowKeyText = CombatDataCatalog.FormatSourceKey(stat.Key);

            rows[index] = new MetricSkillTableRowSnapshot(
                stat.Key,
                rowKeyText,
                // 記録時の名前(英語)ではなく、表示中の言語で引き直す。
                CombatDataCatalog.GetSourceDisplayName(stat.Key, isBuffSource, landing),
                value.ValueTotal,
                CombatClockReading.PerSecond(value.ValueTotal, activeSeconds),
                CombatClockReading.PerSecond(value.ValueTotal, elapsedSeconds),
                value.HitsCount,
                value.CritRate,
                value.ValueAverage,
                percentage,
                // 並びは enum の値の順で安定させる。表示の並びは読む側が決める。
                [.. value.GetValueTotalByElementCopy().OrderBy(pair => pair.Key)],
                [.. value.GetValueTotalByModeCopy().OrderBy(pair => pair.Key)]);
        }

        return new MetricSkillTableSnapshot(rows);
    }

    /// <summary>
    /// 計測の状態。通知は出さないので、表示側が定期的に読み直す。
    /// 完了は「計測の回で起点があり、今が窓の終わり以降」から毎回計算する(完了の瞬間にすることは無い)。
    /// </summary>
    public static BenchmarkStateSnapshot GetBenchmarkState()
    {
        var current = EncounterManager.Current;
        var isActive = EncounterManager.IsBenchmarkActive;
        var hasBegun = isActive && current?.ExData.FirstDamageTimeStamp != null;
        return new BenchmarkStateSnapshot(
            isActive,
            hasBegun,
            hasBegun && current!.IsBenchmarkWindowElapsed(DateTime.UtcNow));
    }

    /// <summary>
    /// 計測の開始と停止を切り替える。入口(メーターのヘッダー・集計タブ・ホットキー)はここだけを呼ぶ。
    /// 実行はパケットを処理するスレッド(キャプチャが止まっていれば呼んだスレッド)。表示が変わるのは実行の後。
    /// </summary>
    public static void ToggleBenchmark()
    {
        MessageManager.RunOnPacketThread(EncounterManager.ToggleBenchmark);
    }

    /// <summary>今の回を区切り直す。実行するスレッドは <see cref="ToggleBenchmark"/> と同じ。</summary>
    public static void ResetCurrentEncounter()
    {
        MessageManager.RunOnPacketThread(EncounterManager.ResetCurrentEncounter);
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
    /// <b>アイコンの有無で判定しない。</b> 計数・マーカー・移動アクションの有効化など、
    /// アイコンを持っていてもゲームが出さないバフが多数ある。
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
    /// <see cref="BuffEvent.AddDateTime"/> はサーバ由来の時刻が入る経路があってローカル時計とずれるので使えない。
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
            : remainingSeconds == 0d ? TimeSpan.Zero : TimeSpan.MaxValue;
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
            return;
        }
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

    /// <summary>
    /// メーターが映すエンカウンター。履歴を開いていればその回、無ければ今の回。
    ///
    /// <para>
    /// 設定「次のイベントまで結果を保持」がオンで、今の回が前の回(<see cref="Encounter.PastEncounterForMeter"/>)を持ち、
    /// まだ記録が無いうちは前の回を出す。保持してよい区切りかは回を作るときに決まっている(<see cref="EncounterManager.EnterDungeon"/>)。
    /// 記録が入ったら前の回を手放す(記録は減らないので、外した後に前の回へ戻ることは無い)。
    /// </para>
    /// </summary>
    private static Encounter? ResolveActiveEncounter()
    {
        if (AppState.OpenedHistoricalEncounter is { } openedHistoricalEncounter)
        {
            return openedHistoricalEncounter;
        }

        var currentEncounter = EncounterManager.Current;
        if (CombatRuntimeSettings.KeepPastEncounterInMeterUntilNextDamage
            && currentEncounter?.PastEncounterForMeter is { } pastEncounter)
        {
            if (!currentEncounter.HasStatsBeenRecorded())
            {
                return pastEncounter;
            }

            currentEncounter.PastEncounterForMeter = null;
        }

        return currentEncounter;
    }

    private static MeterPlayerSnapshot CreatePlayerValue(long characterId, Entity entity, MeterSnapshotKind kind, CombatClockReading clock)
    {
        var totalValue = kind == MeterSnapshotKind.Damage
            ? entity.TotalDamage
            : entity.TotalHealing;
        var stats = kind == MeterSnapshotKind.Damage
            ? entity.DamageStats
            : entity.HealingStats;
        var valuePerSecond = CombatClockReading.PerSecond(stats.ValueTotal, clock.Elapsed.TotalSeconds);
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
                source.IsSpecAbilityUnequipped,
                PlayerClassSpecResolver.HasMeanTransformBuff(entity.UUID),
                PlayerClassSpecResolver.HasGolemTransformBuff(entity.UUID)),
            source.CombatPower,
            source.SeasonStrength,
            source.Level,
            source.SeasonLevel,
            isSelf,
            source.IsNpc,
            totalValue,
            valuePerSecond,
            0d,
            0d,
            source.SeasonTalentBuffId,
            source.IsSeasonTalentInactive);
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
