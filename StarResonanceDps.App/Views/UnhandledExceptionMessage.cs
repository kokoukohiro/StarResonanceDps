using System;
using System.Windows;
using StarResonanceDps.App.Localization;

namespace StarResonanceDps.App.Views;

/// <summary>
/// どこにも受け手の無い例外で終わることを出す。持ち主はメイン窓(まだ無ければ無し)。
/// 詳細には一番内側の例外の型と文言を入れる(例外の全文はログに書く)。
/// </summary>
public static class UnhandledExceptionMessage
{
    public static void Show(Window? owner, Exception exception)
    {
        var localization = LocalizationManager.Instance;
        MessageWindow.Show(
            owner,
            localization.GetString("Error_Unhandled_Title"),
            localization.GetString("Error_Unhandled_Message"),
            ExceptionDetailFormatter.Format(exception));
    }
}
