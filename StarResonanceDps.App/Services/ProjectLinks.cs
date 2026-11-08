namespace StarResonanceDps.App.Services;

/// <summary>アプリの外で開くプロジェクトのページ(GitHubのリポジトリとWiki)。</summary>
public static class ProjectLinks
{
    public const string RepositoryUrl = "https://github.com/kokoukohiro/StarResonanceDps";

    public const string WikiUrl = RepositoryUrl + "/wiki";

    /// <summary>Wikiのページ名はdocs/wikiのファイル名。ファイル名を変えるとこのリンクが切れる。</summary>
    public static readonly string PluginDevelopmentUrl = WikiUrl + "/" + Uri.EscapeDataString("プラグイン開発");

    public const string ReleasesUrl = RepositoryUrl + "/releases";

    public const string LicenseUrl = RepositoryUrl + "/blob/main/LICENSING.md";
}
