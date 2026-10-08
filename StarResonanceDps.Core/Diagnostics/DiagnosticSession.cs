using System.Globalization;
using System.Text;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.Core.Diagnostics;

/// <summary>
/// 計測ログをアプリ起動ごとに分け、ファイル単体で素性が分かるようにする。
///
/// <para>
/// <b>同じファイルに追記し続けない。</b>どの起動・どのビルドの記録なのかを後から切り分けられず、
/// 区切りを推測しながら読むことになる。
/// </para>
/// </summary>
public static class DiagnosticSession
{
    /// <summary>ファイル名に使う <c>yyyy-MM-dd_HHmmss</c>。プロセスごとに一度だけ決まる。</summary>
    public static string Stamp { get; } =
        DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture);

    /// <summary>
    /// 常設プローブの出力先。<c>Logs</c> 直下には一時計測やアプリのログも並ぶので、
    /// <b>出たら見るべきものだけ</b>をここへ分ける。
    ///
    /// <para>
    /// 常設プローブは検知が1件も無ければファイルを作らない。
    /// このフォルダにファイルがあること自体が検知の合図になる。
    /// </para>
    /// </summary>
    public static string ResidentLogDirectory { get; } =
        Path.Combine(CombatRuntimePaths.LogsDirectory, "Resident");

    private static readonly HashSet<string> HeaderWritten = [];
    private static readonly Dictionary<string, (long Uuid, long CharId)> LastIdentity = [];

    /// <summary>
    /// ファイルの先頭にヘッダを1行書く。書き込み済みなら、自分の identity が変わったときだけ1行足す。
    ///
    /// <para>
    /// <b>呼び出し元のロックの中から呼ぶこと。</b> 各プローブが自分のファイルを排他している前提で、
    /// ここでは追加のロックを取らない。
    /// </para>
    ///
    /// <para>
    /// identity をヘッダだけで済ませないのは、最初の書き込み時点ではまだ未取得のことがあるため。
    /// 未取得なら未取得と書き、判明した時点で1行足す。それらしい値で埋めない。
    /// </para>
    /// </summary>
    public static void EnsureHeader(string path)
    {
        var current = CurrentIdentity();

        if (HeaderWritten.Add(path))
        {
            Append(path,
                "==== 計測開始 " + Stamp
                + "  ビルド=" + BuildStamp
                + "  " + Describe(current));
            LastIdentity[path] = current;
            return;
        }

        if (LastIdentity.TryGetValue(path, out var prior) && prior != current)
        {
            LastIdentity[path] = current;
            Append(path, "==== 自分の identity が変化: " + Describe(prior) + " → " + Describe(current));
        }
    }

    private static (long Uuid, long CharId) CurrentIdentity()
    {
        var uuid = MessageManager.currentUserUuid != 0
            ? MessageManager.currentUserUuid
            : AppState.PlayerUUID;
        return (uuid, AppState.PlayerUID);
    }

    private static string Describe((long Uuid, long CharId) identity)
    {
        return identity.Uuid == 0 && identity.CharId == 0
            ? "自分=(未取得)"
            : "自分 charId=" + identity.CharId.ToString(CultureInfo.InvariantCulture)
                + " uuid=" + identity.Uuid.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// 実行中アセンブリの最終更新時刻。どのビルドで採ったログかを取り違えないために出す。
    /// 単一ファイル発行などで場所が取れない場合は、取れなかったと分かるように書く。
    /// </summary>
    private static string BuildStamp
    {
        get
        {
            var location = typeof(DiagnosticSession).Assembly.Location;
            return string.IsNullOrEmpty(location)
                ? "(取得不可)"
                : File.GetLastWriteTime(location).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }
    }

    private static void Append(string path, string line)
    {
        File.AppendAllText(path, line + Environment.NewLine, Encoding.UTF8);
    }
}
