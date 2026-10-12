using System;
using System.Windows;
using StarResonanceDps.App.Localization;

namespace StarResonanceDps.App.Views;

/// <summary>
/// 起動時の初期化(DB・データの表・キャプチャ)で例外が出たので起動できないことを出す。
/// 詳細には一番内側の例外の型と文言を入れる(例外の全文はログに書く)。
/// </summary>
public static class StartupInitializationFailedMessage
{
    public static void Show(Window owner, Exception exception)
    {
        var localization = LocalizationManager.Instance;
        MessageWindow.Show(
            owner,
            localization.GetString("Startup_Error_Title"),
            localization.GetString("Startup_InitializationFailed_Message"),
            ExceptionDetailFormatter.Format(exception));
    }
}
