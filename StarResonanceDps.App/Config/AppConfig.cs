using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.CombatRuntime.DataTypes;

namespace StarResonanceDps.App.Config;

public sealed class AppConfig
{
    public WindowBounds? StartUpState { get; set; }

    public SettingsConfig Settings { get; set; } = AppConfigDefaults.CreateSettings();

    public ColorPickerConfig ColorPicker { get; set; } = AppConfigDefaults.CreateColorPicker();
}

public sealed class WindowBounds
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

public sealed class ColorPickerConfig
{
    public List<string> RecentColors { get; set; } = [];

    public ColorPickerConfig Clone()
    {
        return new ColorPickerConfig
        {
            RecentColors = RecentColors is null ? [] : [.. RecentColors]
        };
    }
}

public sealed class SettingsConfig
{
    public int LanguageIndex { get; set; }

    // --- 基本設定(キャプチャ) ---
    // Core は AppSettings.json を読めないので、起動時と保存時に
    // CombatRuntimeSettings.Apply で流し込む。

    public string NetCaptureDeviceName { get; set; } = CombatRuntimeSettings.AutomaticNetCaptureDeviceName;
    public EGameCapturePreference GameCapturePreference { get; set; } = EGameCapturePreference.Auto;
    public string GameCaptureCustomExeName { get; set; } = string.Empty;

    // --- 集計設定 ---

    /// <summary>アステルリーズ(街)では戦闘の時計を立てず、何も記録しない。</summary>
    public bool StopAggregationInAsterleeds { get; set; } = true;
    public bool SplitEncountersOnNewPhases { get; set; } = true;
    public bool KeepPastEncounterInMeterUntilNextDamage { get; set; } = true;
    public bool ClearHistorySelectionOnNextEvent { get; set; } = true;

    /// <summary>戦闘履歴を残す最大の件数。<b>0 は無限。</b></summary>
    public int DatabaseMaxEncounterCount { get; set; } = 99;

    /// <summary>計測の長さ(秒)。範囲は <see cref="AppConfigDefaults"/> の定数。計測を始めた時点の値で測る。</summary>
    public int BenchmarkDurationSeconds { get; set; } = AppConfigDefaults.BenchmarkDurationDefaultSeconds;

    /// <summary>計測で、自分が最初にダメージを与えた敵へのダメージだけを数える。計測を始めた時点の値で測る。</summary>
    public bool BenchmarkFirstTargetOnly { get; set; } = true;

    /// <summary>自動一時停止の秒数。選択肢は <see cref="AppConfigDefaults.CombatExitChoicesSeconds"/>、<b>0 は一時停止しない。</b></summary>
    public int CombatExitSeconds { get; set; } = AppConfigDefaults.CombatExitDefaultSeconds;
    public int NumberDisplayFormatIndex { get; set; }
    public int PlayerNameDisplayModeIndex { get; set; }
    public int InternalIdDisplayModeIndex { get; set; }

    /// <summary>
    /// ウィジェットの窓を最前面に出す条件。0=常に最前面 / 1=ピン留め時のみ最前面。
    /// 反映は <c>WidgetWindow.ApplyTopmost</c>。
    /// </summary>
    public int WidgetWindowTopmostModeIndex { get; set; } = AppConfigDefaults.AlwaysWidgetWindowTopmostModeIndex;
    public int WindowColorIndex { get; set; }
    public List<string> WindowColors { get; set; } = AppConfigDefaults.CreateDefaultWindowColors();

    // --- ホットキー ---

    /// <summary>
    /// ホットキーの割り当て。登録は <c>GlobalHotkeyService</c> が起動時と保存時に行い、
    /// 全体設定で変えている間はその割り当てでプレビューする(保存せずに閉じたら戻す)。
    /// </summary>
    public HotkeySettingsConfig Hotkeys { get; set; } = AppConfigDefaults.CreateDefaultHotkeys();

    // --- 通知 ---
    // 何を通知・読み上げるかは各ウィジェットの設定で決める。ここは出し方と音量と声だけ。

    /// <summary>通知方式。0=通知しない / 1=Windows の通知 / 2=読み上げ(<see cref="AppConfigDefaults"/> の定数)。</summary>
    public int NotificationMethodIndex { get; set; } = AppConfigDefaults.NotificationMethodNoneIndex;

    /// <summary>
    /// 通知音量 0〜100(%)。Windows の通知の音と読み上げの両方に掛ける(掛け方は <c>SpeechQueue</c>)。
    /// 100 は Windows の通知の音と Windows の音声では元の大きさ、VOICEVOX では元の大きさを上げた大きさ(50 が元の大きさ)。
    /// </summary>
    public int NotificationVolume { get; set; } = AppConfigDefaults.NotificationVolumeDefault;

    /// <summary>読み上げ方式(通知方式が読み上げのときの声)。0=Windows の音声 / 1=VOICEVOX(<see cref="AppConfigDefaults"/> の定数)。</summary>
    public int SpeechVoiceIndex { get; set; } = AppConfigDefaults.SpeechVoiceWindowsIndex;

    // VOICEVOX の話者。名前も持つのは、エンジンが起動していないときにも選んだ話者と利用規約のリンクを出すため。
    public string VoicevoxSpeakerUuid { get; set; } = string.Empty;
    public string VoicevoxSpeakerName { get; set; } = string.Empty;

    /// <summary>VOICEVOX のスタイルの番号。0 も使われる番号なので、未選択は null。</summary>
    public int? VoicevoxStyleId { get; set; }

    public string VoicevoxStyleName { get; set; } = string.Empty;

    public SettingsConfig Clone()
    {
        return new SettingsConfig
        {
            LanguageIndex = LanguageIndex,
            NetCaptureDeviceName = NetCaptureDeviceName,
            GameCapturePreference = GameCapturePreference,
            GameCaptureCustomExeName = GameCaptureCustomExeName,
            StopAggregationInAsterleeds = StopAggregationInAsterleeds,
            SplitEncountersOnNewPhases = SplitEncountersOnNewPhases,
            KeepPastEncounterInMeterUntilNextDamage = KeepPastEncounterInMeterUntilNextDamage,
            ClearHistorySelectionOnNextEvent = ClearHistorySelectionOnNextEvent,
            DatabaseMaxEncounterCount = DatabaseMaxEncounterCount,
            BenchmarkDurationSeconds = BenchmarkDurationSeconds,
            BenchmarkFirstTargetOnly = BenchmarkFirstTargetOnly,
            CombatExitSeconds = CombatExitSeconds,
            NumberDisplayFormatIndex = NumberDisplayFormatIndex,
            PlayerNameDisplayModeIndex = PlayerNameDisplayModeIndex,
            InternalIdDisplayModeIndex = InternalIdDisplayModeIndex,
            WidgetWindowTopmostModeIndex = WidgetWindowTopmostModeIndex,
            WindowColorIndex = WindowColorIndex,
            WindowColors = WindowColors is null ? AppConfigDefaults.CreateDefaultWindowColors() : [.. WindowColors],
            Hotkeys = Hotkeys?.Clone() ?? AppConfigDefaults.CreateDefaultHotkeys(),
            NotificationMethodIndex = NotificationMethodIndex,
            NotificationVolume = NotificationVolume,
            SpeechVoiceIndex = SpeechVoiceIndex,
            VoicevoxSpeakerUuid = VoicevoxSpeakerUuid,
            VoicevoxSpeakerName = VoicevoxSpeakerName,
            VoicevoxStyleId = VoicevoxStyleId,
            VoicevoxStyleName = VoicevoxStyleName
        };
    }
}
