using System.Windows;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Services;

namespace StarResonanceDps.App.Views;

/// <summary>更新の確認や置き換えができなかったことを出す。詳細は理由か直し方で、zipの取得が直し方ならリリースのページのリンクも入れる。</summary>
public static class UpdateFailureMessage
{
    public static void ShowCheckFailed(Window? owner, AppUpdateFailure failure)
    {
        ShowFailure(owner, "Update_CheckError_Message", failure);
    }

    public static void Show(Window? owner, AppUpdateFailure failure)
    {
        ShowFailure(owner, "Update_Error_Message", failure);
    }

    /// <summary>置き換えは済んだが、新しいバージョンを起動できなかった。</summary>
    public static void ShowRestartFailed(Window? owner)
    {
        var localization = LocalizationManager.Instance;
        MessageWindow.Show(
            owner,
            localization.GetString("Update_Error_Title"),
            localization.GetString("Update_RestartFailed_Message"),
            localization.GetString("Update_RestartFailed_Detail"));
    }

    private static void ShowFailure(Window? owner, string messageResourceKey, AppUpdateFailure failure)
    {
        var localization = LocalizationManager.Instance;
        var title = localization.GetString("Update_Error_Title");
        var message = localization.GetString(messageResourceKey);
        var detail = localization.GetString(GetDetailResourceKey(failure));

        if (failure is AppUpdateFailure.InvalidRelease or AppUpdateFailure.NotFromZip or AppUpdateFailure.RestoreFailed)
        {
            MessageWindow.Show(
                owner,
                title,
                message,
                [
                    new MessageDetailPart(detail + Environment.NewLine),
                    new MessageDetailPart(localization.GetString("Update_OpenReleasesPage"), ProjectLinks.ReleasesUrl)
                ]);
            return;
        }

        MessageWindow.Show(owner, title, message, detail);
    }

    private static string GetDetailResourceKey(AppUpdateFailure failure)
    {
        return failure switch
        {
            AppUpdateFailure.Network => "Update_Error_Network",
            AppUpdateFailure.RateLimit => "Update_Error_RateLimit",
            AppUpdateFailure.InvalidRelease => "Update_Error_InvalidRelease",
            AppUpdateFailure.NotFromZip => "Update_Error_NotFromZip",
            AppUpdateFailure.Corrupt => "Update_Error_Corrupt",
            AppUpdateFailure.Write => "Update_Error_Write",
            AppUpdateFailure.Start => "Update_Error_Start",
            AppUpdateFailure.Replace => "Update_Error_Replace",
            AppUpdateFailure.RestoreFailed => "Update_Error_RestoreFailed",
            _ => throw new ArgumentOutOfRangeException(nameof(failure), failure, null)
        };
    }
}
