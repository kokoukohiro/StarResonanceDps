// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Globalization;
using System.Windows;
using System.Windows.Media;
using StarResonanceDps.App.Localization;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.Services;

/// <summary>
/// 潜在心相晶の型と絵の対応。型ごとに、型ID・シーズン・根ノードのバフID・絵の名前を持つ
/// (型の表 <c>SeasonTalentTemplateTable</c> の <c>Id</c>・<c>BelongSeasonId</c>・<c>NoteRootId</c> の効果のバフ・<c>BigIconNormal</c>)。
/// 絵は Icons.xaml の <c>Icon.SeasonTalent.&lt;絵の名前&gt;</c>、アイコンカラーの設定の鍵は <c>SeasonTalent.&lt;型ID&gt;</c>。同じ絵の型も別の行。
/// シーズンをまたいで同じ根のバフを使う型(3002410)があるので、型は根のバフと回のシーズンで決める(<see cref="TryResolveType"/>)。
/// プレイヤーリストは、型が分からない人(不明)と無効の人を鍵 <see cref="UnknownColorKey"/> の色で塗る。
/// プレイヤー情報は、無効の人を鍵 <see cref="InactiveColorKey"/> の色で塗り、不明はほかのバッジの不明と共通の1色で塗る。
/// </summary>
public static class SeasonTalentIcons
{
    public const string ColorKeyPrefix = "SeasonTalent.";

    /// <summary>プレイヤーリストの不明と無効の色の鍵。</summary>
    public const string UnknownColorKey = ColorKeyPrefix + "Unknown";

    /// <summary>プレイヤー情報の無効の色の鍵。</summary>
    public const string InactiveColorKey = ColorKeyPrefix + "Inactive";

    /// <summary>
    /// メインの型(プレイモードの中だけで足される型は入れない)。この並びがアイコンカラーの設定の行の並びになる。
    /// 新しいシーズンから並べ、シーズンの中はゲームの並び(<c>SortId</c>)。
    /// </summary>
    public static IReadOnlyList<SeasonTalentType> Types { get; } =
    [
        new(9, 4, 3004900, "s2talent02_01"),
        new(12, 4, 3005500, "s2talent07_01"),
        new(13, 4, 3005700, "s2talent06_01"),
        new(11, 4, 3005300, "s2talent08_01"),
        new(14, 4, 3005900, "s2talent051_01"),
        new(15, 4, 3006100, "s2talent054_01"),
        new(16, 4, 3002410, "s2talent03_01"),
        new(10, 4, 3005100, "s2talent05_01"),
        new(5, 3, 3002810, "s2talent05_01"),
        new(7, 3, 3003210, "s2talent07_01"),
        new(8, 3, 3003410, "s2talent08_01"),
        new(6, 3, 3003010, "s2talent06_01"),
        new(1, 3, 3002010, "s2talent01_01"),
        new(2, 3, 3002210, "s2talent02_01"),
        new(3, 3, 3002410, "s2talent03_01"),
        new(4, 3, 3002610, "s2talent04_01")
    ];

    /// <summary>型の色の鍵。型ごとに1件。</summary>
    public static IReadOnlyList<string> TypeColorKeys { get; } = [.. Types.Select(type => type.ColorKey)];

    /// <summary>プレイヤーリストのアイコンカラーの設定の鍵。型ごとに1件、最後に不明。</summary>
    public static IReadOnlyList<string> ColorKeys { get; } = [.. TypeColorKeys, UnknownColorKey];

    private static readonly Dictionary<int, SeasonTalentType[]> TypesByRootBuffId = Types
        .GroupBy(type => type.RootBuffId)
        .ToDictionary(group => group.Key, group => group.ToArray());

    private static readonly Dictionary<string, SeasonTalentType> TypesByColorKey = Types
        .ToDictionary(type => type.ColorKey, StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, Brush> IconMasks = new(StringComparer.Ordinal);

    public static bool IsColorKey(string key)
    {
        return key.StartsWith(ColorKeyPrefix, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>名簿の行の回のシーズン。履歴の行はその回のシーズン、ライブの行は今のシーズン。</summary>
    public static int GetSeasonId(PlayerRosterEntry player)
    {
        return player.SeasonId ?? SeasonStateStore.CurrentSeasonId;
    }

    /// <summary>
    /// 根のバフと回のシーズンから型を決める。当たる型が1つならその型、2つ以上なら回のシーズンの型、
    /// 回のシーズンの型が無ければ一番新しいシーズンの型。表に無い根のバフは false。
    /// </summary>
    public static bool TryResolveType(int rootBuffId, int seasonId, out SeasonTalentType type)
    {
        if (!TypesByRootBuffId.TryGetValue(rootBuffId, out var candidates))
        {
            type = default;
            return false;
        }

        foreach (var candidate in candidates)
        {
            if (candidate.SeasonId == seasonId)
            {
                type = candidate;
                return true;
            }
        }

        type = candidates.MaxBy(candidate => candidate.SeasonId);
        return true;
    }

    /// <summary>
    /// その型の名前。表に無い根のバフは、根のバフの名前(同じ根のバフの型が複数あれば一番新しいシーズンの型の名前)。
    /// </summary>
    public static string GetTypeName(int rootBuffId, int seasonId)
    {
        return TryResolveType(rootBuffId, seasonId, out var type)
            ? CombatDataCatalog.GetSeasonTalentTypeName(type.TypeId, type.RootBuffId)
            : CombatDataCatalog.GetSeasonTalentName(rootBuffId);
    }

    /// <summary>その型の色の鍵。表に無い型と、型が無い(0)ときは不明の鍵。</summary>
    public static string GetColorKey(int rootBuffId, int seasonId)
    {
        return TryResolveType(rootBuffId, seasonId, out var type)
            ? type.ColorKey
            : UnknownColorKey;
    }

    /// <summary>型の色の鍵の型の絵の名前。型の鍵でなければ null。既定の色の決め方と、古い設定の移し替えに使う。</summary>
    public static string? GetIconName(string colorKey)
    {
        return TypesByColorKey.TryGetValue(colorKey, out var type) ? type.IconName : null;
    }

    /// <summary>
    /// 型の色の鍵を、絵ごとに色を持っていた前の版の鍵(<c>SeasonTalent.&lt;絵の名前&gt;</c>)にする。型の鍵でなければ null。
    /// 前の版の設定ファイルの色を型の行へ移すときに使う。
    /// </summary>
    public static string? GetLegacyIconColorKey(string colorKey)
    {
        return GetIconName(colorKey) is { } iconName ? ColorKeyPrefix + iconName : null;
    }

    /// <summary>
    /// プレイヤーリストの枠の中の形。型が分かれば型の絵、無効なら無し(枠だけ)、不明ならクラスの不明と同じ絵。
    /// 表に無い型(絵が無い型)は、不明と同じクラスの不明の絵(色も不明の色、<see cref="GetColorKey"/>)。
    /// </summary>
    public static Brush? GetIconMask(int rootBuffId, int seasonId, bool isInactive)
    {
        if (rootBuffId > 0)
        {
            return TryResolveType(rootBuffId, seasonId, out var type)
                ? ResolveMask($"Icon.SeasonTalent.{type.IconName}")
                : ResolveMask("Icon.Profession.Unknown");
        }

        return isInactive ? null : ResolveMask("Icon.Profession.Unknown");
    }

    /// <summary>プレイヤー情報で無効の人に出す形。</summary>
    public static Brush? GetInactiveIconMask()
    {
        return ResolveMask("Icon.SeasonTalentInactive");
    }

    /// <summary>アイコンカラーの設定の行の形。不明の鍵ならクラスの不明と同じ絵、無効の鍵なら無効の絵。</summary>
    public static Brush? GetIconMask(string colorKey)
    {
        if (string.Equals(colorKey, UnknownColorKey, StringComparison.OrdinalIgnoreCase))
        {
            return ResolveMask("Icon.Profession.Unknown");
        }

        if (string.Equals(colorKey, InactiveColorKey, StringComparison.OrdinalIgnoreCase))
        {
            return GetInactiveIconMask();
        }

        return GetIconName(colorKey) is { } iconName
            ? ResolveMask($"Icon.SeasonTalent.{iconName}")
            : null;
    }

    /// <summary>
    /// アイコンカラーの設定の行の名前。型の鍵なら型の名前、不明の鍵なら「不明(潜在心相晶)」、無効の鍵なら「心相晶無効」。
    /// </summary>
    public static string GetDisplayName(string colorKey)
    {
        if (string.Equals(colorKey, UnknownColorKey, StringComparison.OrdinalIgnoreCase))
        {
            return LocalizationManager.Instance.GetString("Settings_IconColors_UnknownSeasonTalent");
        }

        if (string.Equals(colorKey, InactiveColorKey, StringComparison.OrdinalIgnoreCase))
        {
            return LocalizationManager.Instance.GetString("Settings_IconColors_SeasonTalentInactive");
        }

        return TypesByColorKey.TryGetValue(colorKey, out var type)
            ? CombatDataCatalog.GetSeasonTalentTypeName(type.TypeId, type.RootBuffId)
            : colorKey;
    }

    /// <summary>Icons.xaml の絵から、塗りを抜く形を作る。鍵ごとに使い回す。</summary>
    private static Brush? ResolveMask(string resourceKey)
    {
        if (IconMasks.TryGetValue(resourceKey, out var cached))
        {
            return cached;
        }

        if (Application.Current?.TryFindResource(resourceKey) is not ImageSource source)
        {
            return null;
        }

        var mask = new ImageBrush(source) { Stretch = Stretch.Uniform };
        mask.Freeze();
        IconMasks[resourceKey] = mask;
        return mask;
    }
}

/// <summary>潜在心相晶の型。型ID・シーズン・根ノードのバフID・絵の名前(<see cref="SeasonTalentIcons.Types"/>)。</summary>
public readonly record struct SeasonTalentType(int TypeId, int SeasonId, int RootBuffId, string IconName)
{
    /// <summary>アイコンカラーの設定の鍵(<c>SeasonTalent.&lt;型ID&gt;</c>)。</summary>
    public string ColorKey => SeasonTalentIcons.ColorKeyPrefix + TypeId.ToString(CultureInfo.InvariantCulture);
}
