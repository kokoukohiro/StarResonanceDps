using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace StarResonanceDps.Core.CombatRuntime.DataTypes;

public class Settings
{
    public const string AutomaticNetCaptureDeviceName = "Auto";

    public static Settings Instance = new();
    private static string SETTINGS_FILE_NAME = "Settings.json";

    public int Version { get; set; } = 0;
    public string Language { get; set; } = "en";
    public string NetCaptureDeviceName { get; set; } = AutomaticNetCaptureDeviceName;
    public bool NormalizeMeterContributions { get; set; } = true;
    public bool UseShortWidthNumberFormatting { get; set; } = true;
    public bool ShowClassIconsInMeters { get; set; } = true;
    public bool ColorClassIconsByRole { get; set; } = true;
    public bool ShowSkillIconsInDetails { get; set; } = true;
    public bool OnlyShowDamageContributorsInMeters { get; set; } = false;
    public bool OnlyShowPartyMembersInMeters { get; set; } = false;
    public bool ShowAbilityScoreInMeters { get; set; } = true;
    public bool ShowSeasonStrengthInMeters { get; set; } = false;
    public bool ShowSubProfessionNameInMeters { get; set; } = true;
    public bool ShowPlayerSummonsInMeters { get; set; } = false;
    public bool ShowPlayerImaginesInMeters { get; set; } = false;
    public bool UseAutomaticWipeDetection { get; set; } = true;
    public bool SkipTeleportStateCheckInAutomaticWipeDetection { get; set; } = false;
    public bool DisableWipeRecalculationOverwriting { get; set; } = false;
    public bool UseLegacyWipeDetection { get; set; } = false;
    public bool SplitEncountersOnNewPhases { get; set; } = true;
    public bool SkipSkillSnapshotSavingInOpenWorld { get; set; } = false;
    public bool DisplayTruePerSecondValuesInMeters { get; set; } = false;
    public bool AllowGamepadNavigationInput { get; set; } = false;
    public bool KeepPastEncounterInMeterUntilNextDamage { get; set; } = false;
    public bool ShowChannelLineNumberInStatus { get; set; } = false;
    public bool ShowCallWipeForEncounterOnMainWindow { get; set; } = false;
    public bool UseDatabaseForEncounterHistory { get; set; } = true;
    public int DatabaseRetentionPolicyDays { get; set; } = 0;
    public bool SkipSavingEncountersWithNoCombatData { get; set; } = false;
    public bool LimitEncounterBuffTrackingInOpenWorld { get; set; } = false;
    public bool AllowEncounterSavingPausingInOpenWorld { get; set; } = false;
    public bool PersistEncounterSavingPauseStateBetweenMaps { get; set; } = false;
    public bool MinimalProcessingWhileEncounterSavingPaused { get; set; } = false;
    public bool IncludeHealEventsOutsideOfCombat { get; set; } = false;

    public bool MeterSettingsTankingShowDeaths { get; set; } = false;
    public bool MeterSettingsNpcTakenShowHpData { get; set; } = false;
    public bool MeterSettingsNpcTakenHideMaxHp { get; set; } = false;
    public bool MeterSettingsNpcTakenUseHpMeter { get; set; } = false;

    public bool LogToFile { get; set; } = true;
    public EGameCapturePreference GameCapturePreference { get; set; } = EGameCapturePreference.Auto;
    public string GameCaptureCustomExeName { get; set; } = "";
    public bool PlayNotificationSoundOnMatchmake { get; set; } = false;
    public string MatchmakeNotificationSoundPath { get; set; } = "";
    public bool LoopNotificationSoundOnMatchmake { get; set; } = false;
    public float MatchmakeNotificationVolume { get; set;} = 1.0f;
    public bool PlayNotificationSoundOnReadyCheck { get; set; } = false;
    public string ReadyCheckNotificationSoundPath { get; set; } = "";
    public bool LoopNotificationSoundOnReadyCheck { get; set; } = false;
    public float ReadyCheckNotificationVolume { get; set; } = 1.0f;

    public bool SaveEncounterReportToFile { get; set; } = false;
    public int ReportFileRetentionPolicyDays { get; set; } = 0;
    public int MinimumPlayerCountToCreateReport { get; set; } = 0;
    public bool AlwaysCreateReportAtDungeonEnd { get; set; } = true;

    public bool WebhookReportsEnabled { get; set; } = false;
    public EWebhookReportsMode WebhookReportsMode { get; set; } = EWebhookReportsMode.Discord;
    public string WebhookReportsDeduplicationServerHost { get; set; } = "";
    public string WebhookReportsDiscordUrl { get; set; } = "";
    public string WebhookReportsCustomUrl { get; set; } = "";

    public bool CheckForApplicationUpdatesOnStartup { get; set; } = false;
    public string LatestApplicationVersionCheckUrl { get; set; } = "";
    public string ApplicationWebsiteUrl { get; set; } = "";
    public bool HasPromptedEnableUpdateChecks { get; set; } = false;

    public bool LowPerformanceMode { get; set; } = false;

    public uint HotkeysEncounterReset { get; set; }
    public uint HotkeysPinnedWindowClickthrough { get; set; }
    public uint HotkeysToggleWindowMinimize { get; set; }

    public uint FixedFramerateScale { get; set; } = 1;

    public bool EnableGDIBackBufferCopyCompatibility { get; set; } = false;

    public bool AggressiveExceptionDebugLogging = false;

    public void Apply()
    {
        MessageManager.NetCaptureDeviceName = IsAutomaticNetCaptureDeviceName(NetCaptureDeviceName)
            ? string.Empty
            : NetCaptureDeviceName;
        MessageManager.GameCapturePreference = GameCapturePreference;
        MessageManager.GameCaptureCustomExeName = Path.GetFileNameWithoutExtension(GameCaptureCustomExeName ?? string.Empty);
    }

    public static void Load()
    {
        if (File.Exists(Path.Combine(Utils.DATA_DIR_NAME, SETTINGS_FILE_NAME)))
        {
            var settingsTxt = File.ReadAllText(Path.Combine(Utils.DATA_DIR_NAME, SETTINGS_FILE_NAME));
            Instance = DeserializePersistedSettings(settingsTxt);
        }
        else
        {
            Save();
        }
    }

    private static Settings DeserializePersistedSettings(string settingsText)
    {
        var settingsObject = JObject.Parse(settingsText);
        RenamePersistedProperty(
            settingsObject,
            name => name.StartsWith(nameof(AllowGamepadNavigationInput), StringComparison.OrdinalIgnoreCase),
            nameof(AllowGamepadNavigationInput));
        RenamePersistedProperty(
            settingsObject,
            name => name.StartsWith("CheckFor", StringComparison.OrdinalIgnoreCase)
                && name.EndsWith("UpdatesOnStartup", StringComparison.OrdinalIgnoreCase),
            nameof(CheckForApplicationUpdatesOnStartup));
        RenamePersistedProperty(
            settingsObject,
            name => name.StartsWith("Latest", StringComparison.OrdinalIgnoreCase)
                && name.EndsWith("VersionCheckURL", StringComparison.OrdinalIgnoreCase),
            nameof(LatestApplicationVersionCheckUrl));
        RenamePersistedProperty(
            settingsObject,
            name => name.EndsWith("WebsiteURL", StringComparison.OrdinalIgnoreCase),
            nameof(ApplicationWebsiteUrl));

        var settings = settingsObject.ToObject<Settings>() ?? new Settings();
        settings.NormalizePersistedValues();
        return settings;
    }

    public static bool IsAutomaticNetCaptureDeviceName(string? deviceName)
    {
        return string.IsNullOrWhiteSpace(deviceName)
            || string.Equals(deviceName.Trim(), AutomaticNetCaptureDeviceName, StringComparison.OrdinalIgnoreCase);
    }

    private void NormalizePersistedValues()
    {
        if (IsAutomaticNetCaptureDeviceName(NetCaptureDeviceName))
        {
            NetCaptureDeviceName = AutomaticNetCaptureDeviceName;
        }
    }

    private static void RenamePersistedProperty(
        JObject settingsObject,
        Func<string, bool> sourceNamePredicate,
        string destinationName)
    {
        if (settingsObject.Properties().Any(property =>
            string.Equals(property.Name, destinationName, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var sourceProperty = settingsObject.Properties().FirstOrDefault(property =>
            sourceNamePredicate(property.Name));
        if (sourceProperty is null)
        {
            return;
        }

        settingsObject[destinationName] = sourceProperty.Value;
        sourceProperty.Remove();
    }

    public static void Save()
    {
        var settingsJson = JsonConvert.SerializeObject(Instance, Formatting.Indented);
        File.WriteAllText(Path.Combine(Utils.DATA_DIR_NAME, SETTINGS_FILE_NAME), settingsJson);
    }
}

public enum EGameCapturePreference
{
    Auto,
    Steam,
    Standalone,
    Epic,
    HaoPlaySea,
    XDG,
    HaoPlaySeaSteam,
    XDGSteam,
    WeGame,
    Custom = 200
}

public enum EWebhookReportsMode
{
    DiscordDeduplication,
    Discord,
    Custom,
    FallbackDiscordDeduplication,
}
