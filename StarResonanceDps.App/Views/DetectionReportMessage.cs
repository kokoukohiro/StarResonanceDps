using System.Windows;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Services;

namespace StarResonanceDps.App.Views;

/// <summary>フィードバックタブの「選んだ項目を報告」の結果を窓で出す。</summary>
public static class DetectionReportMessage
{
    /// <summary>選んだ項目が1回の報告に入りきらなかった。</summary>
    public static void ShowLimit(Window? owner, int remainingCount)
    {
        var localization = LocalizationManager.Instance;
        MessageWindow.Show(
            owner,
            localization.GetString("Feedback_Report_Title"),
            localization.GetString("Feedback_Limit_Message"),
            localization.Format("Feedback_Limit_Detail", remainingCount));
    }

    /// <summary>選んだ項目は全部、同じ報告がIssueにあった。</summary>
    public static void ShowAlreadyReported(Window? owner)
    {
        var localization = LocalizationManager.Instance;
        MessageWindow.Show(
            owner,
            localization.GetString("Feedback_Report_Title"),
            localization.GetString("Feedback_AlreadyReported_Message"));
    }

    /// <summary>同じ報告があるか確認できなかったので、投稿画面を開かなかった。</summary>
    public static void ShowCheckFailed(Window? owner, FeedbackIssueLookupFailure failure)
    {
        var localization = LocalizationManager.Instance;
        MessageWindow.Show(
            owner,
            localization.GetString("Feedback_CheckError_Title"),
            localization.GetString("Feedback_CheckError_Message"),
            localization.GetString(failure switch
            {
                FeedbackIssueLookupFailure.Network => "GitHub_Error_Network",
                FeedbackIssueLookupFailure.Server => "GitHub_Error_Server",
                FeedbackIssueLookupFailure.RateLimit => "GitHub_Error_RateLimit",
                _ => throw new ArgumentOutOfRangeException(nameof(failure), failure, null)
            }));
    }
}
