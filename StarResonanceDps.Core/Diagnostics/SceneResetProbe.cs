using System.Globalization;

namespace StarResonanceDps.Core.Diagnostics;

/// <summary>
/// マップ切替でプレイヤーリスト/エンティティリストが刷新されない偶発バグを追うための受動的な計測。
///
/// <para>
/// シーン切替に関わる出来事を到着順にすべて残す。刷新が起きなかった回で、
/// どの経路が来てどの経路が来なかったのかを後から時系列で追えるようにするのが目的。
/// </para>
///
/// <list type="bullet">
///   <item>SCENE  — ApplySceneData の呼び出し(呼び元・シーン識別子・AllowSceneUpdate・リセット有無)</item>
///   <item>SKIP   — SceneData は届いたが AllowSceneUpdate が false で捨てた</item>
///   <item>NEWMAP — BattleStateMachine.StartNewMap(フルコンテナ到着の合図)</item>
///   <item>RESET  — 各リストの BeginMap(消す直前の件数つき)</item>
/// </list>
///
/// 出力先は AppContext.BaseDirectory/Logs/SceneResetProbe_yyyy-MM-dd.txt。
/// </summary>
public static class SceneResetProbe
{
    private const string LogNamePrefix = "SceneResetProbe";

    private static readonly object Sync = new();
    private static readonly HashSet<string> SkipSeen = [];

    /// <summary>シーンが変わったら SKIP の重複抑制をやり直す。</summary>
    private static void ResetSkipSuppression()
    {
        lock (Sync)
        {
            SkipSeen.Clear();
        }
    }

    /// <summary>ApplySceneData の呼び出し。刷新したかどうかまで含めて毎回記録する。</summary>
    public static void CaptureSceneData(
        string source,
        uint levelMapId,
        uint lineId,
        string? sceneGuid,
        bool allowSceneUpdateOnEntry,
        bool didReset)
    {
        if (didReset)
        {
            ResetSkipSuppression();
        }

        Write("SCENE  経路=" + source
            + " levelMapId=" + levelMapId.ToString(CultureInfo.InvariantCulture)
            + " lineId=" + lineId.ToString(CultureInfo.InvariantCulture)
            + " sceneGuid=" + (string.IsNullOrEmpty(sceneGuid) ? "(空)" : sceneGuid)
            + " AllowSceneUpdate(入口)=" + allowSceneUpdateOnEntry
            + " → " + (didReset ? "★リストを刷新した" : "刷新なし(同一シーン扱い)"));
    }

    /// <summary>
    /// SceneData は届いたのに AllowSceneUpdate=false で捨てた回。
    /// 速い経路(NotifySocialData)が死んでいる状態を可視化する。
    /// </summary>
    public static void CaptureSkipped(string source, uint levelMapId, uint lineId, string? sceneGuid)
    {
        // 同一シーンの通知は数秒おきに飛んでくる。常時置いておくため初回だけ残す。
        lock (Sync)
        {
            if (!SkipSeen.Add(source + "#" + levelMapId + "#" + lineId))
            {
                return;
            }
        }

        Write("SKIP   経路=" + source
            + " levelMapId=" + levelMapId.ToString(CultureInfo.InvariantCulture)
            + " lineId=" + lineId.ToString(CultureInfo.InvariantCulture)
            + " sceneGuid=" + (string.IsNullOrEmpty(sceneGuid) ? "(空)" : sceneGuid)
            + " ← AllowSceneUpdate=false のため ApplySceneData を呼ばなかった");
    }

    /// <summary>StartNewMap。フルコンテナが届いた合図であり AllowSceneUpdate が再武装される。</summary>
    public static void CaptureStartNewMap()
    {
        Write("NEWMAP BattleStateMachine.StartNewMap(フルコンテナ到着 → AllowSceneUpdate 再武装)");
    }

    /// <summary>各リストの BeginMap。消す直前の件数を残す。</summary>
    public static void CaptureReset(string listName, int countBeforeClear)
    {
        Write("RESET  " + listName
            + " BeginMap 消去直前の件数=" + countBeforeClear.ToString(CultureInfo.InvariantCulture));
    }

    private static void Write(string message)
    {
        try
        {
            var line = DateTimeOffset.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture)
                + "  " + message;
            var directory = Path.Combine(AppContext.BaseDirectory, "Logs");
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
