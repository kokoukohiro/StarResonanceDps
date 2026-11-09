using System.Globalization;
using System.Text;
using Serilog;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.Core.Diagnostics;

/// <summary>
/// 計測ログをアプリ起動ごとに分け、ファイル単体で素性が分かるようにする。行の形はアプリのログ(<see cref="CombatRuntimePaths.LogFilePath"/>)にそろえる。
///
/// <para>
/// <b>同じファイルに追記し続けない。</b>どの起動・どのビルドの記録なのかを後から切り分けられず、
/// 区切りを推測しながら読むことになる。
/// </para>
/// </summary>
public static class DiagnosticSession
{
    // アプリのログの行と同じ時刻の形。
    private const string TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff zzz";

    private const string DetectionLevel = "WRN";

    private static readonly UTF8Encoding LogEncoding = new(encoderShouldEmitUTF8Identifier: false);
    private static readonly object ResidentWriteLock = new();

    /// <summary>前の起動までの検知ログにある種類と番号。その起動で最初の検知のときに1回読む。</summary>
    private static HashSet<string>? _earlierResidentKeys;

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
    ///
    /// <para>
    /// 起動ごとに増えないように、同じ種類と番号の行があるうちは後の起動で書かない。手で消せば、次に起きたときにまた書かれる。
    /// アプリのバージョンが変わると全部消す(<see cref="ClearResidentLogs"/>)。
    /// </para>
    /// </summary>
    public static string ResidentLogDirectory { get; } =
        Path.Combine(CombatRuntimePaths.LogsDirectory, "Resident");

    private static readonly HashSet<string> HeaderWritten = [];
    private static readonly Dictionary<string, (long Uuid, long CharId)> LastIdentity = [];

    /// <summary>
    /// 常設の検知を1行書く。ファイルは <c>Logs\Resident\{logName}_{起動時刻}.log</c>、行は <c>{時刻} [WRN] {種類} {番号} {詳細}</c>。
    /// <paramref name="details"/> は <c>key=value</c> を空白でつないだ文字で、報告の本文に入れない値(ほかのプレイヤーの番号など)も書いてよい。
    /// 前の起動のログに同じ種類と番号の行があれば書かない。その起動の中で何件書くかは呼び出し元が決める。
    /// 診断のみなので、書けなくても本来の処理へは伝播させない。
    /// </summary>
    public static void WriteResident(string logName, ResidentDetectionKind kind, string id, string details)
    {
        try
        {
            // アプリ起動ごとに別ファイル。追記だとどの回の記録か切り分けられない。
            var path = Path.Combine(ResidentLogDirectory, $"{logName}_{Stamp}.log");

            lock (ResidentWriteLock)
            {
                _earlierResidentKeys ??= ReadResidentKeys();
                if (_earlierResidentKeys.Contains($"{kind} {id}"))
                {
                    return;
                }

                Directory.CreateDirectory(ResidentLogDirectory);
                EnsureHeader(path);
                Append(path, DetectionLevel, $"{kind} {id} {details}");
            }
        }
        catch
        {
            // 診断のみ。本来の処理へは伝播させない。
        }
    }

    /// <summary>常設の検知ログを全部消す。アプリのバージョンが変わった最初の起動で呼ぶ。消せなくてもアプリの処理は止めない。</summary>
    public static void ClearResidentLogs()
    {
        try
        {
            if (Directory.Exists(ResidentLogDirectory))
            {
                Directory.Delete(ResidentLogDirectory, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Log.Warning(exception, "Could not delete {Path}", ResidentLogDirectory);
        }
    }

    private static HashSet<string> ReadResidentKeys()
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        if (!Directory.Exists(ResidentLogDirectory))
        {
            return keys;
        }

        foreach (var path in Directory.EnumerateFiles(ResidentLogDirectory, "*.log"))
        {
            foreach (var line in File.ReadLines(path, LogEncoding))
            {
                // 行は「{日付} {時刻} {時差} [WRN] {種類} {番号} {詳細}」。
                var parts = line.Split(' ', 7);
                if (parts.Length >= 6 && parts[3] == $"[{DetectionLevel}]")
                {
                    keys.Add($"{parts[4]} {parts[5]}");
                }
            }
        }

        return keys;
    }

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
            Append(path, "INF", "Session started build=" + BuildStamp + " " + Describe(current));
            LastIdentity[path] = current;
            return;
        }

        if (LastIdentity.TryGetValue(path, out var prior) && prior != current)
        {
            LastIdentity[path] = current;
            Append(path, "INF", "Self identity changed: " + Describe(prior) + " -> " + Describe(current));
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
            ? "self=(unknown)"
            : "self charId=" + identity.CharId.ToString(CultureInfo.InvariantCulture)
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
                ? "(unknown)"
                : File.GetLastWriteTime(location).ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
        }
    }

    private static void Append(string path, string level, string message)
    {
        var timestamp = DateTimeOffset.Now.ToString(TimestampFormat, CultureInfo.InvariantCulture);
        File.AppendAllText(path, $"{timestamp} [{level}] {message}{Environment.NewLine}", LogEncoding);
    }
}
