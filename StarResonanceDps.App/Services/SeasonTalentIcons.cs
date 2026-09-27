using System.Windows;
using System.Windows.Media;
using StarResonanceDps.App.Localization;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.Services;

/// <summary>
/// シーズン心相晶の型の絵と型の対応。絵ごとに、その絵を使う型の根ノードのバフの番号を古いシーズンから並べる。
/// 絵は Icons.xaml の <c>Icon.SeasonTalent.&lt;絵の名前&gt;</c>、アイコンカラーの設定の鍵は <c>SeasonTalent.&lt;絵の名前&gt;</c>。
/// 型が分からない人(不明)と無効の人は、鍵 <see cref="UnknownColorKey"/> の色を使う。
/// </summary>
public static class SeasonTalentIcons
{
    public const string ColorKeyPrefix = "SeasonTalent.";

    /// <summary>不明と無効の色の鍵。</summary>
    public const string UnknownColorKey = ColorKeyPrefix + "Unknown";

    /// <summary>絵の名前の数値の昇順。この並びがアイコンカラーの設定の行の並びになる。</summary>
    public static IReadOnlyList<(string IconName, int[] RootBuffIds)> Icons { get; } =
    [
        ("s2talent01_01", [3002010]),
        ("s2talent02_01", [3002210, 3004900]),
        ("s2talent03_01", [3002410]),
        ("s2talent04_01", [3002610]),
        ("s2talent05_01", [3002810, 3005100]),
        ("s2talent06_01", [3003010, 3005700]),
        ("s2talent07_01", [3003210, 3005500]),
        ("s2talent08_01", [3003410, 3005300]),
        ("s2talent051_01", [3005900]),
        ("s2talent054_01", [3006100])
    ];

    /// <summary>アイコンカラーの設定の鍵。絵の10件、最後に不明。</summary>
    public static IReadOnlyList<string> ColorKeys { get; } =
        [.. Icons.Select(icon => ColorKeyPrefix + icon.IconName), UnknownColorKey];

    private static readonly Dictionary<int, string> IconNameByRootBuffId = Icons
        .SelectMany(icon => icon.RootBuffIds.Select(buffId => (buffId, icon.IconName)))
        .ToDictionary(pair => pair.buffId, pair => pair.IconName);

    private static readonly Dictionary<string, Brush> IconMasks = new(StringComparer.Ordinal);

    public static bool IsColorKey(string key)
    {
        return key.StartsWith(ColorKeyPrefix, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>その型の色の鍵。表に無い型と、型が無い(0)ときは不明の鍵。</summary>
    public static string GetColorKey(int rootBuffId)
    {
        return IconNameByRootBuffId.TryGetValue(rootBuffId, out var iconName)
            ? ColorKeyPrefix + iconName
            : UnknownColorKey;
    }

    /// <summary>
    /// プレイヤーリストの枠の中の形。型が分かれば型の絵、無効なら無し(枠だけ)、不明ならクラスの不明と同じ絵。
    /// 表に無い型は無し。
    /// </summary>
    public static Brush? GetIconMask(int rootBuffId, bool isInactive)
    {
        if (rootBuffId > 0)
        {
            return IconNameByRootBuffId.TryGetValue(rootBuffId, out var iconName)
                ? ResolveMask($"Icon.SeasonTalent.{iconName}")
                : null;
        }

        return isInactive ? null : ResolveMask("Icon.Profession.Unknown");
    }

    /// <summary>アイコンカラーの設定の行の形。不明の鍵ならクラスの不明と同じ絵。</summary>
    public static Brush? GetIconMask(string colorKey)
    {
        if (string.Equals(colorKey, UnknownColorKey, StringComparison.OrdinalIgnoreCase))
        {
            return ResolveMask("Icon.Profession.Unknown");
        }

        return IsColorKey(colorKey)
            ? ResolveMask($"Icon.SeasonTalent.{colorKey[ColorKeyPrefix.Length..]}")
            : null;
    }

    /// <summary>アイコンカラーの設定の行の名前。絵を使う型の名前を古い順に「 / 」でつなぐ。不明の鍵なら「不明」。</summary>
    public static string GetDisplayName(string colorKey)
    {
        if (string.Equals(colorKey, UnknownColorKey, StringComparison.OrdinalIgnoreCase))
        {
            return LocalizationManager.Instance.GetString("SeasonTalent_Unknown");
        }

        foreach (var (iconName, rootBuffIds) in Icons)
        {
            if (string.Equals(colorKey, ColorKeyPrefix + iconName, StringComparison.OrdinalIgnoreCase))
            {
                return string.Join(" / ", rootBuffIds
                    .Select(CombatDataCatalog.GetSeasonTalentName)
                    .Where(name => !string.IsNullOrEmpty(name))
                    .Distinct(StringComparer.Ordinal));
            }
        }

        return colorKey;
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
