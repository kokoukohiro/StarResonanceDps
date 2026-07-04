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
        string professionSpecText)
    {
        NameText = nameText;
        LevelText = levelText;
        AbilityScoreText = abilityScoreText;
        ProfessionText = professionText;
        ProfessionSpecText = professionSpecText;
    }

    public string NameText { get; }

    public string LevelText { get; }

    public string AbilityScoreText { get; }

    public string ProfessionText { get; }

    public string ProfessionSpecText { get; }

    public static PlayerInfoEntry Create(PlayerRosterEntry player)
    {
        var localization = LocalizationManager.Instance;

        return new PlayerInfoEntry(
            $"{localization.GetString("PlayerInfo_Name")}: {player.Name}",
            $"{localization.GetString("PlayerInfo_Level")}: {FormatInteger(player.Level)} (+{FormatInteger(player.SeasonLevel)})",
            $"{localization.GetString("PlayerInfo_AbilityScore")}: {FormatInteger(player.CombatPower)} (+{FormatInteger(player.SeasonStrength)})",
            $"{localization.GetString("PlayerInfo_Profession")}: {GetProfessionDisplayName(player.ProfessionId, localization)}",
            $"{localization.GetString("PlayerInfo_ProfessionSpec")}: {GetProfessionSpecDisplayName(player.ClassSpec, localization)}");
    }

    private static string GetProfessionDisplayName(int professionId, LocalizationManager localization)
    {
        var professionKey = PlayerProfession.GetKey(professionId);
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
