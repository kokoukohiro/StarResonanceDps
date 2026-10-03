using System.Windows;
using System.Windows.Input;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Services;
using StarResonanceDps.App.Views;

namespace StarResonanceDps.Debug;

/// <summary>
/// アプリのメッセージウィンドウ・確認ウィンドウを、エラーを起こさずに出して見た目を確かめる。Debug ビルドだけで使う。
/// 本物の呼び出しと同じ文言の鍵の組を並べてある。本物の窓を足したり本文と詳細の分け方を変えたら、ここも直す。
/// </summary>
public partial class MessagePreviewWindow : Window
{
    private const int ErrorHotkeyAlreadyRegistered = 1409;

    // 「設定できませんでした(エラー N)」の文を出すための試しの値。
    private const int SampleOtherError = 5;

    public MessagePreviewWindow()
    {
        InitializeComponent();
    }

    private static LocalizationManager Localization => LocalizationManager.Instance;

    // 本物: MainWindow(ホットキーの補助が使えなかったとき)
    private void HotkeyHelperUnavailable_Click(object sender, RoutedEventArgs e)
    {
        MessageWindow.Show(
            this,
            Localization.GetString("Hotkey_Error_Title"),
            Localization.GetString("Hotkey_HelperUnavailable_Message"),
            Localization.GetString("Hotkey_HelperUnavailable_Detail"));
    }

    // 本物: 起動時・全体設定(ホットキーを設定できなかったとき)。5つの操作を既定の割り当てで並べる。
    private void HotkeyRegisterFailed_Click(object sender, RoutedEventArgs e)
    {
        var bindings = AppConfigDefaults.CreateDefaultHotkeys();
        var failures = Enum.GetValues<HotkeyAction>()
            .Select((action, index) => new HotkeyRegistrationFailure(
                action,
                bindings.Get(action),
                index % 2 == 0 ? ErrorHotkeyAlreadyRegistered : SampleOtherError))
            .ToList();
        HotkeyRegistrationFailureMessage.Show(this, failures);
    }

    // 本物: 全体設定(Npcap が入っていないとき)
    private void NpcapMissing_Click(object sender, RoutedEventArgs e)
    {
        _ = ConfirmWindow.ShowText(
            this,
            Localization.GetString("Confirm_NpcapUpdate_Title"),
            Localization.GetString("Confirm_NpcapMissing_Message"),
            Localization.GetString("Confirm_NpcapMissing_Detail"));
    }

    // 本物: 全体設定(Npcap が古いとき)。バージョンはこの PC に入っている Npcap のもの。
    private void NpcapOutdated_Click(object sender, RoutedEventArgs e)
    {
        _ = ConfirmWindow.ShowText(
            this,
            Localization.GetString("Confirm_NpcapUpdate_Title"),
            Localization.GetString("Confirm_NpcapOutdated_Message"),
            Localization.Format("Confirm_NpcapOutdated_Detail", NpcapVersionProbe.GetVersion()));
    }

    // 本物: 全体設定・ウィジェット設定(保存せずに閉じるとき)
    private void DiscardUnsaved_Click(object sender, RoutedEventArgs e)
    {
        _ = ConfirmWindow.Show(
            this,
            "Confirm_DiscardUnsaved_Title",
            "Confirm_DiscardUnsaved_Message",
            "Confirm_DiscardUnsaved_Detail");
    }

    // 本物: 全体設定(初期化)
    private void ResetSettings_Click(object sender, RoutedEventArgs e)
    {
        _ = ConfirmWindow.Show(
            this,
            "Confirm_ResetSettings_Title",
            "Confirm_ResetSettings_Message",
            "Confirm_ResetSettings_Detail");
    }

    // 本物: ウィジェット設定(初期化)
    private void ResetWidgetSettings_Click(object sender, RoutedEventArgs e)
    {
        _ = ConfirmWindow.Show(
            this,
            "Confirm_ResetWidgetSettings_Title",
            "Confirm_ResetWidgetSettings_Message",
            "Confirm_ResetWidgetSettings_Detail");
    }

    // 本物: プレイヤー情報(画像を保存できなかったとき)
    private void SaveImageFailed_Click(object sender, RoutedEventArgs e)
    {
        MessageWindow.Show(
            this,
            Localization.GetString("PlayerInfo_SaveImageFailed_Title"),
            Localization.GetString("PlayerInfo_SaveImageFailed_Message"),
            Localization.GetString("PlayerInfo_SaveImageFailed_Detail"));
    }

    // 本物: プレイヤー情報(画像をコピーできなかったとき)
    private void CopyImageFailed_Click(object sender, RoutedEventArgs e)
    {
        MessageWindow.Show(
            this,
            Localization.GetString("PlayerInfo_CopyImageFailed_Title"),
            Localization.GetString("PlayerInfo_CopyImageFailed_Message"),
            Localization.GetString("PlayerInfo_CopyImageFailed_Detail"));
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
