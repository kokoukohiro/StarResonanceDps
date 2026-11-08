namespace StarResonanceDps.App.Services;

/// <summary>プロジェクトのページ(GitHubのリポジトリとWiki)と、更新の確認に使うGitHubのAPI。</summary>
public static class ProjectLinks
{
    public const string RepositoryUrl = "https://github.com/kokoukohiro/StarResonanceDps";

    public const string WikiUrl = RepositoryUrl + "/wiki";

    /// <summary>Wikiのページ名はdocs/wikiのファイル名。ファイル名を変えるとこのリンクが切れる。</summary>
    public static readonly string PluginDevelopmentUrl = WikiUrl + "/" + Uri.EscapeDataString("プラグイン開発");

    public const string ReleasesUrl = RepositoryUrl + "/releases";

    public const string LicenseUrl = RepositoryUrl + "/blob/main/LICENSING.md";

    /// <summary>公開された最新のリリース。下書きとプレリリースは返らない。リリースが1つも無ければ404。</summary>
    public const string LatestReleaseApiUrl = "https://api.github.com/repos/kokoukohiro/StarResonanceDps/releases/latest";
}
