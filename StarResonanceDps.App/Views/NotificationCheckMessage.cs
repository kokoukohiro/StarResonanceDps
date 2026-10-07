using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Services;

namespace StarResonanceDps.App.Views;

/// <summary>
/// 読み上げの声を使えるかを確かめ、使えなければメッセージウィンドウで知らせる。
/// 使うのは全体設定(読み上げにした・声を選んだ・言語を変えた・利用規約のリンクを押した)と起動時だけ。ゲーム中(読み上げようとしたとき)と、話者・スタイルのリストを開いたときには出さない。
/// 確かめている間にオーナーの窓が閉じたら出さない。
/// </summary>
public static partial class NotificationCheckMessage
{
    /// <summary>
    /// 通知方式が読み上げなら、選んである声を確かめる。読み上げでなければ何もしない。VOICEVOX はエンジンに問い合わせるので待つ。
    /// </summary>
    public static async Task CheckSpeechVoiceAsync(Window owner, int notificationMethodIndex, int speechVoiceIndex)
    {
        if (notificationMethodIndex != AppConfigDefaults.NotificationMethodSpeechIndex)
        {
            return;
        }

        if (speechVoiceIndex == AppConfigDefaults.SpeechVoiceWindowsIndex)
        {
            CheckWindowsVoice(owner);
        }
        else if (speechVoiceIndex == AppConfigDefaults.SpeechVoiceVoicevoxIndex)
        {
            var result = await VoicevoxClient.CheckConnectionAsync();
            ShowVoicevoxFailure(owner, result);
        }
    }

    /// <summary>表示言語の Windows の音声があるかを確かめ、無ければ知らせる。</summary>
    public static void CheckWindowsVoice(Window owner)
    {
        if (WindowsVoiceSelector.FindVoice(LocalizationManager.Instance.CurrentCulture) is not null || !owner.IsLoaded)
        {
            return;
        }

        ShowWindowsVoiceMissing(owner);
    }

    /// <summary>表示言語の Windows の音声が無いことを知らせる。</summary>
    public static void ShowWindowsVoiceMissing(Window owner)
    {
        var localization = LocalizationManager.Instance;
        var languageName = localization.GetString(GetLanguageNameKey(localization.CurrentCulture));
        MessageWindow.Show(
            owner,
            localization.GetString("WindowsVoice_Missing_Title"),
            localization.Format("WindowsVoice_Missing_Message", languageName),
            localization.Format("WindowsVoice_Missing_Detail", languageName));
    }

    /// <summary>VOICEVOX への問い合わせが失敗していたら知らせる。成功なら何もしない。</summary>
    public static void ShowVoicevoxFailure(Window owner, VoicevoxResultKind result)
    {
        if (result == VoicevoxResultKind.Success || !owner.IsLoaded)
        {
            return;
        }

        var localization = LocalizationManager.Instance;
        var title = localization.GetString("Voicevox_Error_Title");
        if (result == VoicevoxResultKind.ConnectFailed)
        {
            // 詳細の最後の行にダウンロードのページを押せるリンクで出す。
            MessageWindow.Show(
                owner,
                title,
                localization.GetString("Voicevox_ConnectFailed_Message"),
                [
                    new MessageDetailPart(localization.GetString("Voicevox_ConnectFailed_Detail") + "\n"),
                    new MessageDetailPart(VoicevoxClient.DownloadPageUrl, VoicevoxClient.DownloadPageUrl)
                ]);
        }
        else
        {
            MessageWindow.Show(owner, title, localization.GetString("Voicevox_BadResponse_Message"));
        }
    }

    /// <summary>
    /// 話者の規約の本文をエンジンから取って出す。本文は Markdown なので、行末の改行の印は落とし、
    /// [文](URL) は「文」を、そのまま書かれた URL は URL の文字を、押せるリンクにする。
    /// </summary>
    public static async Task ShowVoicevoxPolicyAsync(Window owner, string speakerUuid, string speakerName)
    {
        var result = await VoicevoxClient.GetPolicyAsync(speakerUuid);
        if (result.Kind != VoicevoxResultKind.Success)
        {
            ShowVoicevoxFailure(owner, result.Kind);
            return;
        }

        if (!owner.IsLoaded)
        {
            return;
        }

        var localization = LocalizationManager.Instance;
        MessageWindow.Show(
            owner,
            localization.Format("Voicevox_Policy_Title", speakerName),
            VoicevoxCredit.Get(speakerUuid, speakerName),
            ToDetailParts(result.Value!));
    }

    private static List<MessageDetailPart> ToDetailParts(string markdown)
    {
        var lines = markdown.Replace("\r\n", "\n").Split('\n').Select(line => line.TrimEnd());
        var text = string.Join("\n", lines).Trim();

        var parts = new List<MessageDetailPart>();
        var position = 0;
        foreach (Match match in LinkRegex().Matches(text))
        {
            if (match.Index > position)
            {
                parts.Add(new MessageDetailPart(text[position..match.Index]));
            }

            if (match.Groups["url"].Success)
            {
                var label = match.Groups["label"].Value;
                var url = match.Groups["url"].Value;
                parts.Add(new MessageDetailPart(label.Length > 0 ? label : url, url));
            }
            else
            {
                parts.Add(new MessageDetailPart(match.Value, match.Value));
            }

            position = match.Index + match.Length;
        }

        if (position < text.Length)
        {
            parts.Add(new MessageDetailPart(text[position..]));
        }

        return parts;
    }

    private static string GetLanguageNameKey(CultureInfo culture)
    {
        return culture.TwoLetterISOLanguageName switch
        {
            "ja" => "Settings_Language_Japanese",
            "ko" => "Settings_Language_Korean",
            "zh" => "Settings_Language_Chinese",
            _ => "Settings_Language_English"
        };
    }

    // [文](URL) か、そのまま書かれた URL。URL は空白か半角の括弧で終わる(ホストやパスの日本語は含める)。
    [GeneratedRegex(@"\[(?<label>[^\]]*)\]\((?<url>[^)\s]+)\)|https?://[^\s()]+")]
    private static partial Regex LinkRegex();
}
