using System.Globalization;

namespace StarResonanceDps.App.Services;

/// <summary>ヘルプのリンクのうち、アプリの言語で開く先が変わるページ。</summary>
public enum LocalizedProjectPage
{
    /// <summary>Wikiのその言語の入口。</summary>
    WikiHome,

    /// <summary>Wikiのその言語のプラグイン開発のページ。</summary>
    PluginDevelopment,

    /// <summary>その言語のREADME。日本語はリポジトリのホームに出るREADME.md。</summary>
    Readme
}

/// <summary>プロジェクトのページ(GitHubのリポジトリとWiki)と、更新の確認とIssueの検索に使うGitHubのAPI。</summary>
public static class ProjectLinks
{
    public const string RepositoryUrl = "https://github.com/kokoukohiro/StarResonanceDps";

    private const string WikiUrl = RepositoryUrl + "/wiki";

    /// <summary>日本語の言語の印。READMEだけは印の無いREADME.mdが日本語。</summary>
    private const string JapaneseSuffix = "ja";

    public const string ReleasesUrl = RepositoryUrl + "/releases";

    public const string LicenseUrl = RepositoryUrl + "/blob/main/LICENSING.md";

    /// <summary>Issueの投稿画面。型のファイル名は .github/ISSUE_TEMPLATE と同じ。</summary>
    public const string NewIssueUrl = RepositoryUrl + "/issues/new?template=feedback.yml";

    /// <summary>公開された最新のリリース。下書きとプレリリースは返らない。リリースが1つも無ければ404。</summary>
    public const string LatestReleaseApiUrl = "https://api.github.com/repos/kokoukohiro/StarResonanceDps/releases/latest";

    /// <summary>Issueの検索。ログインなしは同じIPアドレスから1分に10回まで。</summary>
    public const string IssueSearchApiUrl = "https://api.github.com/search/issues";

    /// <summary>Issueの検索をこのリポジトリに絞る修飾子。</summary>
    public const string IssueSearchRepository = "repo:kokoukohiro/StarResonanceDps";

    /// <summary>
    /// アプリの言語のページのURL。Wikiのページ名は英語の名前の後ろに言語の印を付けたdocs/wikiのファイル名、
    /// READMEはリポジトリの直下のファイル名と同じで、ファイル名を変えるとこのリンクが切れる。
    /// </summary>
    public static string GetLocalizedUrl(LocalizedProjectPage page, CultureInfo culture)
    {
        var suffix = GetLanguageSuffix(culture);
        return page switch
        {
            LocalizedProjectPage.WikiHome => $"{WikiUrl}/Home-{suffix}",
            LocalizedProjectPage.PluginDevelopment => $"{WikiUrl}/Plugin-Development-{suffix}",
            LocalizedProjectPage.Readme => suffix == JapaneseSuffix ? RepositoryUrl : $"{RepositoryUrl}/blob/main/README.{suffix}.md",
            _ => throw new ArgumentOutOfRangeException(nameof(page), page, null)
        };
    }

    /// <summary>アプリの言語(<c>LocalizationManager.CurrentCulture</c>)から言語の印。4言語のほかは英語(アプリの文言と同じ)。</summary>
    private static string GetLanguageSuffix(CultureInfo culture) => culture.Name switch
    {
        "ja-JP" => JapaneseSuffix,
        "ko-KR" => "ko",
        "zh-CN" => "zh-CN",
        _ => "en"
    };
}
