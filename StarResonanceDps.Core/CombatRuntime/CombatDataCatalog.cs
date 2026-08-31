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
    private static FrozenDictionary<int, FrozenDictionary<int, float>> _skillCooldownsByLevel =
        new Dictionary<int, FrozenDictionary<int, float>>().ToFrozenDictionary();
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
            _skillCooldownsByLevel = LoadSkillCooldowns();
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

    public static string GetBuffOwnIconName(int buffId)
    {
        if (_buffs.TryGetValue(buffId, out var buff))
        {
            return FirstNonEmpty(buff.ShowHUDIcon, buff.Icon);
        }

        return string.Empty;
    }

    /// <summary>料理バフのアイコン。</summary>
    private const string CuisineBuffIconName = "buff_food_up";

    /// <summary>薬剤バフのアイコン。</summary>
    private const string PotionBuffIconName = "buff_agentia_up";

    /// <summary>
    /// 消費アイテム系バフのタグ。
    /// <c>美食的加护</c>・虚蚀战利品・丰收宴・禁药・沉梦抗性 などはこれを持たない。
    /// </summary>
    private const int ConsumableBuffTag = 100;

    /// <summary>
    /// 個別に扱わず1つのまとまりとして見るバフか。<b>アイコンとタグの両方</b>で判定する。
    ///
    /// <para>
    /// アイコンだけだと別系統が混ざる(<c>buff_food_up</c> には 美食的加护 と 丰收宴 が、
    /// <c>buff_agentia_up</c> には 禁药 と 沉梦抗性 が入る)。タグ100 を併せると
    /// 料理は <c>2032011</c>〜<c>2032284</c> の136件、薬剤は <c>2033011</c>〜<c>2033189</c> の
    /// 162件ちょうどになる。ゲーム側の表示名もそれぞれ1語に丸められている。
    /// </para>
    /// </summary>
    public static BuffGroup GetBuffGroup(int buffId)
    {
        if (!_buffs.TryGetValue(buffId, out var buff)
            || buff.Tags is null
            || !buff.Tags.Contains(ConsumableBuffTag))
        {
            return BuffGroup.None;
        }

        var icon = FirstNonEmpty(buff.ShowHUDIcon, buff.Icon);

        if (icon.Contains(CuisineBuffIconName, StringComparison.OrdinalIgnoreCase))
        {
            return BuffGroup.Cuisine;
        }

        return icon.Contains(PotionBuffIconName, StringComparison.OrdinalIgnoreCase)
            ? BuffGroup.Potion
            : BuffGroup.None;
    }

    public static string GetBuffIconName(int buffId, int sourceSkillId, string? fallbackIcon = null)
    {
        var buffIcon = GetBuffOwnIconName(buffId);
        if (!string.IsNullOrEmpty(buffIcon))
        {
            return buffIcon;
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
            return skill.IsImagineSlot();
        }

        return fallbackIcon?.Contains("skill_aoyi", StringComparison.OrdinalIgnoreCase) == true;
    }

    public static bool IsSkillRole(int skillId)
    {
        return _skills.TryGetValue(skillId, out var skill)
            && skill.IsRoleSlot();
    }

    public static bool HasLevelDependentCooldown(int skillId)
    {
        return _skillCooldownsByLevel.TryGetValue(skillId, out var cooldownsByLevel)
            && cooldownsByLevel.Count > 1
            && cooldownsByLevel.Values.Distinct().Skip(1).Any();
    }

    public static int GetSkillMaxCharges(int skillId)
    {
        return _skills.TryGetValue(skillId, out var skill)
            && skill.MaxEnergyChargeNum > 1
                ? skill.MaxEnergyChargeNum
                : 0;
    }

    public static double GetSkillChargeCooldownSeconds(int skillId, int tier)
    {
        if (!_skills.TryGetValue(skillId, out var skill)
            || skill.MaxEnergyChargeNum <= 1
            || skill.EnergyChargeTime <= 0)
        {
            return 0;
        }

        return ApplyImagineTierCooldownReduction(
            skill,
            skill.EnergyChargeTime / 1000d,
            tier);
    }

    public static double GetSkillPveCooldownSeconds(int skillId, int currentLevel, int tier)
    {
        if (!_skills.TryGetValue(skillId, out var skill))
        {
            return 0;
        }

        return ApplyImagineTierCooldownReduction(
            skill,
            ResolveSkillPveCooldownSeconds(skill, currentLevel),
            tier);
    }

    private static double ApplyImagineTierCooldownReduction(
        Skill skill,
        double cooldownSeconds,
        int tier)
    {
        if (cooldownSeconds <= 60 || !skill.IsImagineSlot())
        {
            return cooldownSeconds;
        }

        return tier switch
        {
            >= 3 and <= 4 => Math.Ceiling(cooldownSeconds * 0.8333d),
            >= 5 and <= 6 => Math.Ceiling(cooldownSeconds * 0.6666d),
            _ => cooldownSeconds
        };
    }

    private static FrozenDictionary<int, FrozenDictionary<int, float>> LoadSkillCooldowns()
    {
        var cooldownsBySkill = new Dictionary<int, Dictionary<int, float>>();

        foreach (var skillFightLevel in HelperMethods.DataTables.SkillFightLevels.Data.Values)
        {
            if (skillFightLevel.SkillId <= 0 || skillFightLevel.Level <= 0)
            {
                continue;
            }

            if (!cooldownsBySkill.TryGetValue(skillFightLevel.SkillId, out var cooldownsByLevel))
            {
                cooldownsByLevel = new Dictionary<int, float>();
                cooldownsBySkill.Add(skillFightLevel.SkillId, cooldownsByLevel);
            }

            cooldownsByLevel.TryAdd(skillFightLevel.Level, skillFightLevel.PVECoolTime);
        }

        return cooldownsBySkill
            .ToDictionary(
                pair => pair.Key,
                pair => pair.Value.ToFrozenDictionary())
            .ToFrozenDictionary();
    }

    private static float ResolveSkillPveCooldownSeconds(Skill skill, int currentLevel)
    {
        if (_skillCooldownsByLevel.TryGetValue(skill.Id, out var cooldownsByLevel)
            && cooldownsByLevel.Count > 0)
        {
            var normalizedLevel = Math.Max(currentLevel, 1);
            if (cooldownsByLevel.TryGetValue(normalizedLevel, out var exactCooldown))
            {
                return exactCooldown;
            }

            var lowerLevel = cooldownsByLevel.Keys
                .Where(level => level <= normalizedLevel)
                .DefaultIfEmpty(0)
                .Max();
            if (lowerLevel > 0)
            {
                return cooldownsByLevel[lowerLevel];
            }

            return cooldownsByLevel.OrderBy(pair => pair.Key).First().Value;
        }

        if (skill.EffectIDs is { Count: > 0 }
            && HelperMethods.DataTables.SkillFightLevels.Data.TryGetValue(
                skill.EffectIDs[0].ToString(),
                out var firstSkillFightLevel))
        {
            return firstSkillFightLevel.PVECoolTime;
        }

        return 0;
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
