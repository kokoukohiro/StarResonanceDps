using System.Text.RegularExpressions;
using StarResonanceDps.App.Localization;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Models;
using StarResonanceDps.Core.Services;

namespace StarResonanceDps.App.Models.Widgets;

public static partial class PlayerInfoFormatFormatter
{
    private const string HiddenPlayerName = "*****";

    /// <summary>設定画面の見本に出すシーズンタレントの型(根ノードのバフ 3002810、4言語とも名前が短い型)。</summary>
    private const int PreviewSeasonTalentBuffId = 3002810;

    /// <summary>書式に差し込める項目(設定の「プレイヤー名」の項目の一覧)。プレイヤーリスト・メーター・蘇生リストで同じ。</summary>
    public static IReadOnlyList<(string Key, string LabelResourceKey, string Placeholder)> FieldDefinitions { get; } =
    [
        ("PlayerName", "Settings_PlayerInfo_Field_Name", "{PlayerName}"),
        ("Spec", "Settings_PlayerInfo_Field_Class", "{Spec}"),
        ("Psych", "Settings_PlayerInfo_Field_SeasonTalent", "{Psych}"),
        ("PowerLevel", "Settings_PlayerInfo_Field_AbilityScore", "{PowerLevel}"),
        ("SeasonStrength", "Settings_PlayerInfo_Field_SeasonStrength", "{SeasonStrength}"),
        ("SeasonLevel", "Settings_PlayerInfo_Field_SeasonLevel", "{SeasonLevel}"),
        ("Uid", "Settings_PlayerInfo_Field_PlayerUid", "{Uid}")
    ];

    public static string Format(
        MeterPlayerSnapshot player,
        string? formatString,
        PlayerNameDisplayMode nameDisplayMode)
    {
        // 特化は Core で解決済みのものをそのまま使う。ここで SubProfessionId から
        // 組み立て直すと Rank1(アビリティ未装着)を表現できず、メーターだけ「不明」になる。
        return Format(
            new PlayerInfoFormatData(
                player.UserId,
                player.Name,
                player.ProfessionId,
                player.ClassSpec,
                player.AbilityScore,
                player.SeasonStrength,
                player.SeasonLevel,
                player.IsSelf,
                player.IsNpc,
                player.SeasonTalentBuffId,
                player.IsSeasonTalentInactive),
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
                player.IsNpc,
                player.SeasonTalentBuffId,
                player.IsSeasonTalentInactive),
            formatString,
            nameDisplayMode);
    }

    public static string FormatPreview(string? formatString)
    {
        return Format(
            new PlayerInfoFormatData(
                39733357,
                LocalizationManager.Instance.GetString("Settings_PlayerInfo_PreviewName"),
                2,
                PlayerClassSpec.FrostMageIcicle,
                25000,
                1800,
                50,
                true,
                false,
                PreviewSeasonTalentBuffId,
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
        result = GetSpecRegex().Replace(result, GetClassSpecText(player.ClassSpec));
        result = GetPsychRegex().Replace(result, GetSeasonTalentText(player.SeasonTalentBuffId, player.IsSeasonTalentInactive));
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

    /// <summary>
    /// NPC の規則を当てる前の名前の表示値。伏せる設定なら伏せ字、
    /// <b>名前がまだ取れていなければUID</b>、それ以外は名前そのもの。
    ///
    /// <para>
    /// <b>外へ出さない。</b>表示はどこも NPC の印を渡す版を通す。
    /// NPC の印を渡さずに名前を出せる入口があると、そこだけ NPC の規則が抜ける。
    /// </para>
    /// </summary>
    private static string GetDisplayName(
        string? name,
        long userId,
        bool isSelf,
        PlayerNameDisplayMode nameDisplayMode)
    {
        if (ShouldHideName(isSelf, nameDisplayMode))
        {
            return HiddenPlayerName;
        }

        return string.IsNullOrWhiteSpace(name)
            ? $"UID:{userId}"
            : name;
    }

    /// <summary>
    /// 相手の名前の表示値。<b>NPC は名前ではなく職業名を出す</b>(プレイヤー一覧と同じ規則)。
    /// 書式を通さない表示(ウィンドウのタイトル、バフ・デバフカードの本文)もこれを通す。
    /// </summary>
    public static string GetDisplayName(
        string? name,
        long userId,
        bool isSelf,
        bool isNpc,
        int professionId,
        PlayerNameDisplayMode nameDisplayMode)
    {
        if (ShouldHideName(isSelf, nameDisplayMode))
        {
            return HiddenPlayerName;
        }

        return isNpc
            ? LocalizationManager.Instance.GetString($"Classes_{PlayerProfession.GetKey(professionId)}")
            : GetDisplayName(name, userId, isSelf, nameDisplayMode);
    }

    /// <summary>
    /// 被ダメログの登場人物(加害者・対象)と、バフ/デバフリストの付与者(エンティティのリストでプレイヤーが付けた行)の表示値。
    /// システム(召喚者へ寄せられなかった仮想体)は「システム」、名前の無い HP バーの見える敵は「敵」、何者か分からない敵は「未知の敵」、
    /// プレイヤーはプレイヤー名の規則(<see cref="GetDisplayName(string?, long, bool, bool, int, PlayerNameDisplayMode)"/>)、
    /// それ以外は名前のまま。
    /// </summary>
    public static string GetPartyDisplayName(TakenDamageLogParty person)
    {
        if (person.IsSystem)
        {
            return LocalizationManager.Instance["TakenDamageLog_SystemActor"];
        }

        // 名前の無い、HP バーの見える敵。文言はエンティティリストと同じ。名前には内部ID注記だけが入っていることがある。
        if (person.IsUnnamedEnemy)
        {
            return LocalizationManager.Instance["EntityList_UnnamedEnemy"] + person.Name;
        }

        // 何者か分からない敵。名前の無いモンスターなら、名前には内部ID注記だけが入っていることがある。
        if (person.IsUnknownEnemy)
        {
            return LocalizationManager.Instance["EntityList_UnknownEnemy"] + person.Name;
        }

        return person.IsPlayer
            ? GetDisplayName(
                person.Name,
                person.CharacterId,
                person.IsSelf,
                person.IsNpc,
                person.ProfessionId,
                PlayerRosterPresentationStore.Instance.NameDisplayMode)
            : person.Name;
    }

    private static string GetName(PlayerInfoFormatData player, PlayerNameDisplayMode nameDisplayMode)
    {
        if (ShouldHideName(player.IsSelf, nameDisplayMode))
        {
            return HiddenPlayerName;
        }

        return GetDisplayName(
            player.Name,
            player.UserId,
            player.IsSelf,
            player.IsNpc,
            player.ProfessionId,
            nameDisplayMode);
    }

    /// <summary>
    /// シーズンタレントの型の表示値。型が分かれば型の名前、無効なら「無効」、どちらでもなければ「不明」。
    /// 特化の <c>{Spec}</c> と同じく、不明も空にせず文字で出す。書式の <c>{Psych}</c> とプレイヤーリストのツールチップが使う。
    /// </summary>
    public static string GetSeasonTalentText(int seasonTalentBuffId, bool isSeasonTalentInactive)
    {
        if (seasonTalentBuffId > 0)
        {
            return CombatDataCatalog.GetSeasonTalentName(seasonTalentBuffId);
        }

        return LocalizationManager.Instance.GetString(
            IsSeasonTalentUnknown(seasonTalentBuffId, isSeasonTalentInactive) ? "SeasonTalent_Unknown" : "SeasonTalent_Inactive");
    }

    /// <summary>潜在心相晶の型がまだ分からない(型が無く、無効とも確定していない)。表示の「不明」とウィジェットの状態の印が使う。</summary>
    public static bool IsSeasonTalentUnknown(int seasonTalentBuffId, bool isSeasonTalentInactive)
        => seasonTalentBuffId <= 0 && !isSeasonTalentInactive;

    /// <summary>書式が潜在心相晶(<c>{Psych}</c>)を出すか。ウィジェットの状態の印が、型の「不明」を数えるかに使う。</summary>
    public static bool ShowsSeasonTalent(string? format)
        => !string.IsNullOrEmpty(format) && GetPsychRegex().IsMatch(format);

    /// <summary>
    /// 特化の表示値。特化(クラスR2)なら名前の表 <c>ClassSpecNames.json</c> の名前、
    /// それ以外(クラスR1・変身・不明)はリソース <c>ClassSpec_&lt;特化&gt;</c> の文字。
    /// 書式の <c>{Spec}</c>、プレイヤーリスト、被ダメログ、プレイヤー情報が使う。
    /// </summary>
    public static string GetClassSpecText(PlayerClassSpec classSpec)
    {
        var subProfessionId = PlayerClassSpecResolver.ToSubProfessionId(classSpec);
        return subProfessionId > 0
            ? CombatDataCatalog.GetClassSpecName(subProfessionId)
            : LocalizationManager.Instance.GetString($"ClassSpec_{classSpec}");
    }

    internal static bool ShouldHideName(bool isSelf, PlayerNameDisplayMode nameDisplayMode)
    {
        return nameDisplayMode switch
        {
            PlayerNameDisplayMode.Hide => true,
            PlayerNameDisplayMode.HideOthers => !isSelf,
            _ => false
        };
    }

    [GeneratedRegex(@"\{PlayerName\}", RegexOptions.IgnoreCase)]
    private static partial Regex GetNameRegex();

    [GeneratedRegex(@"\{Spec\}", RegexOptions.IgnoreCase)]
    private static partial Regex GetSpecRegex();

    [GeneratedRegex(@"\{Psych\}", RegexOptions.IgnoreCase)]
    private static partial Regex GetPsychRegex();

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
        bool IsNpc,
        int SeasonTalentBuffId,
        bool IsSeasonTalentInactive);
}
