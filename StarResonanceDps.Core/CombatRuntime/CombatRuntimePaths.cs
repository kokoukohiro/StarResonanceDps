namespace StarResonanceDps.Core.CombatRuntime;

/// <summary>
/// Core が読み書きするファイルの場所。基準は実行フォルダ(<see cref="AppContext.BaseDirectory"/>)の1つだけ。
///
/// <para>
/// <b>作業フォルダは読まない・変えない。</b>作業フォルダは起動する側が決め(ショートカットの設定・別のフォルダからの起動)、
/// 実行中もプロセスのどこからでも書き換えられる。App のファイルの場所(<c>AppDataPaths</c>)もここの基準から組む。
/// </para>
/// </summary>
public static class CombatRuntimePaths
{
    public static string BaseDirectory => AppContext.BaseDirectory;

    public static string DataDirectory => Path.Combine(BaseDirectory, "Data");

    /// <summary>
    /// 言語別の生テーブルをそのまま置くフォルダ。<c>Data</c> 直下は設定と実行時の生成物
    /// (<c>AppSettings.json</c> / <c>WidgetSettings.json</c> / 戦闘履歴DB / ログ)が並ぶので、生データはここへ分けてある。
    ///
    /// <para>
    /// <b>中身は加工しないこと。</b> 間引きや書き換えをすると、テーブルを取り直したときに
    /// 何が自前の変更だったのか分からなくなる。絞り込みは読む側で行う。
    /// </para>
    /// </summary>
    public static string RawTableDirectory => Path.Combine(DataDirectory, "Raw");

    public static string LocalizationDirectory => Path.Combine(DataDirectory, "Localization");

    public static string OverridesDirectory => Path.Combine(DataDirectory, "Overrides");

    public static string GeneratedDirectory => Path.Combine(DataDirectory, "Generated");

    public static string DatabasePath => Path.Combine(DataDirectory, "CombatHistory.db");

    public static string LogFilePath => Path.Combine(DataDirectory, "CombatRuntime.log");

    /// <summary>計測ログの置き場。製品では無くなることがあるので、アプリのログ(<see cref="LogFilePath"/>)は置かない。</summary>
    public static string LogsDirectory => Path.Combine(BaseDirectory, "Logs");

    /// <summary>
    /// <see cref="DataDirectory"/> を作り、書けるかを確かめる。書けなければ例外(<see cref="IOException"/> /
    /// <see cref="UnauthorizedAccessException"/>)をそのまま投げる。
    /// 確かめのファイルは起動ごとに違う名前で作り(同じ名前の残りで誤って書けないと判定しないため)、閉じると OS が消す。
    /// </summary>
    public static void EnsureWritableDataDirectory()
    {
        Directory.CreateDirectory(DataDirectory);
        var checkPath = Path.Combine(DataDirectory, $".write-check-{Guid.NewGuid():N}.tmp");
        using var stream = new FileStream(
            checkPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 1,
            FileOptions.DeleteOnClose);
    }
}
