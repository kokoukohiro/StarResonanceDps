using System.Globalization;
using StarResonanceDps.Core.CombatRuntime;
using Zproto;

namespace StarResonanceDps.Core.Diagnostics;

/// <summary>
/// バフ/デバフリストに出るバフのうち、<b>表に名前が無い</b>ものを残す常設の計測。アイコンの有無は見ない。
///
/// <para>
/// <b>呼ぶのは付与の通知の最後</b>(<c>Services.BuffOriginResolver.ResolvePending</c>)。表(<c>BuffNames</c> と手修正)に名前が無く、
/// リストに出る条件(<see cref="BuffDisplayRules.IsDisplayed"/>)を満たすもの。ウィジェットを開いていなくても、全部の実体のバフで拾う。
/// スキル詳細の空欄検知(<see cref="BlankSourceNameProbe"/>)と同じく、どこにも着かず空欄のまま出るものだけでなく、
/// 付与元をたどった由来(オプション・技)に着いてその名前で出るものも書く(由来の名前はバフ自身の名前より確かさが一段低い)。
/// <b>モジュール効果のバフは書かない</b>(呼ぶ側で外す。名前・絵・出し方は番号ごとの手修正で決めている)。
/// どれも<b>たどった鎖</b>を書く。同じバフが別の由来に着いた回は、食い違いとして書く。
/// </para>
///
/// <para>
/// <b>例外として、名前のある装備効果(セット効果・武器効果)のバフも書く</b>(<see cref="CaptureUnhandledEquipmentEffect"/>)。
/// 出し方の手修正(<c>BuffOverrides.json</c> の優先度)が無いまま足す規則だけで出たもので、行の頭は「装備効果の手修正なし」。
/// 名前のあるバフは空欄としては書かないので、手修正の漏れをここで拾う。
/// </para>
///
/// <para>
/// 出たバフは <c>Data/Overrides/BuffNameAssignedOverrides.json</c> に名前を足す候補になる。
/// <b>値は実物を確認してから入れること。</b> 根拠のない名前は、空欄のままより悪い。
/// </para>
///
/// <para>
/// <b>同じバフの番号は着地先の種類ごとに1起動1行</b>、食い違いは(バフ, 先に着いた先, 今回の先)ごとに1行。
/// 付与のたびに届くので、複数残しても同じ内容が並ぶだけ。
/// </para>
///
/// <para>
/// <b>これは調査用の一時計測ではない。</b> 常設で、プローブ撤去の対象外。リリース前に掃除する。
/// </para>
///
/// <para>
/// 出力先は AppContext.BaseDirectory/Logs/<b>Resident</b>/BlankBuffNameProbe_(起動時刻).txt。
/// <b>空欄が1件も無ければファイルを作らない</b>ので、存在すること自体が検知の合図になる。
/// </para>
/// </summary>
public static class BlankBuffNameProbe
{
    private const string LogNamePrefix = "BlankBuffNameProbe";

    private static readonly object Sync = new();
    private static readonly HashSet<(int BuffId, SourceLandingKind Kind)> SeenBuffIds = [];
    private static readonly HashSet<(int BuffId, SourceLanding Decided, SourceLanding Other)> SeenConflicts = [];

    /// <summary>バフの番号 → 最初に着いた由来(着かなかった回は入れない)。食い違いの判定に使う。</summary>
    private static readonly Dictionary<int, SourceLanding> DecidedLandings = [];

    /// <summary>「装備効果の手修正なし」を書いたバフの番号。1起動1バフ1行。</summary>
    private static readonly HashSet<int> SeenUnhandledEquipmentEffects = [];

    /// <summary>
    /// 表に名前の無いバフを1件残す。着地先の種類ごとに1起動1行。由来に着いた回は、前に別の由来に着いていれば食い違いも書く。
    /// </summary>
    /// <param name="holderUuid">バフが付いた実体。</param>
    /// <param name="buffEvent">そのバフ。番号・種別・優先度・持続・付与者・付与元を書く。</param>
    /// <param name="landing">付与元をたどって着いた先。着かなければ無し。</param>
    /// <param name="trace">付与元をたどった鎖(段ごとの種類とID、止まった理由)。</param>
    public static void Capture(long holderUuid, BuffEvent buffEvent, SourceLanding landing, string trace)
    {
        var buffId = buffEvent.BaseId;
        bool isNew;
        SourceLanding? conflictWith = null;
        lock (Sync)
        {
            isNew = SeenBuffIds.Add((buffId, landing.Kind));
            if (landing.Kind != SourceLandingKind.None)
            {
                if (!DecidedLandings.TryGetValue(buffId, out var decided))
                {
                    DecidedLandings[buffId] = landing;
                }
                else if (decided != landing && SeenConflicts.Add((buffId, decided, landing)))
                {
                    conflictWith = decided;
                }
            }
        }

        if (isNew)
        {
            var landingName = CombatDataCatalog.GetLandingName(landing);
            Write(string.Create(
                CultureInfo.InvariantCulture,
                $"バフ={buffId} 内部名={GetInternalName(buffId)} 着地={landing} 由来の名前={(string.IsNullOrEmpty(landingName) ? "(なし)" : landingName)} "
                + $"種別={buffEvent.BuffType} 優先度={buffEvent.BuffPriority} 持続={buffEvent.Duration} {DescribeHolderAndSource(holderUuid, buffEvent)} 鎖={trace}"));
        }

        if (conflictWith is { } decidedLanding)
        {
            Write(string.Create(
                CultureInfo.InvariantCulture,
                $"着地先が食い違う バフ={buffId} 内部名={GetInternalName(buffId)} 先に着いた先={decidedLanding} 今回={landing} "
                + $"{DescribeHolderAndSource(holderUuid, buffEvent)} 鎖={trace}"));
        }
    }

    /// <summary>
    /// 名前のある装備効果のバフが、出し方の手修正の無いまま足す規則だけで出たのを1件残す。1起動1バフ1行。
    /// </summary>
    /// <param name="holderUuid">バフが付いた実体。</param>
    /// <param name="buffEvent">そのバフ。番号・名前・種別・優先度・持続・付与者・付与元を書く。</param>
    public static void CaptureUnhandledEquipmentEffect(long holderUuid, BuffEvent buffEvent)
    {
        var buffId = buffEvent.BaseId;
        lock (Sync)
        {
            if (!SeenUnhandledEquipmentEffects.Add(buffId))
            {
                return;
            }
        }

        Write(string.Create(
            CultureInfo.InvariantCulture,
            $"装備効果の手修正なし バフ={buffId} 内部名={GetInternalName(buffId)} 名前={CombatDataCatalog.GetBuffNameWithoutInternalId(buffId)} "
            + $"種別={buffEvent.BuffType} 優先度={buffEvent.BuffPriority} 持続={buffEvent.Duration} {DescribeHolderAndSource(holderUuid, buffEvent)}"));
    }

    private static string GetInternalName(int buffId)
        => HelperMethods.DataTables.Buffs.Data.TryGetValue(buffId.ToString(CultureInfo.InvariantCulture), out var buff)
            ? buff.NameDesign
            : string.Empty;

    private static string DescribeHolderAndSource(long holderUuid, BuffEvent buffEvent)
        => string.Create(
            CultureInfo.InvariantCulture,
            $"付く相手={holderUuid}({DescribeEntity(holderUuid)}) 付与者={buffEvent.FireUuid}({DescribeEntity(buffEvent.FireUuid)}) "
            + $"付与元={DescribeSourceType(buffEvent.FightSourceType)} {buffEvent.SourceConfigId}");

    private static string DescribeEntity(long uuid)
    {
        if (uuid == 0)
        {
            return "なし";
        }

        var entityType = Utils.UuidToEntityType(uuid);
        var text = Enum.IsDefined(typeof(EEntityType), (int)entityType)
            ? ((EEntityType)entityType).ToString()
            : entityType.ToString(CultureInfo.InvariantCulture);
        if (Utils.IsSummonByUuid(uuid))
        {
            text += "・召喚の印";
        }

        if (uuid == AppState.PlayerUUID)
        {
            text += "・自分";
        }

        return text;
    }

    private static string DescribeSourceType(int fightSourceType)
        => Enum.IsDefined(typeof(EFightSource), fightSourceType)
            ? ((EFightSource)fightSourceType).ToString()
            : fightSourceType.ToString(CultureInfo.InvariantCulture);

    private static void Write(string message)
    {
        try
        {
            var line = DateTimeOffset.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture)
                + "  " + message;
            var directory = DiagnosticSession.ResidentLogDirectory;
            // アプリ起動ごとに別ファイル。追記だとどの回の記録か切り分けられない。
            var path = Path.Combine(directory, $"{LogNamePrefix}_{DiagnosticSession.Stamp}.txt");

            lock (Sync)
            {
                Directory.CreateDirectory(directory);
                DiagnosticSession.EnsureHeader(path);
                File.AppendAllText(path, line + Environment.NewLine, System.Text.Encoding.UTF8);
            }
        }
        catch
        {
            // 診断のみ。本来の処理へは伝播させない。
        }
    }
}
