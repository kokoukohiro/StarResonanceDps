using System.Globalization;
using StarResonanceDps.App.Localization;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.Models.Widgets;

public sealed class PlayerInfoEntry
{
    private PlayerInfoEntry(
        string nameText,
        string levelText,
        string abilityScoreText,
        string professionText,
        string professionSpecText,
        int seasonTalentBuffId,
        bool isSeasonTalentInactive,
        string seasonTalentName)
    {
        NameText = nameText;
        LevelText = levelText;
        AbilityScoreText = abilityScoreText;
        ProfessionText = professionText;
        ProfessionSpecText = professionSpecText;
        SeasonTalentBuffId = seasonTalentBuffId;
        IsSeasonTalentInactive = isSeasonTalentInactive;
        SeasonTalentName = seasonTalentName;
    }

    public string NameText { get; }

    public string LevelText { get; }

    public string AbilityScoreText { get; }

    public string ProfessionText { get; }

    public string ProfessionSpecText { get; }

    /// <summary>有効化しているシーズンタレントの型の根ノードのバフID。0 は不明か無効(<see cref="IsSeasonTalentInactive"/> で分ける)。</summary>
    public int SeasonTalentBuffId { get; }

    /// <summary>シーズンタレントの型が無効(どの型も有効化していない)と確定しているか。</summary>
    public bool IsSeasonTalentInactive { get; }

    /// <summary>有効化しているシーズンタレントの型の名前(根ノードの名前)。不明か無効なら空。</summary>
    public string SeasonTalentName { get; }

    public static PlayerInfoEntry Create(PlayerRosterEntry player)
    {
        var localization = LocalizationManager.Instance;

        return new PlayerInfoEntry(
            $"{localization.GetString("PlayerInfo_Name")}: {player.Name}",
            $"{localization.GetString("PlayerInfo_Level")}: {FormatInteger(player.Level)} (+{FormatInteger(player.SeasonLevel)})",
            $"{localization.GetString("PlayerInfo_AbilityScore")}: {FormatInteger(player.CombatPower)} (+{FormatInteger(player.SeasonStrength)})",
            $"{localization.GetString("PlayerInfo_Profession")}: {GetProfessionDisplayName(player.ProfessionId, player.ClassSpec, localization)}",
            $"{localization.GetString("PlayerInfo_ProfessionSpec")}: {GetProfessionSpecDisplayName(player.ClassSpec, localization)}",
            player.SeasonTalentBuffId,
            player.IsSeasonTalentInactive,
            player.SeasonTalentBuffId > 0
                ? StarResonanceDps.Core.CombatRuntime.CombatDataCatalog.GetSeasonTalentName(player.SeasonTalentBuffId)
                : string.Empty);
    }

    private static string GetProfessionDisplayName(int professionId, PlayerClassSpec classSpec, LocalizationManager localization)
    {
        var professionKey = PlayerProfession.GetKey(professionId, classSpec);
        if (professionId != 0 && string.Equals(professionKey, "Unknown", StringComparison.Ordinal))
        {
            return FormatInteger(professionId);
        }

        var key = $"Classes_{professionKey}";
        var value = localization.GetString(key);
        return string.Equals(value, key, StringComparison.Ordinal)
            ? FormatInteger(professionId)
            : value;
    }

    private static string GetProfessionSpecDisplayName(PlayerClassSpec classSpec, LocalizationManager localization)
    {
        if (PlayerClassSpecResolver.ToSubProfessionId(classSpec) > 0)
        {
            return PlayerInfoFormatFormatter.GetClassSpecText(classSpec);
        }

        var key = $"ClassSpec_{classSpec}";
        var value = localization.GetString(key);
        return string.Equals(value, key, StringComparison.Ordinal)
            ? localization.GetString("ClassSpec_Unknown")
            : value;
    }

    private static string FormatInteger(int value)
    {
        return value.ToString(CultureInfo.CurrentCulture);
    }
}
