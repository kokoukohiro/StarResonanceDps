using System.Windows;
using StarResonanceDps.App.Localization;

namespace StarResonanceDps.App.Views;

/// <summary>新しいバージョンがあるときに、更新するかを聞く。詳細にリリースノートのリンクを入れる。</summary>
public static class UpdateConfirmMessage
{
    public static bool Show(Window owner, Version version, string releaseNotesUrl)
    {
        var localization = LocalizationManager.Instance;
        return ConfirmWindow.ShowText(
            owner,
            localization.GetString("Update_Title"),
            localization.Format("Update_Confirm_Message", version.ToString(3)),
            [
                new MessageDetailPart(localization.GetString("Update_Confirm_Detail") + Environment.NewLine),
                new MessageDetailPart(localization.GetString("Update_Confirm_ReleaseNotes"), releaseNotesUrl)
            ]);
    }
}
