using System.Windows;
using StarResonanceDps.App.Localization;

namespace StarResonanceDps.App.Views;

/// <summary>読めなかった・初期化できなかったプラグインの名前を並べて出す(起動時に1回、「＋」で足したとき)。</summary>
public static class PluginLoadFailureMessage
{
    public static void Show(Window? owner, IReadOnlyList<string> pluginNames)
    {
        if (pluginNames.Count == 0)
        {
            return;
        }

        var localization = LocalizationManager.Instance;
        MessageWindow.Show(
            owner,
            localization.GetString("PluginError_Title"),
            localization.GetString("PluginLoadFailed_Message"),
            localization.Format("PluginLoadFailed_Detail", string.Join(Environment.NewLine, pluginNames)));
    }
}
