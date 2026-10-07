using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using Microsoft.Win32;
using Serilog;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using Windows.UI.Notifications;

namespace StarResonanceDps.App.Services;

/// <summary>
/// ウィジェットの通知の入口。全体設定(保存前プレビューを含む)の通知方法で、Windows の通知か読み上げに出す。
/// 何を通知するかは各ウィジェットが決め、文章(Windows の通知の本文・読み上げの内容)を渡す。
///
/// <para>
/// 失敗はメッセージを出さずログに残す(ゲーム中にエラーの窓を出さない)。同じ種類のログは <see cref="NotificationFailureLog"/> が間引く。
/// </para>
///
/// <para>
/// Windows の通知は、パッケージ化していないアプリなので、出す前にアプリの名前を
/// <c>HKCU\Software\Classes\AppUserModelId\StarResonanceDps</c> に書く。通知方法をほかに変えて保存したら消す
/// (<see cref="RemoveWindowsNotificationRegistration"/>)。
/// </para>
///
/// <para>
/// Windows の通知の音は、トーストでは消し、アプリが通知音量を掛けて鳴らす(<see cref="SpeechQueue"/>)。
/// トーストの音には音量を指定する手段が無く、Windows の応答不可の間はトーストの音が鳴らないため。
/// 通知音量が 0 なら鳴らさず、読み上げもしない。
/// </para>
/// </summary>
public sealed class NotificationService : IDisposable
{
    private const string AppUserModelId = "StarResonanceDps";
    private const string AppUserModelIdKeyPath = @"Software\Classes\AppUserModelId\" + AppUserModelId;

    private static readonly Lazy<NotificationService> LazyInstance = new(() => new NotificationService());

    private readonly SpeechQueue _speechQueue = new();

    private NotificationService()
    {
    }

    public static NotificationService Instance => LazyInstance.Value;

    /// <summary>アプリの終了。読み上げの順番待ちを止める(一度も使っていなければ何もしない)。</summary>
    public static void Shutdown()
    {
        if (LazyInstance.IsValueCreated)
        {
            LazyInstance.Value.Dispose();
        }
    }

    public void Notify(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var settings = ConfigManager.Instance.GetSettingsSnapshot();
        var hasVolume = settings.NotificationVolume > AppConfigDefaults.NotificationVolumeMin;
        switch (settings.NotificationMethodIndex)
        {
            case AppConfigDefaults.NotificationMethodWindowsIndex:
                ShowWindowsNotification(text);
                if (hasVolume)
                {
                    _speechQueue.Enqueue(new NotificationSoundRequest(settings.NotificationVolume));
                }

                break;
            case AppConfigDefaults.NotificationMethodSpeechIndex:
                if (hasVolume)
                {
                    _speechQueue.Enqueue(new SpeechRequest(
                        text,
                        settings.SpeechVoiceIndex,
                        settings.VoicevoxStyleId,
                        LocalizationManager.Instance.CurrentCulture,
                        settings.NotificationVolume));
                }

                break;
        }
    }

    public void Dispose()
    {
        _speechQueue.Dispose();
    }

    /// <summary>通知方法を Windows の通知以外にして保存したときに呼ぶ。アプリの名前の登録を消す(無ければ何もしない)。</summary>
    public static void RemoveWindowsNotificationRegistration()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(AppUserModelIdKeyPath, throwOnMissingSubKey: false);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException or IOException)
        {
            Log.Warning(ex, "Could not remove the Windows notification registration key={Key}", AppUserModelIdKeyPath);
        }
    }

    private static void ShowWindowsNotification(string text)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 10240))
        {
            NotificationFailureLog.Warning(
                "windows-notification-os",
                null,
                "Windows notifications need Windows 10 or later. The notification was not shown");
            return;
        }

        try
        {
            EnsureWindowsNotificationRegistration();
            ShowToast(text);
        }
        catch (Exception ex) when (ex is COMException or UnauthorizedAccessException or SecurityException or IOException)
        {
            NotificationFailureLog.Warning("windows-notification", ex, "Could not show a Windows notification");
        }
    }

    /// <summary>
    /// アプリの名前(管理画面のタイトル)とアイコンを登録する。値が今と同じなら書かない。
    /// アイコンのファイルが無ければアイコン無しで登録し、そのことをログに残す。
    /// </summary>
    private static void EnsureWindowsNotificationRegistration()
    {
        var displayName = LocalizationManager.Instance.GetString("Window_Manager_Title");
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Images", "ApplicationIcon_32x32.png");

        using var key = Registry.CurrentUser.CreateSubKey(AppUserModelIdKeyPath, writable: true);
        if (!string.Equals(key.GetValue("DisplayName") as string, displayName, StringComparison.Ordinal))
        {
            key.SetValue("DisplayName", displayName);
        }

        if (!File.Exists(iconPath))
        {
            NotificationFailureLog.Warning(
                "windows-notification-icon",
                null,
                "The Windows notification icon is missing path={Path}",
                iconPath);
            return;
        }

        if (!string.Equals(key.GetValue("IconUri") as string, iconPath, StringComparison.OrdinalIgnoreCase))
        {
            key.SetValue("IconUri", iconPath);
        }
    }

    [SupportedOSPlatform("windows10.0.10240")]
    private static void ShowToast(string text)
    {
        var content = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastText01);
        content.GetElementsByTagName("text")[0].AppendChild(content.CreateTextNode(text));

        // 音はアプリが鳴らすので、トーストの音は消す(消さないと応答不可でないときに二重に鳴る)。
        var audio = content.CreateElement("audio");
        audio.SetAttribute("silent", "true");
        content.DocumentElement.AppendChild(audio);

        ToastNotificationManager.CreateToastNotifier(AppUserModelId).Show(new ToastNotification(content));
    }
}

/// <summary>
/// 通知の失敗のログ。ゲーム中に失敗が続いてもログが埋まらないよう、同じ種類は最初の1件の後 1分に1件だけ書き、
/// その間に書かなかった件数を添える(パケット処理の例外のログと同じ決まり)。
/// </summary>
internal static class NotificationFailureLog
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
    private static readonly object Sync = new();
    private static readonly Dictionary<string, (DateTime LastLogged, int Suppressed)> State = new(StringComparer.Ordinal);

    public static void Warning(string kind, Exception? exception, string messageTemplate, params object?[] values)
    {
        int suppressed;
        lock (Sync)
        {
            var now = DateTime.Now;
            if (State.TryGetValue(kind, out var state))
            {
                if (now - state.LastLogged < Interval)
                {
                    State[kind] = (state.LastLogged, state.Suppressed + 1);
                    return;
                }

                suppressed = state.Suppressed;
            }
            else
            {
                suppressed = 0;
            }

            State[kind] = (now, 0);
        }

        Log.Warning(exception, messageTemplate + " (skipped {Suppressed} since the last log)", [.. values, suppressed]);
    }
}
