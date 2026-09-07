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
    /// <summary>発生源ID → ゲーム内メーターの行名。<c>Data/Localization/recount.*.json</c>。</summary>
    private static FrozenDictionary<string, FrozenDictionary<int, string>> _recountNames =
        new Dictionary<string, FrozenDictionary<int, string>>(StringComparer.OrdinalIgnoreCase)
            .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private static FrozenDictionary<int, int> _recountSourceMap = FrozenDictionary<int, int>.Empty;

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
            // 畳みマッピングを先に読む。上書きが「畳まれて消えるID」を指していたら
            // 効かないので、読み込み時に警告を出すために要る。
            _recountSourceMap = LoadSourceMap("RecountSourceMap");
            _recountNames = LoadLocalizedText("recount", "RecountOverrides");
            _buffNameAliases = LoadNameAliases("BuffNameAlias");
            _nameSuffixes = LoadNameSuffixes();
        }
    }

    /// <summary>
    /// 行名に接尾辞を足すID。<c>Data/Mappings/BuffNameAlias.json</c>。イマジンのパッシブ用。
    ///
    /// <para>
    /// パッシブは<b>ゲーム内メーターでは本体イマジンと同じ行</b>に入る。こちらは分離して出す仕様なので、
    /// <c>RecountSourceMap</c> から除外したうえで、行名(＝本体の名前)に接尾辞を足して読み分ける。
    /// <b>IDは変えない</b>ので集計も分かれたままになる。
    /// </para>
    ///
    /// <para>
    /// 行名が既に本体の名前なので、借りる先のIDは持たない。<b>接尾辞だけ。</b>
    /// </para>
    ///
    /// <para>
    /// <b>表示時にだけ効く。</b> 記録側(<c>CombatStats.Name</c>)は触らないので、
    /// 後から表や接尾辞を変えれば過去のエンカウンターにも反映される。
    /// </para>
    /// </summary>
    private static FrozenDictionary<int, NameAlias> _buffNameAliases =
        FrozenDictionary<int, NameAlias>.Empty;

    /// <summary>接尾辞キー → 言語 → 文字列。<c>Data/Mappings/NameSuffixes.json</c>。</summary>
    private static FrozenDictionary<string, FrozenDictionary<string, string>> _nameSuffixes =
        FrozenDictionary<string, FrozenDictionary<string, string>>.Empty;

    /// <param name="Suffix">接尾辞のキー。<c>NameSuffixes.json</c> を引く。</param>
    private readonly record struct NameAlias(string Suffix);

    private sealed class NameAliasEntry
    {
        public string? Suffix { get; set; }
    }

    private static FrozenDictionary<int, NameAlias> LoadNameAliases(string dataName)
    {
        var path = Path.Combine(Utils.DATA_DIR_NAME, "Mappings", $"{dataName}.json");
        if (!File.Exists(path))
        {
            return FrozenDictionary<int, NameAlias>.Empty;
        }

        var raw = JsonConvert.DeserializeObject<Dictionary<string, NameAliasEntry>>(
            File.ReadAllText(path));
        var map = new Dictionary<int, NameAlias>();
        if (raw is not null)
        {
            foreach (var pair in raw)
            {
                if (int.TryParse(pair.Key, out var id)
                    && pair.Value is not null
                    && !string.IsNullOrWhiteSpace(pair.Value.Suffix))
                {
                    map[id] = new NameAlias(pair.Value.Suffix);
                }
            }
        }

        Log.Information("Loaded {Count} {DataName} entries", map.Count, dataName);
        return map.ToFrozenDictionary();
    }

    private static FrozenDictionary<string, FrozenDictionary<string, string>> LoadNameSuffixes()
    {
        var path = Path.Combine(Utils.DATA_DIR_NAME, "Mappings", "NameSuffixes.json");
        if (!File.Exists(path))
        {
            return FrozenDictionary<string, FrozenDictionary<string, string>>.Empty;
        }

        var raw = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(
            File.ReadAllText(path));
        var map = new Dictionary<string, FrozenDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        if (raw is not null)
        {
            foreach (var pair in raw)
            {
                map[pair.Key] = pair.Value.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
            }
        }

        return map.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// メーターの行に出す名前。<b>ゲーム内メーターの見出し表だけが決める。</b>
    ///
    /// <para>
    /// <c>skills</c> / <c>buffs</c> の4言語テーブルは引かない。あちらは装備中スキル枠や
    /// バフ/デバフウィジェットのためのもので、メーターの行とは対象も粒度も違う。
    /// </para>
    ///
    /// <para>
    /// 別名表にあるIDは接尾辞を足す(イマジンのパッシブ)。
    /// <b>名前が空なら接尾辞も付けない。</b>
    /// 接尾辞だけの行が出ると、名前が取れていないことが見えなくなる。
    /// </para>
    /// </summary>
    public static string GetSourceDisplayName(int id, bool isBuffSource)
    {
        // 名前・接尾辞・内部ID注記の3つを、この順で組み立てる。
        //
        // <b>注記は最後に1回だけ、行自身のIDで付ける。</b> GetSkillName / GetBuffName は
        // 注記込みで返すので、それを土台にすると「借り先のID + 接尾辞」という順序になり、
        // 注記が行のIDを指さなくなる(実際 3210050 の行が "…(3903)（パッシブ）" と出ていた)。
        var body = ResolveText(_recountNames, Volatile.Read(ref _cultureName), id);

        // イマジンのパッシブ。行名は既に本体イマジンの名前なので、借りる先は要らない。
        // 接尾辞だけを足して本体と読み分ける(ゲームは統合するが、こちらは分離して出す)。
        //
        // <b>名前が空なら接尾辞も付けない。</b>
        // 接尾辞だけの行が出ると、名前が取れていないことが見えなくなる。
        if (!string.IsNullOrEmpty(body) && _buffNameAliases.TryGetValue(id, out var alias))
        {
            body += ResolveSuffix(alias.Suffix);
        }

        return AppendInternalId(
            body,
            isBuffSource ? InternalIdDisplayMode.BuffOnly : InternalIdDisplayMode.SkillOnly,
            id);
    }

    private static string ResolveSuffix(string suffixKey)
    {
        if (string.IsNullOrEmpty(suffixKey)
            || !_nameSuffixes.TryGetValue(suffixKey, out var byCulture))
        {
            return string.Empty;
        }

        var culture = NormalizeCultureName(Volatile.Read(ref _cultureName));
        if (byCulture.TryGetValue(culture, out var text) && !string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        // 受け皿は zh-CN ただ1つ。名前の解決と同じ規則に揃える。
        return byCulture.TryGetValue("zh-CN", out var chinese) ? chinese : string.Empty;
    }

    /// <summary>
    /// 発生源ID → 同じメーター行の最若ID。<c>Data/Mappings/RecountSourceMap.json</c>。
    ///
    /// <para>
    /// ゲーム内メーターは <c>RecountTable</c> の1行に複数の構成IDをまとめる。この表は
    /// その行の所属から生成したもので、<b>手書きの判断は入っていない</b>。
    /// 畳み先は行の最若ID。
    /// </para>
    ///
    /// <para>
    /// <b>イマジンのパッシブは入れていない。</b> 行の上では本体と同じ行に居るが、
    /// こちらは分離して出す仕様なので、畳むと本体に統合されてしまう。
    /// 代わりに <c>BuffNameAlias</c> が接尾辞を付ける。
    /// </para>
    /// </summary>
    public static bool TryResolveRecountSource(int id, out int rowLeadId)
        => _recountSourceMap.TryGetValue(id, out rowLeadId);


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

    /// <summary>
    /// メーターの行のアイコン。
    ///
    /// <para>
    /// <b>畳み先のIDは、届いたときの種別と一致するとは限らない。</b>
    /// 行の最若IDがバフで、そこへスキルとして届いたIDが畳まれることがある(実測で112行中5行)。
    /// そこで<b>IDがどちらの生テーブルに載っているか</b>で決め、
    /// 両方に載っている(90件)か、どちらにも無いときだけ届いた種別に従う。
    /// </para>
    /// </summary>
    public static string GetSourceIconName(int id, bool arrivedAsBuff)
    {
        var inSkillTable = _skills.ContainsKey(id);
        var inBuffTable = _buffs.ContainsKey(id);

        if (inSkillTable && !inBuffTable)
        {
            return GetSkillIconName(id);
        }

        if (inBuffTable && !inSkillTable)
        {
            return GetBuffOwnIconName(id);
        }

        return arrivedAsBuff ? GetBuffOwnIconName(id) : GetSkillIconName(id);
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

    /// <summary>
    /// 生成物の名前テーブルに手修正を重ねる表。<c>Data/Overrides/{name}.json</c>。
    ///
    /// <para>
    /// 形は <c>{ "発生源ID": { "言語": "名前" } }</c>。
    /// 書いた言語だけ差し替え、書かない言語は生成値のまま。
    /// <b>空文字は「生成値を消す」</b>で、以後は通常どおり zh-CN へ落ちる。
    /// </para>
    ///
    /// <para>
    /// <b>言語名の打ち間違いを黙って無視しない。</b> 未知のキーはログにエラーを出して飛ばす。
    /// 静かに効かないのが一番困る失敗なので、必ずログに出す。
    /// </para>
    /// </summary>
    private static Dictionary<string, Dictionary<int, string>> LoadLocalizedOverrides(string dataName)
    {
        var result = SupportedCultures.ToDictionary(
            culture => culture,
            _ => new Dictionary<int, string>(),
            StringComparer.OrdinalIgnoreCase);

        var path = Path.Combine(Utils.DATA_DIR_NAME, "Overrides", $"{dataName}.json");
        if (!File.Exists(path))
        {
            return result;
        }

        var raw = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(
            File.ReadAllText(path));
        if (raw is null)
        {
            return result;
        }

        var applied = 0;
        foreach (var entry in raw)
        {
            if (!int.TryParse(entry.Key, out var id))
            {
                Log.Error("{DataName}: キーがIDではないので飛ばす \"{Key}\"", dataName, entry.Key);
                continue;
            }

            foreach (var pair in entry.Value ?? [])
            {
                if (!result.TryGetValue(pair.Key, out var byId))
                {
                    Log.Error(
                        "{DataName}: {Id} に未知のキー \"{Key}\"。言語は {Cultures}",
                        dataName, entry.Key, pair.Key, string.Join(" / ", SupportedCultures));
                    continue;
                }

                byId[id] = (pair.Value ?? string.Empty).Trim();
                applied++;
            }

            // 畳まれて消えるIDへ書いても表示されない。静かな空振りになるので警告する。
            if (_recountSourceMap.TryGetValue(id, out var rowLeadId))
            {
                Log.Warning(
                    "{DataName}: {Id} は {RowLeadId} へ畳まれるので効かない。{RowLeadId} に書くこと",
                    dataName, id, rowLeadId, rowLeadId);
            }
        }

        Log.Information("Loaded {Count} {DataName} overrides for {Ids} ids", applied, dataName, raw.Count);
        return result;
    }

    private static FrozenDictionary<string, FrozenDictionary<int, string>> LoadLocalizedText(
        string dataName,
        string? overridesName = null)
    {
        var result = new Dictionary<string, FrozenDictionary<int, string>>(StringComparer.OrdinalIgnoreCase);
        var overrides = overridesName is null ? null : LoadLocalizedOverrides(overridesName);

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

            // 手修正を重ねる。空文字は生成値を消す指示。
            if (overrides is not null && overrides.TryGetValue(cultureName, out var overridesById))
            {
                foreach (var pair in overridesById)
                {
                    if (string.IsNullOrEmpty(pair.Value))
                    {
                        names.Remove(pair.Key);
                    }
                    else
                    {
                        names[pair.Key] = pair.Value;
                    }
                }
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
