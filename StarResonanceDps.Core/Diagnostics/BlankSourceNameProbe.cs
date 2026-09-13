using System.Globalization;

namespace StarResonanceDps.Core.Diagnostics;

/// <summary>
/// スキル詳細ウィジェットの行に<b>名前が入っていない</b>鍵を残す常設の計測。
///
/// <para>
/// メーターの行名は <c>Data/Localization/recounts.*.json</c>(ゲーム内メーターの見出し表の写し)
/// だけが決める。総括行(其他)は行ごと落としてあるので、そこに居た鍵と、
/// 見出し表にそもそも無い鍵は<b>名前が空のまま内部ID注記だけ</b>で出る。
/// どの鍵がそうなったかは実戦で撃ってみないと分からないので、常設で拾う。
/// </para>
///
/// <para>
/// 出た鍵は <c>Data/Overrides/RecountOverrides.json</c> に手当てする候補になる。
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
/// <b>同じ鍵は1起動につき1行だけ。</b> <see cref="SpecConflictProbe"/> が同一IDで3件まで残すのは、
/// 術者や <c>sourceConfigId</c> が違う別々の事象を見るため。こちらはスナップショットが
/// 毎秒作り直されるので、複数残しても同じ内容が並ぶだけで新しい情報が無い。
/// 1鍵1行にすると、ファイルがそのまま「名前を入れるべき鍵の一覧」になる。
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
    private static readonly HashSet<long> SeenRowKeys = [];

    /// <summary>
    /// 名前が空のままスキル詳細ウィジェットに載った行を1件残す。
    /// </summary>
    /// <param name="rowKey">畳んだあとの行代表キー。<c>ownerId:枝番</c> で書き出す。</param>
    /// <param name="rowKeyText">その鍵の表示形。手修正ファイルへそのまま写せる形で残す。</param>
    /// <param name="isBuffSource">届いたときの種別。名前には効かないが、正体を追う手掛かりになる。</param>
    /// <param name="isHealing">与ダメ側とヒール側のどちらのウィジェットで出たか。</param>
    /// <param name="totalValue">
    /// <b>検知した時点の</b>累計値。スナップショットは毎秒作り直されるので最終値ではない。
    /// </param>
    /// <param name="hitCount">同じく検知した時点のヒット数。</param>
    /// <param name="characterId">出した人。職の当たりを付ける手掛かり。</param>
    public static void Capture(
        long rowKey,
        string rowKeyText,
        bool isBuffSource,
        bool isHealing,
        ulong totalValue,
        ulong hitCount,
        long characterId)
    {
        lock (Sync)
        {
            if (!SeenRowKeys.Add(rowKey))
            {
                return;
            }
        }

        Write(string.Create(
            CultureInfo.InvariantCulture,
            $"行={rowKeyText} 種別={(isBuffSource ? "バフ" : "スキル")} 表={(isHealing ? "ヒール" : "与ダメ")} "
            + $"検知時点 累計={totalValue} ヒット={hitCount} charId={characterId}"));
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
