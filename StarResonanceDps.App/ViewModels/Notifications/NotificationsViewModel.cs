using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Services;

namespace StarResonanceDps.App.ViewModels.Notifications;

/// <summary>
/// メイン窓の通知タブ。中央に項目の一覧(名前とスイッチ)、右に選んだ項目の文章と設定を出す。
///
/// <para>
/// 保存前プレビューは持たず、変えたらその場で保存する(<see cref="ConfigManager.SaveNotifications"/>)。
/// 文章と出現の対象の名前は打ち終わってから <see cref="TextSaveDelay"/> で保存し、タブを離れたとき・アプリを閉じるときは待たずに保存する(<see cref="FlushPendingSave"/>)。
/// Windows の通知を使うオンの項目が無くなったら、Windows の通知の登録を消す(<see cref="NotificationService.RemoveWindowsNotificationRegistration"/>)。
/// </para>
/// </summary>
public sealed partial class NotificationsViewModel : ObservableObject
{
    /// <summary>文章を打ち終わってから保存するまでの待ち。推移グラフのホイールの保存と同じ。</summary>
    private static readonly TimeSpan TextSaveDelay = TimeSpan.FromMilliseconds(300);

    private readonly DispatcherTimer _textSaveTimer;
    private bool _usesWindowsNotification;

    [ObservableProperty]
    private NotificationItemViewModel? _selectedItem;

    public NotificationsViewModel()
    {
        Items =
        [
            new NotificationItemViewModel(
                NotificationKind.EntryConfirmation,
                "Notification_EntryConfirmation",
                [("Content", "Settings_PlayerListNotification_Field_Content", "{Content}")]),
            new NotificationItemViewModel(
                NotificationKind.HealthLow,
                "Settings_PlayerListNotification_HealthLow",
                [("PlayerName", "Settings_PlayerInfo_Field_Name", "{PlayerName}")]),
            new NotificationItemViewModel(
                NotificationKind.PlayerAppeared,
                "Notification_PlayerAppeared",
                [("PlayerName", "Settings_PlayerInfo_Field_Name", "{PlayerName}")]),
            new NotificationItemViewModel(
                NotificationKind.EntityAppeared,
                "Notification_EntityAppeared",
                [("EntityName", "Settings_EntityInfo_Field_Name", "{EntityName}")]),
            new NotificationItemViewModel(
                NotificationKind.Targeted,
                "Notification_Targeted",
                [
                    ("PlayerName", "Settings_PlayerInfo_Field_Name", "{PlayerName}"),
                    ("EntityName", "Settings_EntityInfo_Field_Name", "{EntityName}")
                ]),
            new NotificationItemViewModel(
                NotificationKind.BuffExpired,
                "Settings_BuffCardNotification_Expired",
                [
                    ("BuffName", "Settings_BuffInfo_Field_BuffName", "{BuffName}"),
                    ("TargetName", "Settings_BuffInfo_Field_Name", "{TargetName}"),
                    ("CasterName", "Settings_BuffInfo_Field_Caster", "{CasterName}")
                ]),
            new NotificationItemViewModel(
                NotificationKind.CuisinePotionLow,
                "Settings_BuffCardNotification_CuisinePotionLow",
                [
                    ("BuffName", "Settings_BuffInfo_Field_BuffName", "{BuffName}"),
                    ("TargetName", "Settings_BuffInfo_Field_Name", "{TargetName}")
                ]),
            new NotificationItemViewModel(
                NotificationKind.TelegraphedSkill,
                "Settings_TakenDamageLogNotification_TelegraphedSkill",
                [
                    ("EntityName", "Settings_EntityInfo_Field_Name", "{EntityName}"),
                    ("SkillName", "Metric_SkillName", "{SkillName}")
                ]),
            new NotificationItemViewModel(
                NotificationKind.EntityCast,
                "Notification_EntityCast",
                [("EntityName", "Settings_EntityInfo_Field_Name", "{EntityName}")])
        ];

        var notifications = ConfigManager.Instance.GetNotificationsSnapshot();
        foreach (var item in Items)
        {
            item.Load(notifications.Get(item.Kind));
            item.Changed += Item_Changed;
            item.TextChanged += Item_TextChanged;
            item.PropertyChanged += Item_PropertyChanged;
            item.SpeechVoiceCheckRequested += Item_SpeechVoiceCheckRequested;
        }

        _usesWindowsNotification = notifications.AnyEnabled(item => item.IsWindowsNotification);
        _textSaveTimer = new DispatcherTimer
        {
            Interval = TextSaveDelay
        };
        _textSaveTimer.Tick += TextSaveTimer_Tick;

        Items[0].IsSelected = true;
        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;
    }

    /// <summary>一覧の項目。並びは <see cref="NotificationKind"/> の並び。</summary>
    public IReadOnlyList<NotificationItemViewModel> Items { get; }

    /// <summary>読み上げの声を確かめる時機。画面が確かめてメッセージを出す(引数は確かめる項目)。</summary>
    public event EventHandler<NotificationItemViewModel>? SpeechVoiceCheckRequested;

    /// <summary>
    /// エンジンから話者の一覧を取り直して、全部の項目の選択肢を作り直す。失敗したら選択肢は今のままにし、結果の種類を返す
    /// (メッセージを出すかは画面が決める。出すのは読み上げで VOICEVOX を選んだときだけ)。
    /// </summary>
    public async Task<VoicevoxResultKind> RefreshVoicevoxSpeakersAsync()
    {
        var result = await VoicevoxClient.GetStylesAsync();
        if (result.Kind == VoicevoxResultKind.Success)
        {
            foreach (var item in Items)
            {
                item.ApplyFetchedVoicevoxStyles(result.Value!);
            }
        }

        return result.Kind;
    }

    /// <summary>待っている文章の保存があれば、待たずに保存する(タブを離れたとき・アプリを閉じるとき)。</summary>
    public void FlushPendingSave()
    {
        if (_textSaveTimer.IsEnabled)
        {
            Save();
        }
    }

    private void Item_Changed(object? sender, EventArgs e)
    {
        Save();
    }

    private void Item_TextChanged(object? sender, EventArgs e)
    {
        _textSaveTimer.Stop();
        _textSaveTimer.Start();
    }

    private void TextSaveTimer_Tick(object? sender, EventArgs e)
    {
        Save();
    }

    private void Item_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(NotificationItemViewModel.IsSelected)
            || sender is not NotificationItemViewModel { IsSelected: true } item)
        {
            return;
        }

        SelectedItem = item;
        foreach (var other in Items)
        {
            if (!ReferenceEquals(other, item))
            {
                other.IsSelected = false;
            }
        }
    }

    private void Save()
    {
        _textSaveTimer.Stop();

        var notifications = new NotificationsConfig
        {
            Items = Items.ToDictionary(item => item.Kind, item => item.CreateConfig())
        };
        ConfigManager.Instance.SaveNotifications(notifications);

        // Windows の通知のためのアプリの名前とアイコンの登録は、Windows の通知を使うオンの項目が無くなったら
        // 通知センターのこのアプリの通知と一緒に消す(使うときに通知の処理が書く)。
        var usesWindowsNotification = notifications.AnyEnabled(item => item.IsWindowsNotification);
        if (_usesWindowsNotification && !usesWindowsNotification)
        {
            NotificationService.RemoveWindowsNotificationRegistration();
        }

        _usesWindowsNotification = usesWindowsNotification;
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        foreach (var item in Items)
        {
            item.RefreshLocalizedText();
        }
    }

    private void Item_SpeechVoiceCheckRequested(object? sender, EventArgs e)
    {
        if (sender is NotificationItemViewModel item)
        {
            SpeechVoiceCheckRequested?.Invoke(this, item);
        }
    }
}
