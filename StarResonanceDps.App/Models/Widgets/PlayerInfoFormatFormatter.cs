using System.Text.RegularExpressions;
using StarResonanceDps.App.Localization;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.Models.Widgets;

public static partial class PlayerInfoFormatFormatter
{
    private const string HiddenPlayerName = "*****";

    public static string Format(
        MeterPlayerSnapshot player,
        string? formatString,
        PlayerNameDisplayMode nameDisplayMode)
    {
        // 自分も他人と同じ経路。特化は特化マーカーバフだけで決まるので、
        // 自分だけタレントから引き直す必要はない。
        var classSpec = PlayerClassSpecResolver.FromSubProfessionId(player.SubProfessionId);
        return Format(
            new PlayerInfoFormatData(
                player.UserId,
                player.Name,
                player.ProfessionId,
                classSpec,
                player.AbilityScore,
                player.SeasonStrength,
                player.SeasonLevel,
                player.IsSelf,
                player.IsNpc),
            formatString,
            nameDisplayMode);
    }

    public static string Format(
        PlayerRosterEntry player,
        string? formatString,
        PlayerNameDisplayMode nameDisplayMode)
    {
        return Format(
            new PlayerInfoFormatData(
                player.CharacterId,
                player.Name,
                player.ProfessionId,
                player.ClassSpec,
                player.CombatPower,
                player.SeasonStrength,
                player.SeasonLevel,
                player.IsSelf,
                player.IsNpc),
            formatString,
            nameDisplayMode);
    }

    public static string FormatPreview(string? formatString)
    {
        return Format(
            new PlayerInfoFormatData(
                123456789,
                LocalizationManager.Instance.GetString("Settings_PlayerInfo_PreviewName"),
                2,
                PlayerClassSpec.FrostMageIcicle,
                25000,
                8,
                50,
                true,
                false),
            formatString,
            PlayerNameDisplayMode.Show);
    }

    private static string Format(
        PlayerInfoFormatData player,
        string? formatString,
        PlayerNameDisplayMode nameDisplayMode)
    {
        if (string.IsNullOrEmpty(formatString))
        {
            return string.Empty;
        }

        var result = formatString;

        result = GetNameRegex().Replace(result, GetName(player, nameDisplayMode));
        result = GetSpecRegex().Replace(result, LocalizationManager.Instance.GetString($"ClassSpec_{player.ClassSpec}"));
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

    private static string GetName(PlayerInfoFormatData player, PlayerNameDisplayMode nameDisplayMode)
    {
        if (ShouldHideName(player.IsSelf, nameDisplayMode))
        {
            return HiddenPlayerName;
        }

        if (player.IsNpc)
        {
            var professionKey = PlayerProfession.GetKey(player.ProfessionId);
            return LocalizationManager.Instance.GetString($"Classes_{professionKey}");
        }

        return string.IsNullOrWhiteSpace(player.Name)
            ? $"UID:{player.UserId}"
            : player.Name;
    }

    private static bool ShouldHideName(bool isSelf, PlayerNameDisplayMode nameDisplayMode)
    {
        return nameDisplayMode switch
        {
            PlayerNameDisplayMode.Hide => true,
            PlayerNameDisplayMode.HideOthers => !isSelf,
            _ => false
        };
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

    private readonly record struct PlayerInfoFormatData(
        long UserId,
        string Name,
        int ProfessionId,
        PlayerClassSpec ClassSpec,
        int AbilityScore,
        int SeasonStrength,
        int SeasonLevel,
        bool IsSelf,
        bool IsNpc);
}
