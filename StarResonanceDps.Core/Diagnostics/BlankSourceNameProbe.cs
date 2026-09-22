using System.Globalization;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.Core.Diagnostics;

/// <summary>
/// スキル詳細ウィジェットの行(プレイヤーの与ダメ・ヒール)に<b>名前が入っていない</b>鍵を残す常設の計測。
///
/// <para>
/// <b>呼ぶのは記録の直後</b>(<c>Encounter.ResolveSourceLanding</c>)。ウィジェットを開いていなくても、
/// 全プレイヤーの与ダメ・ヒールの両方で拾う。行を作る側に置くと、開いている1人・1種別しか見えない。
/// </para>
///
/// <para>
/// メーターの行名は <c>Data/Localization/RecountRows.json</c>(ゲーム内メーターの見出し表の写し)が先に決める。
/// 総括行(其他)は行ごと落としてあるので、そこに居た鍵と見出し表にそもそも無い鍵は、
/// 記録時に付与元をたどった着地先(オプションか技、<c>Services.SourceLandingResolver</c>)の名前で出る。
/// ここに残すのは、どこにも着かず<b>名前が空のまま内部ID注記だけ</b>で出る鍵と、技に着いた鍵(オプションより確かさが一段低い)、
/// 着地先が食い違った鍵。どれも<b>たどった鎖</b>を書く。
/// 着かなかった鍵は、後の記録で着けば DB に空欄が残らないので、記録の時点では書かずにエンカウンターに控え、
/// DB へ保存した直後にまだ着いていないものだけを書く(<c>Encounter.ReportUnresolvedBlankSources</c>)。
/// </para>
///
/// <para>
/// 出た鍵は <c>Data/Overrides/RecountRowOverrides.json</c> に手当てする候補になる。
/// 行に寄せるなら <c>Row</c>、独立した行として名前を付けるなら <c>Name</c>。
/// <b>値は実物を確認してから入れること。</b> 根拠のない名前は、空欄のままより悪い。
/// </para>
///
/// <para>
/// <b>判定は注記を付ける前の生名(<c>GetSourceName</c>)で行う。</b>
/// 表示名は空欄でも <c>(2203531:1)</c> の注記が付くので空文字にならず、
/// しかも注記は表示設定で消えるため、表示名で見ると設定次第で検知が変わる。
/// </para>
///
/// <para>
/// <b>同じ鍵は結果の種類ごとに1起動1行。</b> <see cref="SpecConflictProbe"/> が同一IDで3件まで残すのは、
/// 術者や <c>sourceConfigId</c> が違う別々の事象を見るため。こちらは同じ鍵がヒットのたびに
/// 届くので、複数残しても同じ内容が並ぶだけで新しい情報が無い。
/// 着かなかった行だけを拾えば、ファイルがそのまま「名前を入れるべき鍵の一覧」になる。
/// </para>
///
/// <para>
/// <b>これは調査用の一時計測ではない。</b> 常設で、プローブ撤去の対象外。
/// リリース前に掃除する。
/// </para>
///
/// <para>
/// 出力先は AppContext.BaseDirectory/Logs/<b>Resident</b>/BlankSourceNameProbe_(起動時刻).txt。
/// <b>空欄が1件も無ければファイルを作らない</b>ので、存在すること自体が検知の合図になる。
/// </para>
/// </summary>
public static class BlankSourceNameProbe
{
    private const string LogNamePrefix = "BlankSourceNameProbe";

    private static readonly object Sync = new();
    private static readonly HashSet<(long RowKey, SourceLandingKind Kind)> SeenRowKeys = [];
    private static readonly HashSet<(long RowKey, SourceLanding Decided, SourceLanding Other)> SeenConflicts = [];

    /// <summary>
    /// 見出し表で名前が空の行を1件残す。保存の時点で着いていなかったもの(<paramref name="landing"/> が無し)と、技に着いたものを書く。
    /// 同じ鍵は結果の種類ごとに1起動1行(ある戦闘で着かなかった鍵が別の戦闘で技に着けば、それも1行書く)。
    /// </summary>
    /// <param name="rowKey">畳んだあとの行代表キー。<c>ownerId:枝番</c> で書き出す。</param>
    /// <param name="rowKeyText">その鍵の表示形。手修正ファイルへそのまま写せる形で残す。</param>
    /// <param name="isBuffSource">届いたときの種別。名前には効かないが、正体を追う手掛かりになる。</param>
    /// <param name="isHealing">与ダメとヒールのどちらの記録で出たか。</param>
    /// <param name="totalValue">
    /// <b>検知した時点の</b>累計値。記録の途中で拾うので最終値ではない(たいてい最初のヒットの値)。
    /// </param>
    /// <param name="hitCount">同じく検知した時点のヒット数。</param>
    /// <param name="characterId">出した人。職の当たりを付ける手掛かり。</param>
    /// <param name="landing">付与元をたどって着いた先。着かなければ無し。</param>
    /// <param name="trace">たどった鎖(段ごとの種類とID、止まった理由)。</param>
    /// <param name="firstBlankAt">
    /// 着かなかった鍵の、最初に着かなかった記録の到着時刻。行の先頭の時刻は書いた時刻(保存の直後)なので別に残す。技に着いた鍵では無し。
    /// </param>
    public static void Capture(
        long rowKey,
        string rowKeyText,
        bool isBuffSource,
        bool isHealing,
        ulong totalValue,
        ulong hitCount,
        long characterId,
        SourceLanding landing,
        string trace,
        DateTime? firstBlankAt = null)
    {
        lock (Sync)
        {
            if (!SeenRowKeys.Add((rowKey, landing.Kind)))
            {
                return;
            }
        }

        var firstBlankText = firstBlankAt is { } blankAt
            ? string.Create(CultureInfo.InvariantCulture, $"初回={blankAt.ToLocalTime():HH:mm:ss.fff} ")
            : string.Empty;
        Write(string.Create(
            CultureInfo.InvariantCulture,
            $"行={rowKeyText} 種別={(isBuffSource ? "バフ" : "スキル")} 表={(isHealing ? "ヒール" : "与ダメ")} "
            + $"検知時点 累計={totalValue} ヒット={hitCount} charId={characterId} 着地={landing} {firstBlankText}鎖={trace}"));
    }

    /// <summary>
    /// 着地先が決まった鍵で、別の先に着いた回を残す。同じ(鍵, 決まった先, 別の先)は1起動1行。
    /// </summary>
    public static void CaptureConflict(
        long rowKey,
        string rowKeyText,
        bool isHealing,
        long characterId,
        SourceLanding decided,
        SourceLanding other,
        string trace)
    {
        lock (Sync)
        {
            if (!SeenConflicts.Add((rowKey, decided, other)))
            {
                return;
            }
        }

        Write(string.Create(
            CultureInfo.InvariantCulture,
            $"着地先が食い違う 行={rowKeyText} 表={(isHealing ? "ヒール" : "与ダメ")} charId={characterId} "
            + $"決まった先={decided} 今回={other} 鎖={trace}"));
    }

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
