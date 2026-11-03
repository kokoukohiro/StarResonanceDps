using System.Windows;
using StarResonanceDps.App.Localization;

namespace StarResonanceDps.App.Views;

/// <summary>プラグインの読み込み・初期化・中身の作成に失敗して、プラグインの窓を開けなかったことを出す。</summary>
public static class PluginOpenFailureMessage
{
    public static void Show(Window? owner)
    {
        var localization = LocalizationManager.Instance;
        MessageWindow.Show(
            owner,
            localization.GetString("PluginOpenFailed_Title"),
            localization.GetString("PluginOpenFailed_Message"),
            localization.GetString("PluginOpenFailed_Detail"));
    }
}
