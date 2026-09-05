using System.Collections.Frozen;
using Newtonsoft.Json;
using Serilog;
using StarResonanceDps.Core.CombatRuntime.DataTypes;
using StarResonanceDps.Core.Models;

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
    private static FrozenDictionary<string, FrozenDictionary<int, string>> _monsterNames =
        new Dictionary<string, FrozenDictionary<int, string>>(StringComparer.OrdinalIgnoreCase)
            .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    private static FrozenDictionary<int, int> _skillSourceMap = FrozenDictionary<int, int>.Empty;

    private static FrozenDictionary<int, int> _buffSourceMap = FrozenDictionary<int, int>.Empty;

    private static FrozenSet<int> _skillKeys = FrozenSet<int>.Empty;

    private static FrozenSet<int> _buffKeys = FrozenSet<int>.Empty;

    private static FrozenDictionary<int, int> _attrSourceMap = FrozenDictionary<int, int>.Empty;

    private static FrozenDictionary<string, FrozenDictionary<int, string>> _sceneNames =
        new Dictionary<string, FrozenDictionary<int, string>>(StringComparer.OrdinalIgnoreCase)
            .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    private static string _cultureName = "en-US";

    /// <summary>
    /// 参照した内部IDを名前に添えるか。<c>int</c> で持つのは <see cref="Volatile"/> で読み書きするため。
    /// </summary>
    private static int _internalIdDisplayMode = (int)InternalIdDisplayMode.Hidden;

    public static void SetCulture(string? cultureName)
    {
        Volatile.Write(ref _cultureName, NormalizeCultureName(cultureName));
    }

    public static void SetInternalIdDisplay(InternalIdDisplayMode mode)
    {
        Volatile.Write(ref _internalIdDisplayMode, (int)mode);
    }

    /// <summary>
    /// 参照した内部IDを名前に添える。<b>書式は全言語で半角括弧。</b>
    ///
    /// <para>
    /// テーブルに無くて生テーブル名へ落ちた場合も付ける。「どのIDを引いたか」を見るためのもので、
    /// 引けなかったときこそIDが要る(欠けているエントリを特定できる)。
    /// </para>
    ///
    /// <para>
    /// <b>名前が空のときは付けない。</b>「名前が無い」ことを見て表示を落としている箇所があるので、
    /// そこを <c>(123)</c> で埋めると挙動が変わる。
    /// </para>
    /// </summary>
    private static string AppendInternalId(string name, InternalIdDisplayMode kind, long id)
    {
        if (id <= 0 || string.IsNullOrEmpty(name))
        {
            return name;
        }

        var mode = (InternalIdDisplayMode)Volatile.Read(ref _internalIdDisplayMode);
        return mode == InternalIdDisplayMode.All || mode == kind
            ? $"{name}({id})"
            : name;
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
            _monsterNames = LoadLocalizedText("monsters");
            _sceneNames = LoadLocalizedText("scenes");
            _skillSourceMap = LoadSourceMap("SkillSourceMap");
            _buffSourceMap = LoadSourceMap("BuffSourceMap");
            _skillKeys = LoadLocalizedKeys("skills");
            _buffKeys = LoadLocalizedKeys("buffs");
            _attrSourceMap = LoadAttrSourceMap();
        }
    }

    /// <summary>
    /// ダメージ属性ID → 親スキルID。<c>SkillEffectTable.SkillAttrDes</c> の式に出る
    /// 合成キーを <c>DamageAttrTable.TypeEnum</c> で解いて、その式を持つスキルを親とする。
    ///
    /// <para>
    /// 弾ID規則(<c>SkillId × 100 + Level</c>)と違い、<b>どのスキルの効果として書かれているか</b>を
    /// そのまま読む。両者は同じ整数を別の意味で使うことがあり、実測19件で行き先が食い違った
    /// (<c>230101</c> は弾規則だと <c>2301 琴弦撩拨</c>、式の上では <c>2308 聚合乐章</c>)。
    /// </para>
    ///
    /// <para>
    /// <b>親が複数に割れる子は入れない。</b> プレイヤーの技とNPC版が同じダメージ属性を
    /// 共有していることがあり、術者を見ないと決まらない。決め切れないものは畳まない。
    /// </para>
    /// </summary>
    private static FrozenDictionary<int, int> LoadAttrSourceMap()
    {
        var typeEnumByKey = new Dictionary<string, int>();
        foreach (var pair in HelperMethods.DataTables.DamageAttrs.Data)
        {
            if (pair.Value.TypeEnum != 0)
            {
                typeEnumByKey[pair.Key] = pair.Value.TypeEnum;
            }
        }

        var parents = new Dictionary<int, int>();
        var split = new HashSet<int>();

        foreach (var pair in HelperMethods.DataTables.SkillEffects.Data)
        {
            var parent = pair.Value.SkillId;
            if (parent == 0 || pair.Value.SkillAttrDes is null)
            {
                continue;
            }

            foreach (var row in pair.Value.SkillAttrDes)
            {
                if (row is null)
                {
                    continue;
                }

                foreach (var cell in row)
                {
                    foreach (var key in ExtractBracedKeys(cell))
                    {
                        if (!typeEnumByKey.TryGetValue(key, out var child)
                            || child == 0
                            || child == parent)
                        {
                            continue;
                        }

                        if (parents.TryGetValue(child, out var known))
                        {
                            if (known != parent)
                            {
                                split.Add(child);
                            }
                        }
                        else
                        {
                            parents[child] = parent;
                        }
                    }
                }
            }
        }

        foreach (var child in split)
        {
            parents.Remove(child);
        }

        Log.Information(
            "Loaded {Count} attr sources ({Split} split, dropped)",
            parents.Count,
            split.Count);
        return parents.ToFrozenDictionary();
    }

    /// <summary>
    /// <c>{123456}</c> の形で式に埋め込まれた合成キーを拾う。
    ///
    /// <para>
    /// <b>式は入れ子になっている。</b> 実データは
    /// <c>{*skillpara.damageMerge({122950102},{1},"PVEDamageRadio","up")*}</c> の形で、
    /// 外側の <c>{*</c> と内側の <c>{数字}</c> が混ざる。最初の <c>{</c> と最初の <c>}</c> を
    /// 対にすると内側を取り逃すので、<b>「<c>{</c> の直後が数字で、数字の直後が <c>}</c>」</b>
    /// という形だけを拾う。
    /// </para>
    /// </summary>
    private static IEnumerable<string> ExtractBracedKeys(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            yield break;
        }

        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] != '{')
            {
                continue;
            }

            var start = index + 1;
            var end = start;
            while (end < text.Length && char.IsAsciiDigit(text[end]))
            {
                end++;
            }

            if (end > start && end < text.Length && text[end] == '}')
            {
                yield return text[start..end];
                index = end;
            }
        }
    }

    /// <summary>
    /// ダメージ属性IDから親スキルへ。畳み込みの<b>最後の手段</b>で、
    /// 対応表・4言語テーブル・種別ごとの解決・召喚体のどれでも表に届かなかったときだけ引く。
    /// </summary>
    public static bool TryResolveAttrSource(int id, out int parentSkillId)
        => _attrSourceMap.TryGetValue(id, out parentSkillId);

    /// <summary>
    /// 4言語テーブルに<b>キーとして載っているID</b>。値が空でも数える。
    ///
    /// <para>
    /// 畳み込みの停止条件はこちらで、名前解決とは別。
    /// 「IDはあるが訳が無い」は正当な状態で、そのIDは素のまま表示する意思表示として扱う。
    ///
    /// <para>
    /// <see cref="LoadLocalizedText"/> と別に読むのは、あちらが空値を落とすため
    /// 「そのIDが登録されているか」を答えられないから。空値を落とすのは
    /// <see cref="ResolveText"/> が空欄で zh-CN へ落ちる仕様に必要で、そちらは正しい。
    /// </para>
    /// </para>
    /// </summary>
    private static FrozenSet<int> LoadLocalizedKeys(string dataName)
    {
        var keys = new HashSet<int>();

        foreach (var cultureName in SupportedCultures)
        {
            var path = Path.Combine(
                Utils.DATA_DIR_NAME,
                "Localization",
                $"{dataName}.{cultureName}.json");
            if (!File.Exists(path))
            {
                continue;
            }

            var raw = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(path));
            if (raw is null)
            {
                continue;
            }

            foreach (var pair in raw)
            {
                if (int.TryParse(pair.Key, out var id))
                {
                    keys.Add(id);
                }
            }
        }

        Log.Information("Loaded {Count} {DataName} keys", keys.Count, dataName);
        return keys.ToFrozenSet();
    }

    /// <summary>
    /// 観測ID → 親スキルID。<c>Data/Mappings/</c> の手書きの対応表。
    ///
    /// <para>
    /// 畳み込みの<b>1段目</b>。実行時の鎖より先に引く。鎖はバフ実体が届いているかに左右されるが、
    /// この表は届いていなくても答えを持っているので、同じスキルが行として割れなくなる。
    /// </para>
    /// </summary>
    public static bool TryResolveSkillSource(int id, out int parentSkillId)
        => _skillSourceMap.TryGetValue(id, out parentSkillId);

    /// <summary>
    /// バフID → 親スキルID。<c>SkillSourceMap</c> と<b>種別で分けてある</b>ので、
    /// バフとして届いたIDはこちらだけを引く。
    ///
    /// <para>
    /// <c>SkillTable</c> と <c>BuffTable</c> は90個のIDが重複するため、両方を引くと
    /// 同じIDがどちらの意味で解決されるか呼び出し元次第になる。分けておけば
    /// <c>DamageSource</c> で一意に決まる。
    /// </para>
    /// </summary>
    public static bool TryResolveBuffSource(int id, out int parentSkillId)
        => _buffSourceMap.TryGetValue(id, out parentSkillId);

    /// <summary>
    /// そのIDが<b>4言語テーブルにキーとして載っている</b>か。畳み込みの停止条件。
    ///
    /// <para>
    /// 載っている＝「素のまま表示すると決めたID」で、そこから先へは登らない。
    /// <b>値が空でも止める。</b>「IDはあるが訳が無い」は正当な状態で、
    /// 訳の有無で畳み方が変わってはいけない。
    /// </para>
    ///
    /// <para>
    /// どのIDを載せるかはテーブルを作るときの判断で、実行時には理由を問わない。
    /// 枠に置けるかどうかもその判断材料の1つでしかないので、ここでは見ない。
    /// </para>
    /// </summary>
    public static bool HasSkillKey(int skillId) => _skillKeys.Contains(skillId);

    /// <inheritdoc cref="HasSkillKey"/>
    public static bool HasBuffKey(int buffId) => _buffKeys.Contains(buffId);

    private static FrozenDictionary<int, int> LoadSourceMap(string dataName)
    {
        var path = Path.Combine(Utils.DATA_DIR_NAME, "Mappings", $"{dataName}.json");
        if (!File.Exists(path))
        {
            Log.Warning("Missing source mapping {DataName}", dataName);
            return FrozenDictionary<int, int>.Empty;
        }

        var raw = JsonConvert.DeserializeObject<Dictionary<string, int>>(File.ReadAllText(path));
        var map = new Dictionary<int, int>();
        if (raw is not null)
        {
            foreach (var pair in raw)
            {
                if (int.TryParse(pair.Key, out var id) && pair.Value != 0 && pair.Value != id)
                {
                    map[id] = pair.Value;
                }
            }
        }

        Log.Information("Loaded {Count} source mappings from {DataName}", map.Count, dataName);
        return map.ToFrozenDictionary();
    }

    public static string GetSkillName(int skillId)
    {
        return AppendInternalId(
            ResolveText(_skillNames, Volatile.Read(ref _cultureName), skillId),
            InternalIdDisplayMode.SkillOnly,
            skillId);
    }

    public static string GetBuffName(int buffId)
    {
        return AppendInternalId(
            ResolveText(_buffNames, Volatile.Read(ref _cultureName), buffId),
            InternalIdDisplayMode.BuffOnly,
            buffId);
    }

    /// <summary>
    /// モンスター名。<b>表示中の言語で引く。</b>
    ///
    /// <para>
    /// 引数は <c>AttrId</c>(<c>MonsterTable</c> のキー＝種別ID)。エンティティ側が持っている
    /// 名前は起動時に英語で焼き付くので、言語切替に追従させるにはここを通す。
    /// </para>
    /// </summary>
    public static string GetMonsterName(long monsterId)
    {
        return monsterId is > 0 and <= int.MaxValue
            ? AppendInternalId(
                ResolveText(_monsterNames, Volatile.Read(ref _cultureName), (int)monsterId),
                InternalIdDisplayMode.EntityOnly,
                monsterId)
            : string.Empty;
    }

    /// <summary>
    /// シーン/ダンジョン名。<b>表示中の言語で引く。</b>
    ///
    /// <para>
    /// 引数は <c>LevelMapId</c>。履歴は名前ではなくIDを保持し、表示時にここで引き直す。
    /// </para>
    /// </summary>
    public static string GetSceneName(long levelMapId)
    {
        return levelMapId is > 0 and <= int.MaxValue
            ? AppendInternalId(
                ResolveText(_sceneNames, Volatile.Read(ref _cultureName), (int)levelMapId),
                InternalIdDisplayMode.MapOnly,
                levelMapId)
            : string.Empty;
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
        int id)
    {
        // 「キーが無い」ではなく「空欄」で落とす。テーブルはIDを全言語ぶん持ち、
        // 訳が用意できていない言語だけ空文字にしてある。
        var normalizedCulture = NormalizeCultureName(cultureName);
        if (localizedNames.TryGetValue(normalizedCulture, out var currentNames)
            && currentNames.TryGetValue(id, out var currentName)
            && !string.IsNullOrWhiteSpace(currentName))
        {
            return currentName;
        }

        // 受け皿は zh-CN ただ1つ。en-US を挟まないのは、言語ごとに違う受け皿へ落ちると
        // 「どの言語のテーブルが欠けているのか」が分からなくなるため。
        //
        // 記録時の名前(CombatStats.Name)も使わない。あれは同梱 SkillTable/BuffTable 由来で、
        // 英語と中国語が混ざっており表示言語に追従しない。名前は翻訳テーブルだけが決める。
        if (!string.Equals(normalizedCulture, "zh-CN", StringComparison.OrdinalIgnoreCase)
            && localizedNames.TryGetValue("zh-CN", out var chineseNames)
            && chineseNames.TryGetValue(id, out var chineseName)
            && !string.IsNullOrWhiteSpace(chineseName))
        {
            return chineseName;
        }

        return string.Empty;
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
