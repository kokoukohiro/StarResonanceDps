using System.Windows;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Services;

namespace StarResonanceDps.App.Views;

/// <summary>ホットキーを登録できなかったことをメッセージウィンドウで出す。1件でも複数でも「項目名: 理由」を1行ずつ並べる。</summary>
public static class HotkeyRegistrationFailureMessage
{
    public static void Show(Window? owner, IReadOnlyList<HotkeyRegistrationFailure> failures)
    {
        if (failures.Count == 0)
        {
            return;
        }

        MessageWindow.Show(
            owner,
            LocalizationManager.Instance.GetString("Hotkey_RegisterFailed_Title"),
            string.Join(Environment.NewLine, failures.Select(HotkeyText.FormatFailure)));
    }
}
