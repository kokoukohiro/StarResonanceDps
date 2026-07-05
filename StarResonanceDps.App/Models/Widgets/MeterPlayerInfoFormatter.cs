using System.Text.RegularExpressions;
using StarResonanceDps.App.Localization;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.Models.Widgets;

public static partial class MeterPlayerInfoFormatter
{
    private const string HiddenPlayerName = "*****";

    public static string Format(
        MeterPlayerSnapshot player,
        string? formatString,
        PlayerNameDisplayMode nameDisplayMode)
    {
        var format = string.IsNullOrWhiteSpace(formatString)
            ? "{Name} - {Spec} ({PowerLevel}-S{SeasonStrength})"
            : formatString;
        var result = format;

        result = GetNameRegex().Replace(result, GetName(player, nameDisplayMode));
        result = GetSpecRegex().Replace(result, GetSpec(player));
        result = GetPowerLevelRegex().Replace(result, player.AbilityScore.ToString());
        result = GetSeasonStrengthRegex().Replace(result, player.SeasonStrength.ToString());
        result = GetSeasonLevelRegex().Replace(result, player.SeasonLevel.ToString());
        result = GetUidRegex().Replace(result, player.UserId.ToString());

        result = GetCollapseWhitespaceRegex().Replace(result, " ");
        result = GetEmptyParenthesisRegex().Replace(result, string.Empty);
        result = GetEmptyBracketRegex().Replace(result, string.Empty);
        result = GetRepeatedHyphensRegex().Replace(result, " - ");
        result = GetLeadingOrTrailingHyphenRegex().Replace(result, string.Empty);

        return result.Trim();
    }

    public static string FormatPreview(string? formatString)
    {
        var previewPlayer = new MeterPlayerSnapshot(
            CharacterId: 123456789,
            UserId: 123456789,
            Name: LocalizationManager.Instance.GetString("Settings_MeterPlayerInfo_PreviewName"),
            ProfessionId: 2,
            SubProfessionId: 02_00_01,
            AbilityScore: 25000,
            SeasonStrength: 8,
            SeasonLevel: 50,
            IsSelf: true,
            TotalValue: 0,
            ValuePerSecond: 0,
            Contribution: 0,
            BarRatio: 0);

        return Format(previewPlayer, formatString, PlayerNameDisplayMode.Show);
    }

    private static string GetName(MeterPlayerSnapshot player, PlayerNameDisplayMode nameDisplayMode)
    {
        if (ShouldHideName(player, nameDisplayMode))
        {
            return HiddenPlayerName;
        }

        return string.IsNullOrWhiteSpace(player.Name)
            ? $"UID:{player.UserId}"
            : player.Name;
    }

    private static bool ShouldHideName(MeterPlayerSnapshot player, PlayerNameDisplayMode nameDisplayMode)
    {
        return nameDisplayMode switch
        {
            PlayerNameDisplayMode.Hide => true,
            PlayerNameDisplayMode.HideOthers => !player.IsSelf,
            _ => false
        };
    }

    private static string GetSpec(MeterPlayerSnapshot player)
    {
        var classSpec = PlayerClassSpecResolver.FromSubProfessionId(player.SubProfessionId);
        return LocalizationManager.Instance.GetString($"ClassSpec_{classSpec}");
    }

    [GeneratedRegex(@"\{Name\}", RegexOptions.IgnoreCase)]
    private static partial Regex GetNameRegex();

    [GeneratedRegex(@"\{Spec\}", RegexOptions.IgnoreCase)]
    private static partial Regex GetSpecRegex();

    [GeneratedRegex(@"\{PowerLevel\}", RegexOptions.IgnoreCase)]
    private static partial Regex GetPowerLevelRegex();

    [GeneratedRegex(@"\{SeasonStrength\}", RegexOptions.IgnoreCase)]
    private static partial Regex GetSeasonStrengthRegex();

    [GeneratedRegex(@"\{SeasonLevel\}", RegexOptions.IgnoreCase)]
    private static partial Regex GetSeasonLevelRegex();

    [GeneratedRegex(@"\{Uid\}", RegexOptions.IgnoreCase)]
    private static partial Regex GetUidRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex GetCollapseWhitespaceRegex();

    [GeneratedRegex(@"\(\s*\)")]
    private static partial Regex GetEmptyParenthesisRegex();

    [GeneratedRegex(@"\[\s*\]")]
    private static partial Regex GetEmptyBracketRegex();

    [GeneratedRegex(@"\s*-\s*-\s*")]
    private static partial Regex GetRepeatedHyphensRegex();

    [GeneratedRegex(@"^\s*-\s*|\s*-\s*$")]
    private static partial Regex GetLeadingOrTrailingHyphenRegex();
}
