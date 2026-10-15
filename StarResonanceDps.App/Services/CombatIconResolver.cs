using System.Collections.Concurrent;
using System.IO;
using System.Windows;
using System.Windows.Media;
using Serilog;
using StarResonanceDps.App.Config;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.Services;

public static class CombatIconResolver
{
    /// <summary>
    /// バフの絵の名前のうち、アプリの <c>Styles/Icons.xaml</c> の絵の鍵を指す値の頭(手修正 <c>BuffOverrides.json</c> の <c>Icon</c>)。
    /// 装備効果の絵(<c>Icon.EquipSlot.202</c> など)。
    /// </summary>
    private const string AppIconPrefix = "Icon.";

    private static readonly ConcurrentDictionary<string, string> ResolvedPaths =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Icons.xaml の鍵 → 型抜きのブラシ。鍵ごとに1つ作って使い回す。Icons.xaml に無い鍵は null。画面のスレッドだけで使う。</summary>
    private static readonly Dictionary<string, ImageBrush?> AppIconMasks = new(StringComparer.Ordinal);

    /// <summary>
    /// バフのアイコン(ファイル)。モジュール効果の絵(<see cref="CombatDataCatalog.IsModEffectIcon"/>)ならモジュールのフォルダだけ、
    /// それ以外はバフのフォルダだけを探す。Icons.xaml の絵の鍵(<see cref="ResolveBuffIconMask"/>)はファイルではないので null。
    /// </summary>
    public static string? ResolveBuffIcon(string? iconName)
    {
        if (IsAppIcon(iconName))
        {
            return null;
        }

        return Resolve(iconName, CombatDataCatalog.IsModEffectIcon(iconName) ? "Modules" : "Buffs");
    }

    /// <summary>
    /// バフの絵の名前が Icons.xaml の絵の鍵なら、その絵(白い形)を、白く塗る面の型抜きにするブラシ。
    /// 鍵でなければ null。鍵が Icons.xaml に無ければ null で、警告のログを鍵ごとに1回出す(呼ぶ側ははてなを出す)。
    /// <b>画面のスレッドで呼ぶ</b>(Icons.xaml を引く)。
    /// </summary>
    public static ImageBrush? ResolveBuffIconMask(string? iconName)
    {
        if (!IsAppIcon(iconName))
        {
            return null;
        }

        if (AppIconMasks.TryGetValue(iconName!, out var mask))
        {
            return mask;
        }

        if (Application.Current.TryFindResource(iconName) is ImageSource source)
        {
            mask = new ImageBrush(source) { Stretch = Stretch.Uniform };
            mask.Freeze();
        }
        else
        {
            Log.Warning("Buff icon {IconName} is not a resource in Icons.xaml; showing the unknown icon", iconName);
        }

        AppIconMasks[iconName!] = mask;
        return mask;
    }

    private static bool IsAppIcon(string? iconName)
        => iconName?.StartsWith(AppIconPrefix, StringComparison.Ordinal) == true;

    /// <summary>技のアイコン。スキルのフォルダだけを探す(イマジンの技もここ)。</summary>
    public static string? ResolveSkillIcon(string? iconName)
    {
        return Resolve(iconName, "Skills");
    }


    private static string? Resolve(string? iconName, params string[] categories)
    {
        if (string.IsNullOrWhiteSpace(iconName))
        {
            return null;
        }

        var cacheKey = string.Join('|', categories) + '|' + iconName;
        var resolvedPath = ResolvedPaths.GetOrAdd(
            cacheKey,
            _ => ResolveUncached(iconName, categories) ?? string.Empty);
        return string.IsNullOrEmpty(resolvedPath) ? null : resolvedPath;
    }

    private static string? ResolveUncached(string iconName, IReadOnlyList<string> categories)
    {
        var candidateNames = GetCandidateNames(iconName).ToArray();

        foreach (var category in categories)
        {
            var categoryDirectory = Path.Combine(AppDataPaths.ImagesDirectory, category);
            foreach (var candidateName in candidateNames)
            {
                var candidatePath = Path.Combine(categoryDirectory, candidateName);
                if (File.Exists(candidatePath))
                {
                    return candidatePath;
                }
            }
        }

        return null;
    }

    private static IEnumerable<string> GetCandidateNames(string iconName)
    {
        var normalized = iconName
            .Trim()
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
        var fileName = Path.GetFileName(normalized);

        yield return fileName;

        if (Path.HasExtension(fileName))
        {
            yield break;
        }

        yield return fileName + ".png";
        yield return fileName + ".jpg";
        yield return fileName + ".jpeg";
        yield return fileName + ".webp";
    }
}
