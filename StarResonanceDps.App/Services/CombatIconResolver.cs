using System.Collections.Concurrent;
using System.IO;

namespace StarResonanceDps.App.Services;

public static class CombatIconResolver
{
    private static readonly ConcurrentDictionary<string, string> ResolvedPaths =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>バフのアイコン。バフのフォルダだけを探す。</summary>
    public static string? ResolveBuffIcon(string? iconName)
    {
        return Resolve(iconName, "Buffs");
    }

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
        var baseDirectory = AppContext.BaseDirectory;
        var candidateNames = GetCandidateNames(iconName).ToArray();

        foreach (var category in categories)
        {
            var categoryDirectory = Path.Combine(baseDirectory, "Data", "Images", category);
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
