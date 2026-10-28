using System.Text.Json.Serialization;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.Config;

/// <summary>通知タブの項目。並びは通知タブの一覧の並び(メイン窓のウィジェットの順)。</summary>
public enum NotificationKind
{
    EntryConfirmation,
    HealthLow,
    PlayerAppeared,
    EntityAppeared,
    Targeted,
    BuffExpired,
    CuisinePotionLow,
    TelegraphedSkill,
    EntityCast
}

/// <summary>
/// 通知タブの設定。項目ごとに、通知するか・文章・出し方を持つ。全体設定(<see cref="SettingsConfig"/>)とは別に保存する
/// (全体設定の窓の保存がこの値を巻き戻さないため)。
/// </summary>
public sealed class NotificationsConfig
{
    public Dictionary<NotificationKind, NotificationItemConfig> Items { get; set; } = AppConfigDefaults.CreateNotificationItems();

    public NotificationItemConfig Get(NotificationKind kind)
    {
        return Items[kind];
    }

    /// <summary>オンの項目のどれかが、その出し方を使っているか。</summary>
    public bool AnyEnabled(Func<NotificationItemConfig, bool> predicate)
    {
        return Items.Values.Any(item => item.IsEnabled && predicate(item));
    }

    public NotificationsConfig Clone()
    {
        return new NotificationsConfig
        {
            Items = Items is null
                ? AppConfigDefaults.CreateNotificationItems()
                : Items.ToDictionary(pair => pair.Key, pair => pair.Value?.Clone() ?? AppConfigDefaults.CreateNotificationItem())
        };
    }
}

public sealed class NotificationItemConfig
{
    public bool IsEnabled { get; set; }

    /// <summary>通知の文章。null は既定のまま(表示言語の既定の文)。空欄は保存しない(既定に戻す)。</summary>
    public string? FormatString { get; set; }

    /// <summary>出し方。0=Windows の通知 / 1=読み上げ(<see cref="AppConfigDefaults"/> の定数)。通知しないはスイッチで決める。</summary>
    public int MethodIndex { get; set; } = AppConfigDefaults.NotificationMethodWindowsIndex;

    /// <summary>
    /// 通知音量 0〜100(%)。Windows の通知の音と読み上げの両方に掛ける(掛け方は <c>SpeechQueue</c>)。
    /// 100 は Windows の通知の音と Windows の音声では元の大きさ、VOICEVOX では元の大きさを上げた大きさ(50 が元の大きさ)。
    /// </summary>
    public int Volume { get; set; } = AppConfigDefaults.NotificationVolumeDefault;

    /// <summary>読み上げ方法(出し方が読み上げのときの声)。0=Windows の音声 / 1=VOICEVOX(<see cref="AppConfigDefaults"/> の定数)。</summary>
    public int SpeechVoiceIndex { get; set; } = AppConfigDefaults.SpeechVoiceWindowsIndex;

    // VOICEVOX の話者。名前も持つのは、エンジンが起動していないときにも選んだ話者と利用規約のリンクを出すため。
    public string VoicevoxSpeakerUuid { get; set; } = string.Empty;
    public string VoicevoxSpeakerName { get; set; } = string.Empty;

    /// <summary>VOICEVOX のスタイルの番号。0 も使われる番号なので、未選択は null。</summary>
    public int? VoicevoxStyleId { get; set; }

    public string VoicevoxStyleName { get; set; } = string.Empty;

    /// <summary>通知する人(<see cref="PartyDisplayMode"/> の番号)。使うのは HP低下 だけ。</summary>
    public int TargetModeIndex { get; set; } = (int)PartyDisplayMode.All;

    /// <summary>
    /// 通知する人(<see cref="AppConfigDefaults.TargetedPlayerAllIndex"/> など)。使うのは 狙われ状態 だけ。
    /// </summary>
    public int TargetedPlayerModeIndex { get; set; } = AppConfigDefaults.DefaultTargetedPlayerModeIndex;

    /// <summary>
    /// 通知する敵の種類(<see cref="AppConfigDefaults.TargetEntityBossOnlyIndex"/> など)。使うのは 狙われ状態 だけ。
    /// </summary>
    public int TargetEntityModeIndex { get; set; } = AppConfigDefaults.DefaultTargetEntityModeIndex;

    /// <summary>
    /// 狙われ状態が何秒続いたら出すかの番号(<see cref="AppConfigDefaults.TargetedDurationSeconds"/> の添字)。
    /// 使うのは 狙われ状態 だけ。
    /// </summary>
    public int TargetedConditionIndex { get; set; } = AppConfigDefaults.DefaultTargetedConditionIndex;

    /// <summary>HP低下とみなす割合の番号(<see cref="AppConfigDefaults.HealthLowThresholdPercents"/> の添字)。使うのは HP低下 だけ。</summary>
    public int HealthLowThresholdIndex { get; set; } = AppConfigDefaults.DefaultHealthLowThresholdIndex;

    /// <summary>HP低下が何秒続いたら出すかの番号(<see cref="AppConfigDefaults.HealthLowDurationSeconds"/> の添字)。使うのは HP低下 だけ。</summary>
    public int HealthLowConditionIndex { get; set; } = AppConfigDefaults.DefaultHealthLowConditionIndex;

    /// <summary>
    /// 残り何分を下回ったら出すかの番号(<see cref="AppConfigDefaults.CuisinePotionLowMinutes"/> の添字)。使うのは 薬剤・料理効果切れ だけ。
    /// </summary>
    public int CuisinePotionConditionIndex { get; set; } = AppConfigDefaults.DefaultCuisinePotionConditionIndex;

    /// <summary>
    /// 予告のバーが終わる何秒前に出すかの番号(<see cref="AppConfigDefaults.TelegraphedSkillLeadSeconds"/> の添字)。使うのは 予兆技 の予告だけ(警告は警告が出たときに出す)。
    /// </summary>
    public int TelegraphedSkillConditionIndex { get; set; } = AppConfigDefaults.DefaultTelegraphedSkillConditionIndex;

    /// <summary>
    /// 何秒以上の詠唱なら出すかの番号(<see cref="AppConfigDefaults.EntityCastMinimumSeconds"/> の添字)。使うのは エンティティ詠唱 だけ。
    /// </summary>
    public int EntityCastConditionIndex { get; set; } = AppConfigDefaults.DefaultEntityCastConditionIndex;

    /// <summary>
    /// 出現を知らせる相手の名前。プレイヤー出現はゲームの中のプレイヤー名、エンティティ出現は表示言語のエンティティ名。
    /// 空なら通知しない。使うのは プレイヤー出現 と エンティティ出現 だけ。
    /// </summary>
    public string TargetName { get; set; } = string.Empty;

    [JsonIgnore]
    public bool IsWindowsNotification => MethodIndex == AppConfigDefaults.NotificationMethodWindowsIndex;

    [JsonIgnore]
    public bool IsSpeech => MethodIndex == AppConfigDefaults.NotificationMethodSpeechIndex;

    [JsonIgnore]
    public bool UsesWindowsVoice => IsSpeech && SpeechVoiceIndex == AppConfigDefaults.SpeechVoiceWindowsIndex;

    [JsonIgnore]
    public bool UsesVoicevox => IsSpeech && SpeechVoiceIndex == AppConfigDefaults.SpeechVoiceVoicevoxIndex;

    public NotificationItemConfig Clone()
    {
        return new NotificationItemConfig
        {
            IsEnabled = IsEnabled,
            FormatString = FormatString,
            MethodIndex = MethodIndex,
            Volume = Volume,
            SpeechVoiceIndex = SpeechVoiceIndex,
            VoicevoxSpeakerUuid = VoicevoxSpeakerUuid,
            VoicevoxSpeakerName = VoicevoxSpeakerName,
            VoicevoxStyleId = VoicevoxStyleId,
            VoicevoxStyleName = VoicevoxStyleName,
            TargetModeIndex = TargetModeIndex,
            TargetedPlayerModeIndex = TargetedPlayerModeIndex,
            TargetEntityModeIndex = TargetEntityModeIndex,
            TargetedConditionIndex = TargetedConditionIndex,
            HealthLowThresholdIndex = HealthLowThresholdIndex,
            HealthLowConditionIndex = HealthLowConditionIndex,
            CuisinePotionConditionIndex = CuisinePotionConditionIndex,
            TelegraphedSkillConditionIndex = TelegraphedSkillConditionIndex,
            EntityCastConditionIndex = EntityCastConditionIndex,
            TargetName = TargetName
        };
    }
}
