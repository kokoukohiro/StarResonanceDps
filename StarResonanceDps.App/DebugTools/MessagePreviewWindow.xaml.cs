using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Windows;
using System.Windows.Input;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Services;
using StarResonanceDps.App.Views;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.PluginSdk;

namespace StarResonanceDps.App.DebugTools;

/// <summary>
/// アプリのメッセージウィンドウ・確認ウィンドウを、エラーを起こさずに出して見た目を確かめる。Debug の構成でだけビルドされる。
/// 本物の呼び出しと同じ文言の鍵の組を並べてある。本物の窓を足したり本文と詳細の分け方を変えたら、ここも直す。
/// キーバインドツールの窓は、プラグインの文言(<see cref="PluginLocalizer"/>)を引き、プラグインの文が通る本体側の表示(<see cref="PluginMessageService"/>)で出す。
/// </summary>
public partial class MessagePreviewWindow : Window
{
    private const int ErrorHotkeyAlreadyRegistered = 1409;

    // 「設定できませんでした(エラー N)」の文を出すための試しの値。
    private const int SampleOtherError = 5;

    // 一部を保存できなかったときの行を出すための試しの値。
    private const int SampleRelativeOffset = 0x120;
    private const uint SampleControllerInputType = 0x00000003u;
    private const uint SampleStateValue = 0u;

    private const string KeybindAssemblyName = "KeybindTool";
    private const string KeybindResourceBaseName = "StarResonanceDps.Plugins.KeybindTool.Properties.Resources";
    private const string KeybindLayoutFileName = "keybind_config.json";

    private readonly PluginMessageService _pluginMessages = new();
    private PluginLocalizer? _keybindTexts;

    public MessagePreviewWindow()
    {
        InitializeComponent();
        Closed += (_, _) => _keybindTexts?.Dispose();
    }

    private static LocalizationManager Localization => LocalizationManager.Instance;

    private PluginLocalizer KeybindTexts => _keybindTexts ??= new PluginLocalizer(
        PluginLocalizationService.Instance,
        LoadKeybindAssembly(),
        KeybindResourceBaseName);

    // キーバインドツールが使う設定ファイルの場所(プラグインの設定は本体がプラグインのフォルダに置く)。
    private static string KeybindLayoutFilePath => Path.Combine(AppDataPaths.PluginsDirectory, KeybindLayoutFileName);

    // 本物: 起動時(実行フォルダの Data に書けないとき)。場所は実際の Data のフォルダ。
    private void DataFolderNotWritable_Click(object sender, RoutedEventArgs e)
    {
        DataFolderNotWritableMessage.Show(this, CombatRuntimePaths.DataDirectory);
    }

    // 本物: 起動時のメイン窓(設定ファイルを読めなかったとき)。場所は実際の AppSettings.json。
    private void SettingsLoadFailed_Click(object sender, RoutedEventArgs e)
    {
        SettingsLoadFailureMessage.Show(this, AppDataPaths.AppSettingsPath);
    }

    // 本物: MainWindow(ホットキーの補助が使えなかったとき)
    private void HotkeyHelperUnavailable_Click(object sender, RoutedEventArgs e)
    {
        MessageWindow.Show(
            this,
            Localization.GetString("Hotkey_Error_Title"),
            Localization.GetString("Hotkey_HelperUnavailable_Message"),
            Localization.GetString("Hotkey_HelperUnavailable_Detail"));
    }

    // 本物: 起動時・全体設定(ホットキーを設定できなかったとき)。全部の操作を既定の割り当てで並べる。
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

    // 本物: 起動時・全体設定「通知設定」(VOICEVOX につながらないとき)
    private void VoicevoxConnectFailed_Click(object sender, RoutedEventArgs e)
    {
        NotificationCheckMessage.ShowVoicevoxFailure(this, VoicevoxResultKind.ConnectFailed);
    }

    // 本物: 起動時・全体設定「通知設定」(VOICEVOX の応答を読めないとき)
    private void VoicevoxBadResponse_Click(object sender, RoutedEventArgs e)
    {
        NotificationCheckMessage.ShowVoicevoxFailure(this, VoicevoxResultKind.BadResponse);
    }

    // 本物: 全体設定「通知設定」(利用規約のリンクを押したとき)。規約の本文は起動中のエンジンから取る。
    // 話者は保存してあるもの、無ければエンジンの一覧の最初。つながらなければ本物と同じく失敗を知らせる。
    private async void VoicevoxPolicy_Click(object sender, RoutedEventArgs e)
    {
        var settings = ConfigManager.Instance.AppConfig.Settings;
        if (settings.VoicevoxSpeakerUuid.Length > 0)
        {
            await NotificationCheckMessage.ShowVoicevoxPolicyAsync(this, settings.VoicevoxSpeakerUuid, settings.VoicevoxSpeakerName);
            return;
        }

        var styles = await VoicevoxClient.GetStylesAsync();
        if (styles.Kind != VoicevoxResultKind.Success || styles.Value!.Count == 0)
        {
            NotificationCheckMessage.ShowVoicevoxFailure(this, styles.Kind);
            return;
        }

        await NotificationCheckMessage.ShowVoicevoxPolicyAsync(this, styles.Value[0].SpeakerUuid, styles.Value[0].SpeakerName);
    }

    // 本物: 起動時・全体設定「通知設定」(表示言語の Windows の音声が無いとき)。言語は今の表示言語。
    private void WindowsVoiceMissing_Click(object sender, RoutedEventArgs e)
    {
        NotificationCheckMessage.ShowWindowsVoiceMissing(this);
    }

    // 本物: キーバインドツール(ファイルの場所を開くとき、フォルダが無い)
    private void KeybindDirectoryNotFound_Click(object sender, RoutedEventArgs e)
    {
        ShowKeybindMessage(
            "Keybind.Message.Title.OpenLocationError",
            "Keybind.Message.DirectoryNotFound",
            Path.GetDirectoryName(KeybindLayoutFilePath));
    }

    // 本物: キーバインドツール(ファイルの場所を開けなかった)
    private void KeybindOpenDirectoryFailed_Click(object sender, RoutedEventArgs e)
    {
        ShowKeybindMessage(
            "Keybind.Message.Title.OpenLocationError",
            "Keybind.Message.OpenDirectoryFailed",
            KeybindTexts["Keybind.Message.OpenDirectoryFailedDetail"]);
    }

    // 本物: キーバインドツール(確認/キャンセルを編集できない設定ファイル)
    private void KeybindPresetUnavailable_Click(object sender, RoutedEventArgs e)
    {
        ShowKeybindMessage(
            "Keybind.Message.Title.PresetUnavailable",
            "Keybind.Message.PresetUnavailableBody",
            KeybindTexts["Keybind.Message.PresetUnavailableDetail"]);
    }

    // 本物: キーバインドツール(設定ファイルを読み込めなかった)
    private void KeybindLoadError_Click(object sender, RoutedEventArgs e)
    {
        ShowKeybindMessage(
            "Keybind.Message.Title.LoadError",
            "Keybind.Message.LoadErrorBody",
            null);
    }

    // 本物: キーバインドツール(キー設定プリセットが無い)
    private void KeybindLayoutFileMissing_Click(object sender, RoutedEventArgs e)
    {
        ShowKeybindMessage(
            "Keybind.Message.Title.LayoutLoadError",
            "Keybind.Message.LayoutFileMissing",
            KeybindLayoutFilePath);
    }

    // 本物: キーバインドツール(キー設定プリセットを読み込んだ)
    private void KeybindLayoutLoaded_Click(object sender, RoutedEventArgs e)
    {
        ShowKeybindMessage(
            "Keybind.Message.Title.LayoutLoaded",
            "Keybind.Message.LayoutLoadedBody",
            KeybindTexts["Keybind.Message.LayoutLoadedDetail"]);
    }

    // 本物: キーバインドツール(キー設定プリセットを読み込めなかった。読み込みのボタンと起動時)
    private void KeybindLayoutLoadFailed_Click(object sender, RoutedEventArgs e)
    {
        ShowKeybindMessage(
            "Keybind.Message.Title.LayoutLoadError",
            "Keybind.Message.LayoutLoadFailedBody",
            KeybindTexts["Keybind.Message.LayoutLoadFailedDetail"]);
    }

    // 本物: キーバインドツール(設定ファイルを選ばずに保存した)
    private void KeybindSelectFileFirst_Click(object sender, RoutedEventArgs e)
    {
        ShowKeybindMessage(
            "Keybind.Message.Title.SaveError",
            "Keybind.Message.SelectFileFirst",
            KeybindTexts["Keybind.Message.SelectFileFirstSaveDetail"]);
    }

    // 本物: キーバインドツール(未設定のキーがある)。一覧はプラグインと同じ書式で2行。
    private void KeybindIncompleteSettings_Click(object sender, RoutedEventArgs e)
    {
        var controller = KeybindTexts["Keybind.InputDevice.Controller"];
        var lines = new[]
        {
            KeybindTexts.Format("Keybind.Message.IncompleteSettingsItem", controller, KeybindTexts["Keybind.Label.Helper1"]),
            KeybindTexts.Format("Keybind.Message.IncompleteSettingsItem", controller, KeybindTexts["Keybind.Label.Helper2"])
        };
        ShowKeybindMessage(
            "Keybind.Message.Title.SaveError",
            "Keybind.Message.IncompleteSettings",
            string.Join(Environment.NewLine, lines));
    }

    // 本物: キーバインドツール(一部のキーを保存できなかった)。一覧はプラグインと同じ書式と末尾の2行。
    private void KeybindPartialSave_Click(object sender, RoutedEventArgs e)
    {
        var lines = new[]
        {
            KeybindTexts.Format(
                "Keybind.Message.PartialSaveTarget",
                KeybindTexts["Keybind.InputDevice.Controller"],
                KeybindTexts["Keybind.Group.Main"],
                KeybindTexts["Keybind.Action.Main.Abilities"]),
            KeybindTexts.Format(
                "Keybind.Message.PartialSaveControllerOffset",
                SampleRelativeOffset,
                SampleControllerInputType,
                SampleStateValue),
            string.Empty,
            KeybindTexts["Keybind.Message.PartialSaveRecovery"],
            KeybindTexts["Keybind.Message.PartialSaveNote"]
        };
        ShowKeybindMessage(
            "Keybind.Message.Title.PartialSave",
            "Keybind.Message.PartialSaveIntro",
            string.Join(Environment.NewLine, lines));
    }

    // 本物: キーバインドツール(キー設定プリセットを作成した)
    private void KeybindSaveCompleted_Click(object sender, RoutedEventArgs e)
    {
        ShowKeybindMessage(
            "Keybind.Message.Title.SaveCompleted",
            "Keybind.Message.SaveCompletedBody",
            KeybindLayoutFilePath);
    }

    // 本物: キーバインドツール(書き込めなかった)
    private void KeybindSaveError_Click(object sender, RoutedEventArgs e)
    {
        ShowKeybindMessage(
            "Keybind.Message.Title.SaveError",
            "Keybind.Message.SaveErrorBody",
            KeybindTexts["Keybind.Message.SaveErrorDetail"]);
    }

    // プラグインの ShowMessageWithDetail と同じく、空の詳細は渡さない。
    private void ShowKeybindMessage(string titleKey, string messageKey, string? detail)
    {
        _pluginMessages.Show(
            KeybindTexts[titleKey],
            KeybindTexts[messageKey],
            string.IsNullOrWhiteSpace(detail) ? null : detail);
    }

    // 本体がプラグインを読むのと同じ場所(既定のロード コンテキスト)から取る。まだ読まれていなければ読む。
    private static Assembly LoadKeybindAssembly()
    {
        return AssemblyLoadContext.Default.Assemblies.FirstOrDefault(assembly => assembly.GetName().Name == KeybindAssemblyName)
            ?? AssemblyLoadContext.Default.LoadFromAssemblyPath(
                Path.Combine(AppDataPaths.PluginsDirectory, KeybindAssemblyName + ".dll"));
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
