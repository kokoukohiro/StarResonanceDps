using System.Runtime.InteropServices;
using StarResonanceDps.App.Localization;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Diagnostics;

namespace StarResonanceDps.App.Services;

/// <summary>GitHubのIssueの投稿画面を開くURLを作る。欄の名前は .github/ISSUE_TEMPLATE/feedback.yml と同じ。</summary>
internal static class FeedbackIssueLinks
{
    // これより長いと、GitHubのログインの後に入力済みの画面へ戻れない。
    private const int MaxUrlLength = 4000;

    public static string CreateNewIssueUrl()
    {
        return ProjectLinks.NewIssueUrl + "&environment=" + Uri.EscapeDataString(CreateEnvironmentText());
    }

    /// <summary>
    /// 検出された項目の報告のURLを作る。先頭から入る行だけを本文に入れ、入れた行の数を返す。
    /// 1行だけで入らないときは、その行の末尾を切る。
    /// </summary>
    public static DetectionReportLink CreateDetectionReportLink(IReadOnlyList<ResidentDetection> detections)
    {
        var environment = CreateEnvironmentText();
        var content = string.Empty;
        var url = string.Empty;
        var count = 0;

        foreach (var detection in detections)
        {
            var line = FormatLine(detection);
            var nextContent = count == 0 ? line : content + "\n" + line;
            var nextUrl = CreateDetectionUrl(count + 1, environment, nextContent);
            if (nextUrl.Length > MaxUrlLength)
            {
                break;
            }

            content = nextContent;
            url = nextUrl;
            count++;
        }

        return count > 0
            ? new DetectionReportLink(url, count)
            : new DetectionReportLink(CreateTruncatedUrl(environment, FormatLine(detections[0])), 1);
    }

    private static string CreateTruncatedUrl(string environment, string line)
    {
        var low = 0;
        var high = line.Length;
        while (low < high)
        {
            var middle = (low + high + 1) / 2;
            if (CreateDetectionUrl(1, environment, TakeWholeCharacters(line, middle)).Length <= MaxUrlLength)
            {
                low = middle;
            }
            else
            {
                high = middle - 1;
            }
        }

        return CreateDetectionUrl(1, environment, TakeWholeCharacters(line, low));
    }

    // サロゲートペアの途中で切らない。
    private static string TakeWholeCharacters(string text, int length)
    {
        return length > 0 && length < text.Length && char.IsHighSurrogate(text[length - 1])
            ? text[..(length - 1)]
            : text[..length];
    }

    private static string CreateDetectionUrl(int count, string environment, string content)
    {
        return ProjectLinks.NewIssueUrl
            + "&title=" + Uri.EscapeDataString($"[検出された項目] {count}件")
            + "&environment=" + Uri.EscapeDataString(environment)
            + "&content=" + Uri.EscapeDataString(content);
    }

    /// <summary>本文の行の先頭。同じ報告があるかはここで見分ける(<see cref="FeedbackIssueLookup"/>)。</summary>
    public static string FormatLineKey(ResidentDetection detection)
    {
        return $"{detection.Kind} {detection.Id}";
    }

    /// <summary>本文の行の版。閉じたIssueの行が今の版かを見分ける。環境の欄は投稿する人が消せるので、行に持つ。</summary>
    public static string AppVersionField => $"app={AppUpdateService.CurrentVersion.ToString(3)}";

    private static string FormatLine(ResidentDetection detection)
    {
        return $"{FormatLineKey(detection)} {AppVersionField} server={detection.Server} {detection.Details}";
    }

    private static string CreateEnvironmentText()
    {
        return string.Join(
            "\n",
            $"App: {AppUpdateService.CurrentVersion.ToString(3)}",
            $"OS: {RuntimeInformation.OSDescription}",
            $".NET: {RuntimeInformation.FrameworkDescription}",
            $"Npcap: {NpcapVersionProbe.GetVersionString()}",
            $"Language: {LocalizationManager.Instance.CurrentCulture.Name}",
            $"Game server: {GameServerStore.Current}");
    }
}

/// <summary>検出された項目の報告のURLと、本文に入れた行の数。</summary>
internal readonly record struct DetectionReportLink(string Url, int ReportedCount);
