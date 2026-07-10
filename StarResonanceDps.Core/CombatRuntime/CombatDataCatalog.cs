using System.Collections.Frozen;
using Newtonsoft.Json;
using Serilog;
using StarResonanceDps.Core.CombatRuntime.DataTypes;

namespace StarResonanceDps.Core.CombatRuntime;

public static class CombatDataCatalog
{
    private static readonly string[] SupportedCultures = ["en-US", "ja-JP", "ko-KR", "zh-CN"];
    private static readonly object Sync = new();

    private static FrozenDictionary<int, Skill> _skills =
        new Dictionary<int, Skill>().ToFrozenDictionary();
    private static FrozenDictionary<int, Buff> _buffs =
        new Dictionary<int, Buff>().ToFrozenDictionary();
    private static FrozenDictionary<string, FrozenDictionary<int, string>> _skillNames =
        new Dictionary<string, FrozenDictionary<int, string>>(StringComparer.OrdinalIgnoreCase)
            .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    private static FrozenDictionary<string, FrozenDictionary<int, string>> _buffNames =
        new Dictionary<string, FrozenDictionary<int, string>>(StringComparer.OrdinalIgnoreCase)
            .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    private static FrozenDictionary<string, FrozenDictionary<int, string>> _buffDescriptions =
        new Dictionary<string, FrozenDictionary<int, string>>(StringComparer.OrdinalIgnoreCase)
            .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    private static string _cultureName = "en-US";

    public static void SetCulture(string? cultureName)
    {
        Volatile.Write(ref _cultureName, NormalizeCultureName(cultureName));
    }

    public static void Load()
    {
        lock (Sync)
        {
            _skills = LoadNumericCatalog(HelperMethods.DataTables.Skills.Data);
            _buffs = LoadNumericCatalog(HelperMethods.DataTables.Buffs.Data);
            _skillNames = LoadLocalizedText("skills");
            _buffNames = LoadLocalizedText("buffs");
            _buffDescriptions = LoadLocalizedText("buff-descriptions");
        }
    }

    public static void ApplyEnglishNamesToLegacyTables()
    {
        foreach (var pair in _skills)
        {
            var fallback = FirstNonEmpty(pair.Value.Name, pair.Value.NameDesign);
            pair.Value.Name = ResolveText(_skillNames, "en-US", pair.Key, fallback);
        }

        foreach (var pair in _buffs)
        {
            var fallback = FirstNonEmpty(pair.Value.Name, pair.Value.NameDesign);
            pair.Value.Name = ResolveText(_buffNames, "en-US", pair.Key, fallback);
            pair.Value.Desc = ResolveText(_buffDescriptions, "en-US", pair.Key, pair.Value.Desc);
        }
    }

    public static string GetSkillName(int skillId, string? fallbackName = null)
    {
        var fallback = fallbackName;
        if (string.IsNullOrWhiteSpace(fallback)
            && _skills.TryGetValue(skillId, out var skill))
        {
            fallback = FirstNonEmpty(skill.Name, skill.NameDesign);
        }

        return ResolveText(_skillNames, Volatile.Read(ref _cultureName), skillId, fallback);
    }

    public static string GetBuffName(int buffId, string? fallbackName = null)
    {
        var fallback = fallbackName;
        if (string.IsNullOrWhiteSpace(fallback)
            && _buffs.TryGetValue(buffId, out var buff))
        {
            fallback = FirstNonEmpty(buff.Name, buff.NameDesign);
        }

        return ResolveText(_buffNames, Volatile.Read(ref _cultureName), buffId, fallback);
    }

    public static string GetBuffDescription(int buffId, string? fallbackDescription = null)
    {
        var fallback = fallbackDescription;
        if (string.IsNullOrWhiteSpace(fallback)
            && _buffs.TryGetValue(buffId, out var buff))
        {
            fallback = buff.Desc;
        }

        return ResolveText(_buffDescriptions, Volatile.Read(ref _cultureName), buffId, fallback);
    }

    public static string GetSkillIconName(int skillId, string? fallbackIcon = null)
    {
        if (_skills.TryGetValue(skillId, out var skill))
        {
            return FirstNonEmpty(skill.Icon, fallbackIcon);
        }

        return fallbackIcon?.Trim() ?? string.Empty;
    }

    public static string GetBuffIconName(int buffId, int sourceSkillId, string? fallbackIcon = null)
    {
        if (_buffs.TryGetValue(buffId, out var buff))
        {
            var buffIcon = FirstNonEmpty(buff.ShowHUDIcon, buff.Icon);
            if (!string.IsNullOrEmpty(buffIcon))
            {
                return buffIcon;
            }
        }

        if (sourceSkillId > 0
            && _skills.TryGetValue(sourceSkillId, out var skill)
            && !string.IsNullOrWhiteSpace(skill.Icon))
        {
            return skill.Icon;
        }

        return fallbackIcon?.Trim() ?? string.Empty;
    }

    public static bool IsSkillImagine(int skillId, string? fallbackIcon = null)
    {
        if (_skills.TryGetValue(skillId, out var skill))
        {
            if (skill.IsImagineSlot())
            {
                return true;
            }

            fallbackIcon = FirstNonEmpty(skill.Icon, fallbackIcon);
        }

        return fallbackIcon?.Contains("skill_aoyi", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static FrozenDictionary<int, T> LoadNumericCatalog<T>(IReadOnlyDictionary<string, T> source)
    {
        var result = new Dictionary<int, T>();
        foreach (var pair in source)
        {
            if (int.TryParse(pair.Key, out var id))
            {
                result[id] = pair.Value;
            }
        }

        return result.ToFrozenDictionary();
    }

    private static FrozenDictionary<string, FrozenDictionary<int, string>> LoadLocalizedText(string dataName)
    {
        var result = new Dictionary<string, FrozenDictionary<int, string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var cultureName in SupportedCultures)
        {
            var path = Path.Combine(
                Utils.DATA_DIR_NAME,
                "Localization",
                $"{dataName}.{cultureName}.json");
            var names = new Dictionary<int, string>();

            if (File.Exists(path))
            {
                var rawNames = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(path));
                if (rawNames is not null)
                {
                    foreach (var pair in rawNames)
                    {
                        if (int.TryParse(pair.Key, out var id) && !string.IsNullOrWhiteSpace(pair.Value))
                        {
                            names[id] = pair.Value.Trim();
                        }
                    }
                }

                Log.Information("Loaded {DataName} localization for {CultureName}", dataName, cultureName);
            }
            else
            {
                Log.Warning("Missing {DataName} localization for {CultureName}", dataName, cultureName);
            }

            result[cultureName] = names.ToFrozenDictionary();
        }

        return result.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    private static string ResolveText(
        IReadOnlyDictionary<string, FrozenDictionary<int, string>> localizedNames,
        string cultureName,
        int id,
        string? fallbackName)
    {
        var normalizedCulture = NormalizeCultureName(cultureName);
        if (localizedNames.TryGetValue(normalizedCulture, out var currentNames)
            && currentNames.TryGetValue(id, out var currentName))
        {
            return currentName;
        }

        if (!string.Equals(normalizedCulture, "en-US", StringComparison.OrdinalIgnoreCase)
            && localizedNames.TryGetValue("en-US", out var englishNames)
            && englishNames.TryGetValue(id, out var englishName))
        {
            return englishName;
        }

        return fallbackName?.Trim() ?? string.Empty;
    }

    private static string NormalizeCultureName(string? cultureName)
    {
        if (cultureName?.StartsWith("ja", StringComparison.OrdinalIgnoreCase) == true)
        {
            return "ja-JP";
        }

        if (cultureName?.StartsWith("ko", StringComparison.OrdinalIgnoreCase) == true)
        {
            return "ko-KR";
        }

        if (cultureName?.StartsWith("zh", StringComparison.OrdinalIgnoreCase) == true)
        {
            return "zh-CN";
        }

        return "en-US";
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }
}
