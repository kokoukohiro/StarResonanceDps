using System.IO;
using System.Linq;

namespace StarResonanceDps.App.Config;

public static class AppDataPaths
{
    // 旧いファイル名。
    private const string LegacyAppSettingsFileName = "appsettings.json";
    private const string LegacyWidgetSettingsFileName = "widgetstate.json";

    private const string AppSettingsFileName = "AppSettings.json";
    private const string WidgetSettingsFileName = "WidgetSettings.json";

    public static string BaseDirectory => AppContext.BaseDirectory;

    public static string DataDirectory => Path.Combine(BaseDirectory, "Data");

    public static string AppSettingsPath => Path.Combine(DataDirectory, AppSettingsFileName);

    public static string WidgetStatePath => Path.Combine(DataDirectory, WidgetSettingsFileName);

    // Windows の通知のアイコン。通知の処理が埋め込みから書き出す。Windows の通知の登録名はこのパスから作る
    // (Windows は登録名ごとに最初に読んだアイコンの場所を覚えて読み直さないため、場所を変えたら登録名も変わる形にしてある)。
    public static string WindowsNotificationIconPath => Path.Combine(DataDirectory, "Images", "Icon.png");

    // Plugin DLLs and every plugin-owned generated file share this one runtime directory.
    public static string PluginsDirectory => Path.Combine(BaseDirectory, "Plugins");

    public static string GetLegacyAppSettingsPath()
    {
        return Path.Combine(BaseDirectory, LegacyAppSettingsFileName);
    }

    public static string GetLegacyWidgetStatePath()
    {
        return Path.Combine(BaseDirectory, LegacyWidgetSettingsFileName);
    }

    /// <summary>
    /// <c>Data/</c> にある旧い名前の設定ファイルを、いまの名前へ改名する。
    ///
    /// <para>
    /// <b>設定を読む前に一度だけ呼ぶこと。</b> <see cref="ConfigManager"/> と
    /// <see cref="WidgetStateManager"/> は遅延生成の singleton なので、
    /// どちらかに最初に触れるより前に済ませる必要がある。
    /// </para>
    ///
    /// <para>
    /// <c>BaseDirectory</c> 直下に残っている更に古い配置は**ここでは触らない**。
    /// あちらは各マネージャが「新しい場所に無ければ読む」経路で拾い、書き戻しは
    /// 新しい場所へ行う。
    /// </para>
    /// </summary>
    public static void MigrateLegacyFileNames()
    {
        RenameInDataDirectory(LegacyAppSettingsFileName, AppSettingsFileName);
        RenameInDataDirectory(LegacyWidgetSettingsFileName, WidgetSettingsFileName);
    }

    /// <summary>
    /// <c>Data/</c> の <paramref name="legacyName"/> を <paramref name="currentName"/> へ改名する。
    ///
    /// <para>
    /// <b>判定に <see cref="File.Exists"/> を使ってはいけない。</b> Windows は大文字小文字を
    /// 区別しないので、<c>appsettings.json</c> しか無くても <c>AppSettings.json</c> が
    /// あることになってしまい、改名が要らないと誤判定する。ディスク上の実際の綴りを
    /// 列挙して序数比較する。
    /// </para>
    ///
    /// <para>
    /// 改名自体は <see cref="File.Move(string,string)"/> 一回でよい。
    /// 同一ボリューム内なら大文字小文字だけの違いでも通る。
    /// </para>
    /// </summary>
    private static void RenameInDataDirectory(string legacyName, string currentName)
    {
        var directory = DataDirectory;
        if (!Directory.Exists(directory))
        {
            return;
        }

        // いまの綴りで既にあるなら何もしない。綴りまで一致するものだけを見る。
        var alreadyCurrent = Directory.EnumerateFiles(directory, currentName)
            .Any(path => string.Equals(Path.GetFileName(path), currentName, StringComparison.Ordinal));
        if (alreadyCurrent)
        {
            return;
        }

        var legacyPath = Directory.EnumerateFiles(directory, legacyName).FirstOrDefault();
        if (legacyPath is null)
        {
            return;
        }

        // 失敗はそのまま表面化させる。握り潰すと、設定が旧名のまま残っていることに
        // 気付けないまま「新しい名前で保存しているつもり」になる。
        File.Move(legacyPath, Path.Combine(directory, currentName));
    }
}
