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
    /// <summary><see cref="_monsterNames"/> の鍵(<c>MonsterTable</c> の行)全部。名前が空の行も含む。</summary>
    private static FrozenSet<int> _monsterIds = FrozenSet<int>.Empty;
    /// <summary>技ID → ボス大技の予告(<c>DbmTable</c>)の名前。<c>Data/Localization/DbmNames.json</c>。</summary>
    private static FrozenDictionary<string, FrozenDictionary<int, string>> _dbmNames =
        new Dictionary<string, FrozenDictionary<int, string>>(StringComparer.OrdinalIgnoreCase)
            .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    /// <summary>
    /// <c>SkillTable.EffectIDs</c> の要素 → その要素を持つ技ID。予告の通知が技IDではなくエフェクトIDを運ぶときに引く。
    /// 複数の技が同じ要素を持つものは持ち主が決まらないので入れない。
    /// </summary>
    private static FrozenDictionary<int, int> _skillIdByEffectId = FrozenDictionary<int, int>.Empty;
    /// <summary>技ID → その技を <c>MonsterTable.SkillIds</c> に持つモンスターの種別ID。</summary>
    private static FrozenDictionary<int, FrozenSet<int>> _monsterIdsBySkillId = FrozenDictionary<int, FrozenSet<int>>.Empty;
    /// <summary>
    /// 戦闘画面の警告バーを出す技の技レベルID(技ID×100＋レベル)。<c>Data/Generated/SkillWarnings.json</c>。
    /// <c>DataTools/gen_skill_warnings.py</c> が生成する。
    /// </summary>
    private static FrozenSet<int> _warningSkillLevelIds = FrozenSet<int>.Empty;
    /// <summary><see cref="_warningSkillLevelIds"/> のどれかのレベルを持つ技ID。</summary>
    private static FrozenSet<int> _warningSkillIds = FrozenSet<int>.Empty;
    /// <summary>
    /// ボス大技の予告の番号(<c>DbmTable.Id</c>) → 予告のバーの既定の秒数(<c>CountCDTime</c>)。<c>Data/Raw/DbmTable.json</c>。
    /// 予告の通知の持続が 0 のとき、ゲームはこの秒数でバーを出す。
    /// </summary>
    private static FrozenDictionary<int, int> _dbmCountCdTimes = FrozenDictionary<int, int>.Empty;
    /// <summary>
    /// 料理のバフ → 名前(そのバフを付けるアイテムの名前)。<c>Data/Localization/CuisineBuffs.json</c>。
    /// <c>DataTools/gen_buff_groups.py</c> が生成する。バフ自身の名前は「料理」の1語に丸められているので、こちらを先に引く。
    /// </summary>
    private static FrozenDictionary<string, FrozenDictionary<int, string>> _cuisineBuffNames =
        new Dictionary<string, FrozenDictionary<int, string>>(StringComparer.OrdinalIgnoreCase)
            .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    /// <summary><see cref="_cuisineBuffNames"/> の鍵(料理のバフ)全部。</summary>
    private static FrozenSet<int> _cuisineBuffIds = FrozenSet<int>.Empty;
    /// <summary>薬剤のバフ → 名前。<c>Data/Localization/PotionBuffs.json</c>。形と引き方は料理と同じ。</summary>
    private static FrozenDictionary<string, FrozenDictionary<int, string>> _potionBuffNames =
        new Dictionary<string, FrozenDictionary<int, string>>(StringComparer.OrdinalIgnoreCase)
            .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    /// <summary><see cref="_potionBuffNames"/> の鍵(薬剤のバフ)全部。</summary>
    private static FrozenSet<int> _potionBuffIds = FrozenSet<int>.Empty;
    /// <summary>行代表キー → ゲーム内メーターの行名。<c>Data/Localization/RecountRows.json</c>。</summary>
    private static FrozenDictionary<string, FrozenDictionary<long, string>> _recountNames =
        new Dictionary<string, FrozenDictionary<long, string>>(StringComparer.OrdinalIgnoreCase)
            .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>発生源キー → 行代表キー。<c>RecountRows.json</c> の行構成に手修正を重ねたもの。</summary>
    private static FrozenDictionary<long, long> _recountRows = FrozenDictionary<long, long>.Empty;

    /// <summary>オプションのバフID → オプション名。<c>Data/Localization/RogueEntryNames.json</c>。</summary>
    private static FrozenDictionary<string, FrozenDictionary<int, string>> _rogueEntryNames =
        new Dictionary<string, FrozenDictionary<int, string>>(StringComparer.OrdinalIgnoreCase)
            .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary><see cref="_rogueEntryNames"/> の鍵(オプションのバフID)全部。</summary>
    private static FrozenSet<int> _rogueEntryBuffIds = FrozenSet<int>.Empty;

    /// <summary>シーズンタレントの型の根ノードのバフID → 根ノードの名前。<c>Data/Localization/SeasonTalentNames.json</c>。</summary>
    private static FrozenDictionary<string, FrozenDictionary<int, string>> _seasonTalentNames =
        new Dictionary<string, FrozenDictionary<int, string>>(StringComparer.OrdinalIgnoreCase)
            .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// <see cref="_seasonTalentNames"/> の鍵(根ノードのバフID)全部。型の判定に使う。
    /// 名前の辞書は空の値を落とすので、判定は名前ではなくこの鍵で行う。
    /// </summary>
    private static FrozenSet<int> _seasonTalentRootBuffIds = FrozenSet<int>.Empty;

    /// <summary>特化の番号(<c>SubProfessionId</c>)→ 特化の名前。<c>Data/Localization/ClassSpecNames.json</c>。</summary>
    private static FrozenDictionary<string, FrozenDictionary<int, string>> _classSpecNames =
        new Dictionary<string, FrozenDictionary<int, string>>(StringComparer.OrdinalIgnoreCase)
            .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>パーティの目的(<c>TeamTargetTable</c> の行)→ 名前。パーティのマッチングのマッチング先。<c>Data/Localization/TeamTargetNames.json</c>。</summary>
    private static FrozenDictionary<string, FrozenDictionary<int, string>> _teamTargetNames =
        new Dictionary<string, FrozenDictionary<int, string>>(StringComparer.OrdinalIgnoreCase)
            .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>活動(<c>SeasonActTable</c> の行)→ 名前。活動のマッチングのマッチング先。<c>Data/Localization/SeasonActNames.json</c>。</summary>
    private static FrozenDictionary<string, FrozenDictionary<int, string>> _seasonActNames =
        new Dictionary<string, FrozenDictionary<int, string>>(StringComparer.OrdinalIgnoreCase)
            .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>シーズン番号 → シーズンの名前(シーズン実績の1枠目の分類名)。<c>Data/Localization/SeasonNames.json</c>。</summary>
    private static FrozenDictionary<string, FrozenDictionary<int, string>> _seasonNames =
        new Dictionary<string, FrozenDictionary<int, string>>(StringComparer.OrdinalIgnoreCase)
            .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>シーズン番号 × 100 ＋ 段階 → シーズンランクの名前。<c>Data/Localization/SeasonRankNames.json</c>。</summary>
    private static FrozenDictionary<string, FrozenDictionary<int, string>> _seasonRankNames =
        new Dictionary<string, FrozenDictionary<int, string>>(StringComparer.OrdinalIgnoreCase)
            .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>装備ID → 装備名(<c>ItemTable.Name</c>)。<c>Data/Localization/EquipNames.json</c>。</summary>
    private static FrozenDictionary<string, FrozenDictionary<int, string>> _equipNames =
        new Dictionary<string, FrozenDictionary<int, string>>(StringComparer.OrdinalIgnoreCase)
            .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 進化・改鋳・レアの効果の番号(一時属性・バフ)→ 説明文。値の差し込みは <c>{0}</c>(そのまま)と <c>{1}</c>(%)。
    /// <c>Data/Localization/EquipEffectTexts.json</c>。
    /// </summary>
    private static FrozenDictionary<string, FrozenDictionary<int, string>> _equipEffectTexts =
        new Dictionary<string, FrozenDictionary<int, string>>(StringComparer.OrdinalIgnoreCase)
            .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>装備ID → 装備の定義。<c>Data/Generated/Equips.json</c> の <c>Equips</c>。</summary>
    private static FrozenDictionary<int, EquipmentDefinition> _equipments =
        FrozenDictionary<int, EquipmentDefinition>.Empty;

    /// <summary>
    /// <see cref="MakeEquipmentKey"/>(属性庫の型, 庫ID)→ 庫の行(表の並び順)。<c>Equips.json</c> の <c>AttrLibs</c>。
    /// 型は 1 = <c>EquipAttrLibTable</c>、2 = <c>EquipAttrSchoolLibTable</c>。
    /// </summary>
    private static FrozenDictionary<long, IReadOnlyList<EquipmentAttrLibRow>> _equipmentAttrLibs =
        FrozenDictionary<long, IReadOnlyList<EquipmentAttrLibRow>>.Empty;

    /// <summary><see cref="MakeEquipmentKey"/>(属性庫の型, 行ID)→ 行。<c>AttrLibs</c> の全行の索引。</summary>
    private static FrozenDictionary<long, EquipmentAttrLibRow> _equipmentAttrRows =
        FrozenDictionary<long, EquipmentAttrLibRow>.Empty;

    /// <summary>
    /// <see cref="MakeEquipmentKey"/>(効果の種類, 番号)→ シーズン強度の項目のシーズン。
    /// <c>Equips.json</c> の <c>StrengthSeasons</c>。
    /// </summary>
    private static FrozenDictionary<long, int> _equipmentStrengthSeasons = FrozenDictionary<long, int>.Empty;

    /// <summary>
    /// 特化の番号(<c>SubProfessionId</c>)→ 型2 の属性庫の特化(<c>TalentSchoolTable.Id</c>)。
    /// <c>Equips.json</c> の <c>SpecSchools</c>。
    /// </summary>
    private static FrozenDictionary<int, int> _equipmentSpecSchools = FrozenDictionary<int, int>.Empty;

    /// <summary>職業ID → クラスR1 の人の型2 の属性庫の特化。<c>Equips.json</c> の <c>Rank1Schools</c>。</summary>
    private static FrozenDictionary<int, int> _equipmentRank1Schools = FrozenDictionary<int, int>.Empty;

    private static FrozenDictionary<string, FrozenDictionary<int, string>> _sceneNames =
        new Dictionary<string, FrozenDictionary<int, string>>(StringComparer.OrdinalIgnoreCase)
            .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);


    /// <summary>
    /// ダンジョン名に付ける難易度名。<c>Data/Localization/DungeonTypeNames.json</c>。
    /// 鍵は <see cref="MakeDungeonTypeKey"/>(ファイルでは <c>番号</c> と <c>番号:段階</c>)。
    /// </summary>
    private static FrozenDictionary<string, FrozenDictionary<long, string>> _dungeonTypeNames =
        new Dictionary<string, FrozenDictionary<long, string>>(StringComparer.OrdinalIgnoreCase)
            .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>ダンジョン名と難易度名の区切り。ゲームの表示と同じで、全言語で同じ文字。</summary>
    private const string DungeonTypeNameSeparator = "-";
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
    /// <b>名前が空でもIDだけ出す。</b> 空のままだとどのIDの行か分からず、
    /// 「テーブルに無いID」と「テーブルにあるが名前が空のID」も見分けが付かない。
    /// </para>
    ///
    /// <para>
    /// <b>この注記が付いた文字列を記憶・保存しない。</b> 注記は表示設定なので、
    /// 保存すると設定を切ったあとも残る。控える値は注記を付けない getter から取る。
    /// </para>
    /// </summary>
    private static string AppendInternalId(string name, InternalIdDisplayMode kind, long id)
    {
        if (id <= 0)
        {
            return name;
        }

        var mode = (InternalIdDisplayMode)Volatile.Read(ref _internalIdDisplayMode);
        if (mode != InternalIdDisplayMode.All && mode != kind)
        {
            return name;
        }

        return string.IsNullOrEmpty(name) ? $"({id})" : $"{name}({id})";
    }

    /// <summary>
    /// メーターの行に内部IDを添える。書式は <c>ownerId:枝番</c> で、生成物・手修正の鍵と同じ。
    ///
    /// <para>
    /// <b>枝番まで出す。</b> 同じ <c>ownerId</c> が別の行に分かれることがあり、
    /// <c>ownerId</c> だけだと 臣鷹出撃 と 臣鷹の雷撃衝撃 がどちらも <c>(2203291)</c> になって
    /// 見分けが付かない。
    /// </para>
    /// </summary>
    private static string AppendSourceInternalId(string name, InternalIdDisplayMode kind, long rowKey)
    {
        if (rowKey == 0)
        {
            return name;
        }

        var mode = (InternalIdDisplayMode)Volatile.Read(ref _internalIdDisplayMode);
        if (mode != InternalIdDisplayMode.All && mode != kind)
        {
            return name;
        }

        var text = FormatSourceKey(rowKey);
        return string.IsNullOrEmpty(name) ? $"({text})" : $"{name}({text})";
    }

    public static void Load()
    {
        lock (Sync)
        {
            _skills = LoadNumericCatalog(HelperMethods.DataTables.Skills.Data);
            _buffs = LoadNumericCatalog(HelperMethods.DataTables.Buffs.Data);
            _skillCooldownsByLevel = LoadSkillCooldowns();
            var skillNames = LoadLocalizedText("SkillNames", out var skillNameIds);
            (_skillNames, _) = ApplyNameOverrides(skillNames, skillNameIds, "SkillNameOverrides.json");
            var buffNames = LoadLocalizedText("BuffNames", out var buffNameIds);
            (_buffNames, _) = ApplyNameOverrides(buffNames, buffNameIds, "BuffNameOverrides.json");
            var monsterNames = LoadLocalizedText("MonsterNames", out var monsterIds);
            (_monsterNames, _monsterIds) = ApplyNameOverrides(monsterNames, monsterIds, "MonsterNameOverrides.json");
            _dbmNames = LoadLocalizedText("DbmNames", out _);
            _rogueEntryNames = LoadLocalizedText("RogueEntryNames", out var rogueEntryBuffIds);
            _rogueEntryBuffIds = rogueEntryBuffIds;
            _seasonTalentNames = LoadLocalizedText("SeasonTalentNames", out var seasonTalentRootBuffIds);
            _seasonTalentRootBuffIds = seasonTalentRootBuffIds;
            _classSpecNames = LoadLocalizedText("ClassSpecNames", out _);
            _seasonNames = LoadLocalizedText("SeasonNames", out _);
            _seasonRankNames = LoadLocalizedText("SeasonRankNames", out _);
            _teamTargetNames = LoadLocalizedText("TeamTargetNames", out _);
            _seasonActNames = LoadLocalizedText("SeasonActNames", out _);
            _equipNames = LoadLocalizedText("EquipNames", out _);
            _equipEffectTexts = LoadLocalizedText("EquipEffectTexts", out _);
            _skillIdByEffectId = BuildSkillIdByEffectId(_skills);
            _monsterIdsBySkillId = BuildMonsterIdsBySkillId();
            _warningSkillLevelIds = LoadWarningSkillLevels();
            _warningSkillIds = _warningSkillLevelIds.Select(skillLevelId => skillLevelId / 100).ToFrozenSet();
            _dbmCountCdTimes = LoadDbmCountCdTimes();
            LoadEquipments();
            LoadBuffGroups();
            _sceneNames = LoadLocalizedText("SceneNames", out _);
            _dungeonTypeNames = LoadDungeonTypeNames();
            LoadRecounts();
        }
    }

    /// <summary>
    /// 発生源キーを作る。<b>ゲーム内メーターの行はこの粒度で決まる。</b>
    ///
    /// <para>
    /// ゲームは <c>RecountTable.DamageId</c> で行を引く。<c>DamageId</c> はワイヤに乗っていないが、
    /// <c>SyncDamageInfo</c> の <c>OwnerId</c> と <c>HitEventId</c> の組が <c>DamageId</c> と
    /// 1対1で対応する(<c>DamageId</c> の <c>TypeEnum</c> が <c>OwnerId</c>、下2桁が <c>HitEventId</c>)。
    /// </para>
    ///
    /// <para>
    /// <b><c>OwnerId</c> だけでは粗すぎる。</b> 同じ <c>OwnerId</c> の枝が別の行に入る例が26〜28種あり、
    /// 枝番を見ないとどちらか一方の行が消える(<c>2203291</c> が 臣鷹出撃 と 臣鷹の雷撃衝撃 にまたがる)。
    /// </para>
    ///
    /// <para>
    /// 詰め方は32bitシフト。<b>桁を食い合わないので、枝番がどんな値でも別の鍵と衝突しない。</b>
    /// </para>
    /// </summary>
    public static long MakeSourceKey(int ownerId, int branch)
        => ((long)ownerId << 32) | (uint)branch;

    /// <summary>発生源キーの <c>OwnerId</c> 側。</summary>
    public static int SourceKeyOwnerId(long key) => (int)(key >> 32);

    /// <summary>発生源キーの枝番(<c>HitEventId</c>)側。</summary>
    public static int SourceKeyBranch(long key) => unchecked((int)(uint)key);

    /// <summary>発生源キーの表示形。ファイルの鍵と同じ <c>ownerId:枝番</c>。</summary>
    public static string FormatSourceKey(long key)
        => $"{SourceKeyOwnerId(key)}:{SourceKeyBranch(key)}";

    private static bool TryParseSourceKey(string text, out long key)
    {
        key = 0;
        var separator = text.IndexOf(':');
        if (separator <= 0
            || !int.TryParse(text.AsSpan(0, separator), out var ownerId)
            || !int.TryParse(text.AsSpan(separator + 1), out var branch))
        {
            return false;
        }

        key = MakeSourceKey(ownerId, branch);
        return true;
    }

    /// <summary>
    /// 内部ID注記を付けないメーターの行名。<b>記録・保存する値にはこちらを使う。</b>
    ///
    /// <para>
    /// 注記は表示設定なので、付いたまま保存すると設定を切ったあとも残る。
    /// </para>
    /// </summary>
    public static string GetSourceName(long rowKey)
        => ResolveText(_recountNames, Volatile.Read(ref _cultureName), rowKey);

    /// <summary>
    /// メーターの行に出す名前。<b>ゲーム内メーターの見出し表(手修正込み)が先に決める。</b>
    ///
    /// <para>
    /// 見出し表で名前が空の行は、バフから来た行で鍵のバフが料理なら料理の名前(<c>CuisineBuffs.json</c>)。
    /// それも無ければ、記録時に付与元をたどって着いた先(<paramref name="landing"/>)の名前を出す。
    /// オプションのバフなら <c>RogueEntryNames</c>、プレイヤーが使った技なら <c>SkillNames</c>。
    /// 着いていなければ空のまま(内部ID注記だけ)。見出し表の名前をこれらで上書きすることはない。
    /// </para>
    ///
    /// <para>
    /// 引数は<b>畳んだあとの行代表キー</b>。注記もそのキーで付けるので、
    /// 「どの行か」と「注記のID」が必ず一致する。
    /// </para>
    /// </summary>
    public static string GetSourceDisplayName(long rowKey, bool isBuffSource, SourceLanding landing)
    {
        var name = GetSourceNameBeforeLanding(rowKey, isBuffSource);
        if (string.IsNullOrEmpty(name))
        {
            name = GetLandingName(landing);
        }

        return AppendSourceInternalId(
            name,
            isBuffSource ? InternalIdDisplayMode.BuffOnly : InternalIdDisplayMode.SkillOnly,
            rowKey);
    }

    /// <summary>
    /// 着地先より前に決まるメーターの行名(内部ID注記なし)。見出し表(手修正込み)、空ならバフから来た行で鍵のバフが料理なら料理の名前。
    /// どちらも無ければ空。
    ///
    /// <para>
    /// 表示(<see cref="GetSourceDisplayName"/>)と空欄の検知(着地先を決めるかどうか)の両方がこれで判定する。
    /// 片方だけ変えると、名前が出る行を空欄として報告するか、空欄の行を報告しなくなる。
    /// </para>
    /// </summary>
    public static string GetSourceNameBeforeLanding(long rowKey, bool isBuffSource)
    {
        var cultureName = Volatile.Read(ref _cultureName);
        var name = ResolveText(_recountNames, cultureName, rowKey);
        if (string.IsNullOrEmpty(name) && isBuffSource && _cuisineBuffIds.Contains(SourceKeyOwnerId(rowKey)))
        {
            name = ResolveText(_cuisineBuffNames, cultureName, SourceKeyOwnerId(rowKey));
        }

        return name;
    }

    /// <summary>着地先の名前。着いていないか名前が無ければ空。</summary>
    public static string GetLandingName(SourceLanding landing)
    {
        var cultureName = Volatile.Read(ref _cultureName);
        return landing.Kind switch
        {
            SourceLandingKind.RogueEntry => ResolveText(_rogueEntryNames, cultureName, landing.Id),
            SourceLandingKind.Skill => ResolveText(_skillNames, cultureName, landing.Id),
            _ => string.Empty
        };
    }

    /// <summary>オプションの表(<c>RogueEntryTable.BuffId</c>)にあるバフか。</summary>
    public static bool IsRogueEntryBuff(int buffId) => _rogueEntryBuffIds.Contains(buffId);

    /// <summary>シーズンタレントの型の根ノードのバフか(<c>SeasonTalentNames.json</c> の鍵にあるか)。</summary>
    public static bool IsSeasonTalentRootBuff(int buffId) => _seasonTalentRootBuffIds.Contains(buffId);

    /// <summary>
    /// シーズンタレントの型の根ノードの名前。鍵は根ノードのバフID。表示中の言語で引き、無ければ空。
    /// 内部IDの注記はバフと同じ設定で付く。
    /// </summary>
    public static string GetSeasonTalentName(int rootBuffId)
    {
        return AppendInternalId(
            ResolveText(_seasonTalentNames, Volatile.Read(ref _cultureName), rootBuffId),
            InternalIdDisplayMode.BuffOnly,
            rootBuffId);
    }

    /// <summary>特化の名前。鍵は特化の番号(<c>SubProfessionId</c>)。表示中の言語で引き、無ければ空。</summary>
    public static string GetClassSpecName(int subProfessionId)
    {
        return ResolveText(_classSpecNames, Volatile.Read(ref _cultureName), subProfessionId);
    }

    /// <summary>シーズンの名前(シーズン実績の1枠目の分類名)。表示中の言語で引き、無ければ空。</summary>
    public static string GetSeasonName(int seasonId)
    {
        return ResolveText(_seasonNames, Volatile.Read(ref _cultureName), seasonId);
    }

    /// <summary>
    /// マッチング先の名前(マッチング成立の通知の <c>MatchKeyInfo</c>)。番号の意味は種類で決まる:
    /// パーティのマッチングは <c>TeamTargetTable</c>、活動のマッチングは <c>SeasonActTable</c> の行。表示中の言語で引き、無ければ空。
    /// </summary>
    public static string GetMatchTargetName(Zproto.EMatchType matchType, long matchTypeUuid)
    {
        if (matchTypeUuid is <= 0 or > int.MaxValue)
        {
            return string.Empty;
        }

        var names = matchType switch
        {
            Zproto.EMatchType.Team => _teamTargetNames,
            Zproto.EMatchType.Activity => _seasonActNames,
            _ => null
        };
        return names is null ? string.Empty : ResolveText(names, Volatile.Read(ref _cultureName), (int)matchTypeUuid);
    }

    /// <summary>
    /// シーズンランクの名前。段階(<c>SeasonRankTable.RankToLevel</c>)の中で★が一番小さい行の名前。
    /// 表示中の言語で引き、無ければ空。
    /// </summary>
    public static string GetSeasonRankName(int seasonId, int rankLevel)
    {
        return ResolveText(_seasonRankNames, Volatile.Read(ref _cultureName), seasonId * 100 + rankLevel);
    }

    /// <summary>装備名(<c>ItemTable.Name</c>)。鍵は装備ID。表示中の言語で引き、無ければ空。</summary>
    public static string GetEquipName(int equipId)
    {
        return ResolveText(_equipNames, Volatile.Read(ref _cultureName), equipId);
    }

    /// <summary>
    /// 進化・改鋳・レアの効果(一時属性・バフ)の説明文。値の差し込みは <c>{0}</c>(そのままの値)と
    /// <c>{1}</c>(% の値。値 ÷ 100)。言語でどちらが入るかが違うことがある。
    /// 鍵は効果の番号。表示中の言語で引き、無ければ空。
    /// </summary>
    public static string GetEquipEffectText(int effectId)
    {
        return ResolveText(_equipEffectTexts, Volatile.Read(ref _cultureName), effectId);
    }

    /// <summary>装備の定義。表に無い装備IDなら <c>null</c>。</summary>
    public static EquipmentDefinition? GetEquipment(int equipId)
    {
        return _equipments.TryGetValue(equipId, out var equipment) ? equipment : null;
    }

    /// <summary>
    /// 属性庫の行を表の並び順で。<paramref name="libType"/> は 1 = <c>EquipAttrLibTable</c>、
    /// 2 = <c>EquipAttrSchoolLibTable</c>。表に無い庫なら空。
    /// </summary>
    public static IReadOnlyList<EquipmentAttrLibRow> GetEquipmentAttrLibRows(int libType, int libId)
    {
        return _equipmentAttrLibs.TryGetValue(MakeEquipmentKey(libType, libId), out var rows) ? rows : [];
    }

    /// <summary>
    /// 属性庫の行を行ID で。<paramref name="libType"/> は <see cref="GetEquipmentAttrLibRows"/> と同じ。
    /// 行ID は型ごとの番号なので、型を取り違えると別の行になる。表に無ければ <c>null</c>。
    /// </summary>
    public static EquipmentAttrLibRow? GetEquipmentAttrRow(int libType, int rowId)
    {
        return _equipmentAttrRows.TryGetValue(MakeEquipmentKey(libType, rowId), out var row) ? row : null;
    }

    /// <summary>
    /// 効果(種類と番号)がシーズン強度の項目ならそのシーズン、でなければ 0。
    /// 項目のシーズンは、その効果を基礎に持つ装備の <c>EquipTable.SeasonId</c> の積集合で決めてある。
    /// </summary>
    public static int GetEquipmentStrengthSeason(int kind, int effectId)
    {
        return _equipmentStrengthSeasons.TryGetValue(MakeEquipmentKey(kind, effectId), out var season) ? season : 0;
    }

    /// <summary>特化の番号(<c>SubProfessionId</c>)に対応する型2 の属性庫の特化。無ければ 0。</summary>
    public static int GetEquipmentSpecSchool(int subProfessionId)
    {
        return _equipmentSpecSchools.TryGetValue(subProfessionId, out var school) ? school : 0;
    }

    /// <summary>クラスR1 の人(職業ID)に対応する型2 の属性庫の特化。無ければ 0。</summary>
    public static int GetEquipmentRank1School(int professionId)
    {
        return _equipmentRank1Schools.TryGetValue(professionId, out var school) ? school : 0;
    }

    /// <summary>
    /// 発生源キー → 行代表キー。<c>RecountRows.json</c> の行構成に手修正を重ねたもの。
    ///
    /// <para>
    /// ゲーム内メーターは <c>RecountTable</c> の1行に複数の <c>DamageId</c> をまとめる。
    /// 生成物はその所属をそのまま写したもので、<b>手書きの判断は入っていない</b>。
    /// 判断が要るぶんは <c>Data/Overrides/RecountRowOverrides.json</c> にある。
    /// </para>
    /// </summary>
    public static bool TryResolveRecountRow(long key, out long rowKey)
        => _recountRows.TryGetValue(key, out rowKey);

    /// <param name="Row">
    /// 行の出入り。別の鍵ならその鍵の行へ入れる(追加)、<c>null</c> なら行から外して単独にする(削除)。
    /// 省略したら生成物のまま。
    /// </param>
    /// <param name="RowSpecified">
    /// <c>Row</c> が書かれていたか。<c>null</c> は「外す」という指示なので、
    /// 「書かなかった」と区別しないと省略が全部「外す」になる。
    /// </param>
    /// <param name="Name">言語 → 名前。書いた言語だけ差し替え、空文字は生成値を消す。</param>
    private sealed class RecountOverrideEntry
    {
        public string? Row { get; set; }

        public bool RowSpecified { get; set; }

        public Dictionary<string, string>? Name { get; set; }
    }

    /// <summary>
    /// 行構成・行名・手修正をまとめて読む。
    ///
    /// <list type="number">
    ///   <item>生成物 <c>RecountRows.json</c> の行から、行の集まりを作る</item>
    ///   <item>手修正の <c>Row</c> を当てる。<b>外すほうを先に</b>当てないと、外した鍵へ寄せられない</item>
    ///   <item>行代表を取り直す。外した鍵が代表だった行は代表が変わる</item>
    ///   <item>生成物の名前を行代表へ配り、手修正の <c>Name</c> を重ねる</item>
    /// </list>
    /// </summary>
    private static void LoadRecounts()
    {
        // 生成物は生の見出し表と同じ形。行ごとに RecountName(4言語)と、その行に属する発生源キーの一覧。
        // 項目名は SourceId。中身は TypeEnum:枝番 で、生の DamageId とは別の値。
        var layout = new Dictionary<string, RecountRow>();
        var path = Path.Combine(CombatRuntimePaths.LocalizationDirectory, "RecountRows.json");
        if (File.Exists(path))
        {
            layout = JsonConvert.DeserializeObject<Dictionary<string, RecountRow>>(File.ReadAllText(path))
                ?? throw new InvalidDataException($"{path} is empty.");
            var missingNames = 0;
            foreach (var row in layout)
            {
                row.Value.RecountName = CheckLocalizedNames("RecountRows", row.Key, row.Value.RecountName, ref missingNames);
            }

            LogMissingLocalizedNames("RecountRows", missingNames);
            Log.Information("Loaded localization {FileName}", "RecountRows");
        }
        else
        {
            Log.Error("Missing localization {FileName}", "RecountRows");
        }

        var groups = new Dictionary<long, HashSet<long>>();
        var groupOf = new Dictionary<long, long>();
        // 鍵 → 元の行。手修正で行から外した鍵が、外れたあとも元の行名を土台にできる。
        var originRow = new Dictionary<long, string>();

        foreach (var row in layout)
        {
            long? head = null;
            foreach (var text in row.Value.SourceId ?? [])
            {
                if (!TryParseSourceKey(text, out var key))
                {
                    Log.Error("recounts: row {Row} has \"{Key}\" which is not in ownerId:branch form", row.Key, text);
                    continue;
                }

                originRow[key] = row.Key;
                if (head is null)
                {
                    groups[key] = [key];
                    groupOf[key] = key;
                    head = key;
                }
                else
                {
                    Join(groups, groupOf, key, head.Value);
                }
            }
        }

        var overrides = LoadRecountOverrides();

        // 外すほうを先に。寄せ先が「外したばかりの鍵」であることがある。
        foreach (var entry in overrides)
        {
            if (entry.Value.RowSpecified && entry.Value.Row is null)
            {
                Detach(groups, groupOf, entry.Key);
            }
        }

        foreach (var entry in overrides)
        {
            if (!entry.Value.RowSpecified || entry.Value.Row is null)
            {
                continue;
            }

            if (!TryParseSourceKey(entry.Value.Row, out var target))
            {
                Log.Error("RecountRowOverrides: {Key} has Row \"{Row}\" which is not in ownerId:branch form",
                    FormatSourceKey(entry.Key), entry.Value.Row);
                continue;
            }

            Join(groups, groupOf, entry.Key, target);
        }

        // 行代表は行の最小キー。ownerId → 枝番 の順で比べる。
        var repOf = new Dictionary<long, long>();
        var membersOfRep = new Dictionary<long, List<long>>();
        foreach (var group in groups)
        {
            var members = group.Value.OrderBy(k => SourceKeyOwnerId(k)).ThenBy(SourceKeyBranch).ToList();
            var rep = members[0];
            membersOfRep[rep] = members;
            foreach (var key in members)
            {
                repOf[key] = rep;
            }
        }

        _recountRows = repOf.ToFrozenDictionary();
        _recountNames = BuildRecountNames(membersOfRep, originRow, layout, repOf, overrides);
        Log.Information("Loaded {Keys} recount keys / {Rows} rows / {Overrides} overrides",
            repOf.Count, groups.Count, overrides.Count);
    }

    /// <param name="RecountName">行名。</param>
    /// <param name="SourceId">
    /// その行に属する発生源キー(<c>ownerId:枝番</c>)。
    /// <b><c>DamageId</c> とは別の値</b>なので、生の見出し表と同じ名前は使わない。
    /// </param>
    private sealed class RecountRow
    {
        /// <summary>言語 → 行名。</summary>
        public Dictionary<string, string>? RecountName { get; set; }

        public List<string>? SourceId { get; set; }
    }

    private static void Join(
        Dictionary<long, HashSet<long>> groups,
        Dictionary<long, long> groupOf,
        long key,
        long target)
    {
        // 先に今いる行から抜く。抜き忘れると空になった行が残り、
        // 代表を取り直すときにその行が「鍵ひとつだけの行」として復活してしまう。
        Remove(groups, groupOf, key);

        // 寄せ先がまだどの行にも属していなければ、その鍵だけの行を先に作る。
        if (!groupOf.TryGetValue(target, out var groupId))
        {
            groupId = target;
            groups[groupId] = [target];
            groupOf[target] = groupId;
        }

        groups[groupId].Add(key);
        groupOf[key] = groupId;
    }

    private static void Detach(
        Dictionary<long, HashSet<long>> groups,
        Dictionary<long, long> groupOf,
        long key)
    {
        Remove(groups, groupOf, key);
        groups[key] = [key];
        groupOf[key] = key;
    }

    private static void Remove(
        Dictionary<long, HashSet<long>> groups,
        Dictionary<long, long> groupOf,
        long key)
    {
        if (!groupOf.TryGetValue(key, out var groupId) || !groups.TryGetValue(groupId, out var members))
        {
            return;
        }

        members.Remove(key);
        groupOf.Remove(key);

        if (members.Count == 0)
        {
            groups.Remove(groupId);
            return;
        }

        // 行の識別子はメンバーの鍵そのもの。その鍵を抜いたら残りのメンバーで付け直す。
        // 付け直さないと、抜いた鍵で行を作り直したときに残りのメンバーごと消える。
        if (groupId == key)
        {
            groups.Remove(groupId);
            var next = members.First();
            groups[next] = members;
            foreach (var member in members)
            {
                groupOf[member] = next;
            }
        }
    }

    /// <summary>
    /// <c>Data/Overrides/</c> の名前の上書きを、<c>Data/Localization/</c> の名前の表に重ねる。形は
    /// <c>RecountRowOverrides</c> の名前と同じで、書いた言語だけ差し替わる(空文字はその言語の名前を消し、以後は zh-CN へ落ちる)。
    ///
    /// <para>
    /// 生成物に無い番号も書け、名前の表に行がある扱いになる。<c>Name</c> の無い項目は行を作らずに飛ばす。
    /// 鍵が番号でない・言語名の打ち間違いはエラーログを出して飛ばす(静かに効かないので)。
    /// </para>
    /// </summary>
    private static (FrozenDictionary<string, FrozenDictionary<int, string>> Names, FrozenSet<int> Ids) ApplyNameOverrides(
        FrozenDictionary<string, FrozenDictionary<int, string>> names,
        FrozenSet<int> ids,
        string fileName)
    {
        var relativePath = $"Overrides/{fileName}";
        var path = Path.Combine(CombatRuntimePaths.OverridesDirectory, fileName);
        if (!File.Exists(path))
        {
            Log.Error("Failed to load {OverridePath}", relativePath);
            return (names, ids);
        }

        var overrides = JsonConvert.DeserializeObject<Dictionary<string, NameOverrideEntry?>>(File.ReadAllText(path))
            ?? throw new InvalidDataException($"{relativePath} が空です。");
        var merged = names.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ToDictionary(),
            StringComparer.OrdinalIgnoreCase);
        var mergedIds = ids.ToHashSet();

        foreach (var (key, entry) in overrides)
        {
            if (!int.TryParse(key, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var id)
                || id <= 0)
            {
                Log.Error("{OverridePath}: skipping \"{Key}\" because the key is not numeric", relativePath, key);
                continue;
            }

            if (entry?.Name is null)
            {
                Log.Error("{OverridePath}: skipping {Key} because it has no Name", relativePath, key);
                continue;
            }

            mergedIds.Add(id);
            foreach (var (culture, value) in entry.Name)
            {
                if (!merged.TryGetValue(culture, out var namesById))
                {
                    Log.Error(
                        "{OverridePath}: {Key} に未知の言語 \"{Culture}\"。言語は {Cultures}",
                        relativePath, key, culture, string.Join(" / ", SupportedCultures));
                    continue;
                }

                var text = value?.Trim() ?? string.Empty;
                if (text.Length == 0)
                {
                    namesById.Remove(id);
                }
                else
                {
                    namesById[id] = text;
                }
            }
        }

        Log.Information("Loaded {OverridePath}", relativePath);
        return (
            merged.ToFrozenDictionary(
                pair => pair.Key,
                pair => pair.Value.ToFrozenDictionary(),
                StringComparer.OrdinalIgnoreCase),
            mergedIds.ToFrozenSet());
    }

    private sealed class NameOverrideEntry
    {
        public Dictionary<string, string?>? Name { get; set; }
    }

    private static Dictionary<long, RecountOverrideEntry> LoadRecountOverrides()
    {
        var result = new Dictionary<long, RecountOverrideEntry>();
        var path = Path.Combine(CombatRuntimePaths.OverridesDirectory, "RecountRowOverrides.json");
        if (!File.Exists(path))
        {
            Log.Error("Failed to load {OverridePath}", "Overrides/RecountRowOverrides.json");
            return result;
        }

        // Row に null が書かれたのか、そもそも書かれなかったのかを見分けるため、
        // 一度 JObject で受けてからキーの有無を見る。
        var raw = JsonConvert.DeserializeObject<Dictionary<string, Newtonsoft.Json.Linq.JObject>>(
            File.ReadAllText(path));
        foreach (var pair in raw ?? [])
        {
            if (!TryParseSourceKey(pair.Key, out var key))
            {
                Log.Error("RecountRowOverrides: skipping \"{Key}\" because the key is not in ownerId:branch form", pair.Key);
                continue;
            }

            var rowToken = pair.Value["Row"];
            var entry = new RecountOverrideEntry
            {
                RowSpecified = rowToken is not null,
                Row = rowToken?.Type == Newtonsoft.Json.Linq.JTokenType.String
                    ? (string?)rowToken
                    : null,
            };

            if (pair.Value["Name"] is Newtonsoft.Json.Linq.JObject names)
            {
                entry.Name = [];
                foreach (var name in names)
                {
                    // 言語名の打ち間違いは静かに効かないので、必ず出す。
                    if (!SupportedCultures.Contains(name.Key, StringComparer.OrdinalIgnoreCase))
                    {
                        Log.Error(
                            "RecountRowOverrides: {Key} に未知の言語 \"{Culture}\"。言語は {Cultures}",
                            pair.Key, name.Key, string.Join(" / ", SupportedCultures));
                        continue;
                    }

                    entry.Name[name.Key] = ((string?)name.Value)?.Trim() ?? string.Empty;
                }
            }

            result[key] = entry;
        }

        return result;
    }

    /// <summary>
    /// 行代表キー → 表示名を、言語ごとに組む。
    ///
    /// <para>
    /// 生成物は行ごとに名前を持つので、<b>行代表がどの行から来たか</b>で引く。
    /// 手修正で行が組み替わると、代表が生成物に無い鍵になることがある(総括行から戻した鍵)。
    /// そのときは同じ行の別のメンバーの元の行名で埋める。
    /// </para>
    /// </summary>
    private static FrozenDictionary<string, FrozenDictionary<long, string>> BuildRecountNames(
        Dictionary<long, List<long>> membersOfRep,
        Dictionary<long, string> originRow,
        Dictionary<string, RecountRow> rows,
        Dictionary<long, long> repOf,
        Dictionary<long, RecountOverrideEntry> overrides)
    {
        var result = new Dictionary<string, FrozenDictionary<long, string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var cultureName in SupportedCultures)
        {
            var names = new Dictionary<long, string>();

            foreach (var group in membersOfRep)
            {
                foreach (var member in group.Value)
                {
                    if (originRow.TryGetValue(member, out var rowId)
                        && rows.TryGetValue(rowId, out var row)
                        && row.RecountName is not null
                        && row.RecountName.TryGetValue(cultureName, out var rowName)
                        && !string.IsNullOrWhiteSpace(rowName))
                    {
                        names[group.Key] = rowName.Trim();
                        break;
                    }
                }
            }

            foreach (var entry in overrides)
            {
                if (entry.Value.Name is null
                    || !entry.Value.Name.TryGetValue(cultureName, out var text))
                {
                    continue;
                }

                // 名前は行のもの。畳まれる鍵に書いても行代表へ届く。
                var rep = repOf.TryGetValue(entry.Key, out var known) ? known : entry.Key;
                if (string.IsNullOrEmpty(text))
                {
                    names.Remove(rep);
                }
                else
                {
                    names[rep] = text;
                }
            }

            result[cultureName] = names.ToFrozenDictionary();
        }

        return result.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    public static string GetSkillName(int skillId)
    {
        return AppendInternalId(
            ResolveText(_skillNames, Volatile.Read(ref _cultureName), skillId),
            InternalIdDisplayMode.SkillOnly,
            skillId);
    }

    /// <summary>内部ID注記を付けない技名。名前が取れていなければ空を返す。</summary>
    public static string GetSkillNameWithoutInternalId(int skillId)
    {
        return ResolveText(_skillNames, Volatile.Read(ref _cultureName), skillId);
    }

    /// <summary>
    /// ボス大技の予告(<c>DbmTable</c>)に載っている技の名前。内部ID注記は付けない。載っていなければ空。
    ///
    /// <para>
    /// ゲームが予告に出す正式な技名で、<c>SkillTable.Name</c> が埋め草の技にも名前がある。
    /// 鍵は技ID(生成時に <c>EffectIDs</c> の要素から持ち主の技へ寄せてある)。
    /// </para>
    /// </summary>
    public static string GetDbmNameWithoutInternalId(int skillId)
    {
        return ResolveText(_dbmNames, Volatile.Read(ref _cultureName), skillId);
    }

    /// <summary>
    /// 弾(<c>Bullet</c> / <c>FakeBullet</c> 由来のダメージの <c>OwnerId</c>)から親の技IDを引く。
    ///
    /// <para>
    /// <c>OwnerId</c> は <c>BulletTable</c> の番号で、<c>BulletTable</c> には技を指す項目が無い。
    /// <c>SkillTable</c> にその番号があればその技、無ければ <c>SkillFightLevelTable</c> の同じ番号の行の
    /// <c>SkillId</c>(<c>SkillTable</c> にあるもの)を親とする。どちらにも無ければ辿れない。
    /// </para>
    /// </summary>
    public static bool TryResolveBulletParentSkillId(int bulletId, out int skillId)
    {
        if (_skills.ContainsKey(bulletId))
        {
            skillId = bulletId;
            return true;
        }

        if (HelperMethods.DataTables.SkillFightLevels.Data.TryGetValue(
                bulletId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                out var fightLevel)
            && _skills.ContainsKey(fightLevel.SkillId))
        {
            skillId = fightLevel.SkillId;
            return true;
        }

        skillId = 0;
        return false;
    }

    /// <summary>
    /// 被ダメ1件が当たった技。<b>記録時(レベルを引く対象)と表示時(ギミック技の判定)で同じ決め方にするため、ここだけで決める。</b>
    /// 技由来はその技、弾由来は親の技(<see cref="TryResolveBulletParentSkillId"/>)、バフ由来は付与元の技(<paramref name="buffSourceSkillId"/>)。
    /// それ以外(落下など)と、辿れないものは 0。
    /// </summary>
    public static int ResolveHitSkillId(Zproto.EDamageSource damageSource, int ownerId, int buffSourceSkillId)
    {
        return damageSource switch
        {
            Zproto.EDamageSource.Skill => ownerId,
            Zproto.EDamageSource.Bullet or Zproto.EDamageSource.FakeBullet =>
                TryResolveBulletParentSkillId(ownerId, out var parentSkillId) ? parentSkillId : 0,
            Zproto.EDamageSource.Buff => buffSourceSkillId,
            _ => 0
        };
    }

    /// <summary>呼び出し側で決めた名前(空のときの代わりの名前など)に、技の内部ID注記を添える。</summary>
    public static string AppendSkillInternalId(string name, int skillId)
    {
        return AppendInternalId(name, InternalIdDisplayMode.SkillOnly, skillId);
    }

    /// <summary>呼び出し側で決めた名前に、バフの内部ID注記を添える。</summary>
    public static string AppendBuffInternalId(string name, int buffId)
    {
        return AppendInternalId(name, InternalIdDisplayMode.BuffOnly, buffId);
    }

    /// <summary>
    /// 詠唱(または誘導)のバーを持つ技か。<c>SkillTable.SingOrGuideTime</c> の先頭要素の全体秒数が 0 より大きい。
    ///
    /// <para>
    /// サーバは「詠唱中」を送らない。ゲームは技の開始とこの設定から詠唱バーを出す。
    /// </para>
    /// </summary>
    public static bool HasSingOrGuideTime(int skillId)
    {
        return _skills.TryGetValue(skillId, out var skill)
            && skill.SingOrGuideTime is [[> 0f, ..], ..];
    }

    /// <summary>
    /// 戦闘画面の警告バーを出す技か(<c>Data/Generated/SkillWarnings.json</c>)。
    /// <b>技レベルで決まる</b>(同じ技でもレベルによって持たないことがある)ので、技IDとレベルの両方で引く。
    /// </summary>
    public static bool IsWarningSkill(int skillId, int skillLevel)
    {
        var skillLevelId = (long)skillId * 100 + skillLevel;
        return skillLevelId <= int.MaxValue && _warningSkillLevelIds.Contains((int)skillLevelId);
    }

    /// <summary>どれかのレベルで戦闘画面の警告バーを出す技か。レベルが分からないときの判定に使う。</summary>
    public static bool IsWarningSkillId(int skillId)
    {
        return _warningSkillIds.Contains(skillId);
    }

    /// <summary>予告の番号が予告の表にあれば、バーの既定の秒数(<c>CountCDTime</c>)を返す。表に無い番号のバーはゲームも出さない。</summary>
    public static bool TryGetDbmCountCdTime(int dbmId, out int seconds)
    {
        return _dbmCountCdTimes.TryGetValue(dbmId, out seconds);
    }

    /// <summary>
    /// ボス大技の予告の通知(<c>DbmTable.Id</c>)から技IDを引く。
    /// <c>SkillTable</c> にその番号があればその技、無ければその番号を <c>EffectIDs</c> に持つ技。
    /// </summary>
    public static bool TryResolveDbmSkillId(int dbmId, out int skillId)
    {
        if (_skills.ContainsKey(dbmId))
        {
            skillId = dbmId;
            return true;
        }

        return _skillIdByEffectId.TryGetValue(dbmId, out skillId);
    }

    /// <summary>モンスター(種別ID)が <c>MonsterTable.SkillIds</c> にその技を持つか。</summary>
    public static bool MonsterHasSkill(int monsterId, int skillId)
    {
        return _monsterIdsBySkillId.TryGetValue(skillId, out var monsterIds)
            && monsterIds.Contains(monsterId);
    }

    /// <summary>
    /// 内部ID注記を付けないバフ名。<b>記憶・保存する値にはこちらを使う。</b>
    ///
    /// <para>
    /// 注記は表示設定なので、付いたまま控えると設定を切ったあとも残る。
    /// 名前が取れていなければ空を返すので、「まだ分かっていない」の判定にも使える。
    /// </para>
    /// </summary>
    public static string GetBuffNameWithoutInternalId(int buffId)
    {
        return ResolveBuffName(Volatile.Read(ref _cultureName), buffId);
    }

    public static string GetBuffName(int buffId)
    {
        return AppendInternalId(
            ResolveBuffName(Volatile.Read(ref _cultureName), buffId),
            InternalIdDisplayMode.BuffOnly,
            buffId);
    }

    /// <summary>
    /// バフの名前。料理・薬剤のバフはその表の名前(付けるアイテムの名前)、それ以外は <c>BuffNames</c>。
    /// 料理・薬剤のバフでも、その表の名前が全言語で空なら <c>BuffNames</c> へ落とさず空を返す。
    /// </summary>
    private static string ResolveBuffName(string cultureName, int buffId)
    {
        return GetBuffGroup(buffId) switch
        {
            BuffGroup.Cuisine => ResolveText(_cuisineBuffNames, cultureName, buffId),
            BuffGroup.Potion => ResolveText(_potionBuffNames, cultureName, buffId),
            _ => ResolveText(_buffNames, cultureName, buffId)
        };
    }

    /// <summary>
    /// モンスター名。<b>表示中の言語で引く。</b>
    ///
    /// <para>
    /// 引数は <c>AttrId</c>(<c>MonsterTable</c> のキー＝種別ID)。エンティティ側が持っている
    /// 名前は起動時に英語で焼き付くので、言語切替に追従させるにはここを通す。
    /// </para>
    /// </summary>
    /// <summary>表示名が空でないか。内部ID注記は数えない(注記は表示設定で付くので、名前の有無の判定に使えない)。</summary>
    public static bool HasMonsterName(long monsterId)
    {
        return monsterId is > 0 and <= int.MaxValue
            && !string.IsNullOrEmpty(ResolveText(_monsterNames, Volatile.Read(ref _cultureName), (int)monsterId));
    }

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
    /// エンティティリストの実体の名前。<b>表示中の言語で引く。</b>
    ///
    /// <para>
    /// 引数は実体の種類と <c>AttrId</c>(種別ID)。引く表は種類で決まる(いまはモンスターだけ)。
    /// 同じ番号が別の表で別のものを指すので、種類を取り違えると別の実体の名前になる。
    /// 表を持たない種類は空。
    /// </para>
    ///
    /// <para>
    /// <paramref name="unnamedLabel"/> は、表に行があって名前が空のときに代わりに出す文字列(「敵」「味方」)。
    /// <b>表に行が無い番号には当てない。</b> 表が古いことを、名前の無い雑魚と見分けられるように空欄のまま(内部ID注記だけ)にする。
    /// </para>
    /// </summary>
    public static string GetEntityName(Zproto.EEntityType entityType, long entityId, string? unnamedLabel = null)
    {
        if (GetEntityNameTable(entityType) is not { } table || entityId is not (> 0 and <= int.MaxValue))
        {
            return string.Empty;
        }

        var name = ResolveText(table.Names, Volatile.Read(ref _cultureName), (int)entityId);
        if (string.IsNullOrEmpty(name) && unnamedLabel is not null && table.Ids.Contains((int)entityId))
        {
            name = unnamedLabel;
        }

        return AppendInternalId(name, InternalIdDisplayMode.EntityOnly, entityId);
    }

    /// <summary><see cref="GetEntityName"/> の名前(内部ID注記を除く)が空でないか。</summary>
    public static bool HasEntityName(Zproto.EEntityType entityType, long entityId)
    {
        return GetEntityNameTable(entityType) is { } table
            && entityId is > 0 and <= int.MaxValue
            && !string.IsNullOrEmpty(ResolveText(table.Names, Volatile.Read(ref _cultureName), (int)entityId));
    }

    /// <summary>
    /// 種類の表にその番号の行があるか。名前が空の行もある扱い。
    /// 「行があって名前が空」と「表に行が無い(表が古い)」を見分けるのに使う。
    /// </summary>
    public static bool HasEntityRow(Zproto.EEntityType entityType, long entityId)
    {
        return GetEntityNameTable(entityType) is { } table
            && entityId is > 0 and <= int.MaxValue
            && table.Ids.Contains((int)entityId);
    }

    /// <summary>
    /// そのモンスターがゲーム内でプレイヤーに見える HP バーを持つか。エンティティリストと被ダメログが同じ判定を使う。
    ///
    /// <para>
    /// <c>MonsterTable.HudShowParam</c> の3番目が HP バーを出すか。値が1つの形は全部の位置がその値。
    /// 先頭は HP バーではない(名前はあって HP バーの無い実体、画面に名前が無くて HP バーのある実体がいる)。
    /// 先頭は画面に名前を出すかと見ているが推定(表に名前があっても画面に出ない実体がいる)。
    /// </para>
    ///
    /// <para>
    /// 表に無い番号・<c>HudShowParam</c> が空か2つのときは判定できないので、持つ側に倒す。
    /// </para>
    /// </summary>
    public static bool HasMonsterHpBar(long monsterId)
    {
        if (monsterId is not (> 0 and <= int.MaxValue)
            || !HelperMethods.DataTables.Monsters.Data.TryGetValue(
                monsterId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                out var monster)
            || monster.HudShowParam is not { } hudShowParam)
        {
            return true;
        }

        return hudShowParam.Count switch
        {
            1 => hudShowParam[0] != 0,
            >= 3 => hudShowParam[2] != 0,
            _ => true
        };
    }

    private static (FrozenDictionary<string, FrozenDictionary<int, string>> Names, FrozenSet<int> Ids)? GetEntityNameTable(
        Zproto.EEntityType entityType)
    {
        return entityType switch
        {
            Zproto.EEntityType.EntMonster => (_monsterNames, _monsterIds),
            _ => null
        };
    }

    /// <summary>
    /// シーン/ダンジョン名。<b>表示中の言語で引く。</b>
    ///
    /// <para>
    /// 引数は <c>LevelMapId</c> と、ダンジョン同期で届いた難易度(<c>EncounterExData.DungeonDifficulty</c>)。
    /// 履歴は名前ではなくIDと難易度を保持し、表示時にここで引き直す。
    /// </para>
    ///
    /// <para>
    /// 難易度名があれば <c>ダンジョン名-難易度名</c> にする。マスターの段階の名前(<c>番号:段階</c>)を先に引き、
    /// 無ければダンジョンごとの名前(<c>番号</c>)。マスターのダンジョンは後者を持たないので、
    /// 段階が分からなければ難易度名を付けない。
    /// </para>
    /// </summary>
    /// <summary>
    /// その職業が出す攻撃力の属性番号(物理 11330 か魔法 11340)。
    /// 表に行が無ければ 0。<b>直書きの分岐で持たない</b> — ゲームの表とずれる。
    /// </summary>
    public static int GetProfessionAttackAttrId(int professionId)
        => GetProfessionShownAttrId(professionId, static row => row.AttackShow);

    /// <summary>
    /// その職業が出す主ステータスの属性番号(筋力 11010 / 知力 11020 / 敏捷 11030)。
    /// 表に行が無ければ 0。
    /// </summary>
    public static int GetProfessionPrimaryStatAttrId(int professionId)
        => GetProfessionShownAttrId(professionId, static row => row.StrOrIntOrDexShow);

    /// <summary>
    /// 表の <c>[[種別, 番号], …]</c> から番号を1つ取る。形が想定と違えば 0。
    /// </summary>
    private static int GetProfessionShownAttrId(
        int professionId,
        Func<DataTypes.ProfessionSystem, List<List<int>>?> select)
    {
        if (professionId <= 0
            || !HelperMethods.DataTables.ProfessionSystems.Data.TryGetValue(
                professionId.ToString(), out var row))
        {
            return 0;
        }

        var pairs = select(row);
        if (pairs is null)
        {
            return 0;
        }

        foreach (var pair in pairs)
        {
            if (pair is { Count: >= 2 } && pair[1] > 0)
            {
                return pair[1];
            }
        }

        return 0;
    }

    public static string GetSceneName(long levelMapId, int dungeonDifficulty)
    {
        if (levelMapId is not (> 0 and <= int.MaxValue))
        {
            return string.Empty;
        }

        var cultureName = Volatile.Read(ref _cultureName);
        var name = ResolveText(_sceneNames, cultureName, (int)levelMapId);
        var typeName = dungeonDifficulty > 0
            ? ResolveText(_dungeonTypeNames, cultureName, MakeDungeonTypeKey((int)levelMapId, dungeonDifficulty))
            : string.Empty;
        if (string.IsNullOrEmpty(typeName))
        {
            typeName = ResolveText(_dungeonTypeNames, cultureName, MakeDungeonTypeKey((int)levelMapId, 0));
        }

        if (!string.IsNullOrEmpty(typeName))
        {
            name = $"{name}{DungeonTypeNameSeparator}{typeName}";
        }

        return AppendInternalId(name, InternalIdDisplayMode.MapOnly, levelMapId);
    }

    /// <summary>難易度名の鍵。段階 0 がダンジョンごとの名前、正の段階がマスターの段階の名前。</summary>
    private static long MakeDungeonTypeKey(int dungeonId, int difficulty)
        => ((long)dungeonId << 32) | (uint)difficulty;

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

    /// <summary>
    /// バフの表の絵(<c>Icon</c>)。HUD 用の絵は見ない(装備の TIPS の効果の行はこの絵を使う)。無ければ空。
    /// </summary>
    public static string GetBuffTableIconName(int buffId)
    {
        return _buffs.TryGetValue(buffId, out var buff)
            ? buff.Icon?.Trim() ?? string.Empty
            : string.Empty;
    }

    /// <summary>一時属性の表の絵(<c>AttrIcon</c>)。無ければ空。</summary>
    public static string GetTempAttrIconName(int tempAttrId)
    {
        return HelperMethods.DataTables.TempAttrs.Data.TryGetValue(
                tempAttrId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                out var tempAttr)
            ? tempAttr.AttrIcon?.Trim() ?? string.Empty
            : string.Empty;
    }

    /// <summary>
    /// 個別に扱わず1つのまとまりとして見るバフか。<c>Data/Localization/CuisineBuffs.json</c> にあれば料理、
    /// <c>Data/Localization/PotionBuffs.json</c> にあれば薬剤。選ぶ条件は <c>DataTools/gen_buff_groups.py</c> が持つ。
    /// </summary>
    public static BuffGroup GetBuffGroup(int buffId)
    {
        if (_cuisineBuffIds.Contains(buffId))
        {
            return BuffGroup.Cuisine;
        }

        return _potionBuffIds.Contains(buffId)
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

    private static FrozenDictionary<int, int> BuildSkillIdByEffectId(FrozenDictionary<int, Skill> skills)
    {
        var owners = new Dictionary<int, HashSet<int>>();
        foreach (var (skillId, skill) in skills)
        {
            foreach (var effectId in skill.EffectIDs ?? [])
            {
                if (!owners.TryGetValue(effectId, out var skillIds))
                {
                    skillIds = [];
                    owners[effectId] = skillIds;
                }

                skillIds.Add(skillId);
            }
        }

        return owners
            .Where(pair => pair.Value.Count == 1)
            .ToFrozenDictionary(pair => pair.Key, pair => pair.Value.First());
    }

    private static FrozenDictionary<int, FrozenSet<int>> BuildMonsterIdsBySkillId()
    {
        var result = new Dictionary<int, HashSet<int>>();
        foreach (var (key, monster) in HelperMethods.DataTables.Monsters.Data)
        {
            if (!int.TryParse(key, out var monsterId))
            {
                continue;
            }

            foreach (var skillId in monster.SkillIds ?? [])
            {
                if (!result.TryGetValue(skillId, out var monsterIds))
                {
                    monsterIds = [];
                    result[skillId] = monsterIds;
                }

                monsterIds.Add(monsterId);
            }
        }

        return result.ToFrozenDictionary(pair => pair.Key, pair => pair.Value.ToFrozenSet());
    }

    /// <summary>形は技レベルIDの配列。ファイルが無ければ空(警告の技は0件)。</summary>
    private static FrozenSet<int> LoadWarningSkillLevels()
    {
        var path = Path.Combine(CombatRuntimePaths.GeneratedDirectory, "SkillWarnings.json");
        if (!File.Exists(path))
        {
            return FrozenSet<int>.Empty;
        }

        var skillLevelIds = JsonConvert.DeserializeObject<int[]>(File.ReadAllText(path))
            ?? throw new InvalidDataException($"{path} is empty.");
        return skillLevelIds.ToFrozenSet();
    }

    /// <summary>
    /// 予告の表の番号と既定の秒数。ファイルが無ければエラーを出して空(予告のバーの終わりが決まらないので、予兆技の予告は通知されない)。
    /// </summary>
    private static FrozenDictionary<int, int> LoadDbmCountCdTimes()
    {
        var path = Path.Combine(CombatRuntimePaths.RawTableDirectory, "DbmTable.json");
        if (!File.Exists(path))
        {
            Log.Error("DbmTable.json is missing. Boss announcement bars cannot be timed path={Path}", path);
            return FrozenDictionary<int, int>.Empty;
        }

        var rows = JsonConvert.DeserializeObject<Dictionary<string, DbmTableRow>>(File.ReadAllText(path))
            ?? throw new InvalidDataException($"{path} is empty.");
        return rows.Values.ToFrozenDictionary(row => row.Id, row => row.CountCDTime);
    }

    private sealed class DbmTableRow
    {
        public int Id { get; set; }

        public int CountCDTime { get; set; }
    }

    /// <summary>装備の表の2つ組の鍵(属性庫の型と庫ID、効果の種類と番号)。詰め方は <see cref="MakeSourceKey"/> と同じ。</summary>
    private static long MakeEquipmentKey(int first, int second)
        => ((long)first << 32) | (uint)second;

    /// <summary>
    /// 装備の定義 <c>Data/Generated/Equips.json</c> を読む。<c>DataTools/gen_equips.py</c> が生成する。
    ///
    /// <para>
    /// ファイルが無い・形が合わないときはエラーログを出して全部空のまま続ける(装備詳細は定義を出せない)。
    /// 読めたところまでで続けることはしない。
    /// </para>
    /// </summary>
    private static void LoadEquipments()
    {
        const string relativePath = "Generated/Equips.json";
        var path = Path.Combine(CombatRuntimePaths.GeneratedDirectory, "Equips.json");
        if (!File.Exists(path))
        {
            Log.Error("{FileName} is missing. Equipment details cannot be shown path={Path}", relativePath, path);
            return;
        }

        FrozenDictionary<int, EquipmentDefinition> equipments;
        FrozenDictionary<long, IReadOnlyList<EquipmentAttrLibRow>> attrLibs;
        FrozenDictionary<long, EquipmentAttrLibRow> attrRows;
        FrozenDictionary<long, int> strengthSeasons;
        FrozenDictionary<int, int> specSchools;
        FrozenDictionary<int, int> rank1Schools;
        try
        {
            var file = JsonConvert.DeserializeObject<EquipmentFile>(File.ReadAllText(path))
                ?? throw new InvalidDataException("the file is empty");
            equipments = ReadEquipmentDefinitions(file.Equips);
            attrLibs = ReadEquipmentAttrLibs(file.AttrLibs, out attrRows);
            strengthSeasons = ReadEquipmentStrengthSeasons(file.StrengthSeasons);
            specSchools = ReadEquipmentSchools(file.SpecSchools, "SpecSchools");
            rank1Schools = ReadEquipmentSchools(file.Rank1Schools, "Rank1Schools");
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Log.Error(ex, "{FileName} could not be read. Equipment details cannot be shown path={Path}", relativePath, path);
            return;
        }

        _equipments = equipments;
        _equipmentAttrLibs = attrLibs;
        _equipmentAttrRows = attrRows;
        _equipmentStrengthSeasons = strengthSeasons;
        _equipmentSpecSchools = specSchools;
        _equipmentRank1Schools = rank1Schools;
        Log.Information(
            "Loaded {FileName}: {Equips} equips / {AttrLibs} attribute libraries / {StrengthSeasons} strength seasons / {SpecSchools} spec schools / {Rank1Schools} rank-1 schools",
            relativePath, equipments.Count, attrLibs.Count, strengthSeasons.Count, specSchools.Count, rank1Schools.Count);
    }

    private static FrozenDictionary<int, EquipmentDefinition> ReadEquipmentDefinitions(
        Dictionary<string, EquipmentEntry?> entries)
    {
        var result = new Dictionary<int, EquipmentDefinition>(entries.Count);
        foreach (var (key, entry) in entries)
        {
            var equipId = ParseEquipmentKey(key, "Equips");
            var equip = RequireEquipmentItem(entry, $"Equips.{key}");
            if (equip.Stages.Count == 0)
            {
                throw new InvalidDataException($"Equips.{key} has no stages");
            }

            var stages = new EquipmentStage[equip.Stages.Count];
            for (var index = 0; index < stages.Length; index++)
            {
                var stage = RequireEquipmentItem(equip.Stages[index], $"Equips.{key}.Stages[{index}]");
                stages[index] = new EquipmentStage(stage.Gs, stage.Basic.ToArray(), stage.Advanced.ToArray());
            }

            result[equipId] = new EquipmentDefinition(
                equipId,
                equip.Part,
                equip.Quality,
                equip.PerfectUpperLimit,
                equip.MainStat,
                stages,
                equip.Recast.ToArray(),
                equip.Rare.ToArray());
        }

        return result.ToFrozenDictionary();
    }

    /// <param name="rowsById">
    /// 読んだ行を <see cref="MakeEquipmentKey"/>(型, 行ID)で引く索引。同じ型に同じ行ID が2回出たら形の誤り。
    /// </param>
    private static FrozenDictionary<long, IReadOnlyList<EquipmentAttrLibRow>> ReadEquipmentAttrLibs(
        Dictionary<string, Dictionary<string, List<EquipmentAttrLibRowEntry?>?>?> entries,
        out FrozenDictionary<long, EquipmentAttrLibRow> rowsById)
    {
        var result = new Dictionary<long, IReadOnlyList<EquipmentAttrLibRow>>();
        var byId = new Dictionary<long, EquipmentAttrLibRow>();
        foreach (var (typeKey, libs) in entries)
        {
            var libType = ParseEquipmentKey(typeKey, "AttrLibs");
            foreach (var (libKey, rows) in RequireEquipmentItem(libs, $"AttrLibs.{typeKey}"))
            {
                var libId = ParseEquipmentKey(libKey, $"AttrLibs.{typeKey}");
                var libPath = $"AttrLibs.{typeKey}.{libKey}";
                var rowEntries = RequireEquipmentItem(rows, libPath);
                var libRows = new EquipmentAttrLibRow[rowEntries.Count];
                for (var rowIndex = 0; rowIndex < libRows.Length; rowIndex++)
                {
                    var rowPath = $"{libPath}[{rowIndex}]";
                    var row = RequireEquipmentItem(rowEntries[rowIndex], rowPath);
                    var effects = new EquipmentAttrEffect[row.Effects.Count];
                    for (var effectIndex = 0; effectIndex < effects.Length; effectIndex++)
                    {
                        var effect = RequireEquipmentItem(row.Effects[effectIndex], $"{rowPath}.Effects[{effectIndex}]");
                        effects[effectIndex] = new EquipmentAttrEffect(effect.Kind, effect.Id, effect.Min, effect.Max, effect.Format);
                    }

                    libRows[rowIndex] = new EquipmentAttrLibRow(row.Id, row.Parts.ToArray(), row.Specs.ToArray(), effects);
                    if (!byId.TryAdd(MakeEquipmentKey(libType, row.Id), libRows[rowIndex]))
                    {
                        throw new InvalidDataException($"AttrLibs.{typeKey} has row {row.Id} more than once");
                    }
                }

                result[MakeEquipmentKey(libType, libId)] = libRows;
            }
        }

        rowsById = byId.ToFrozenDictionary();
        return result.ToFrozenDictionary();
    }

    private static FrozenDictionary<long, int> ReadEquipmentStrengthSeasons(List<EquipmentStrengthSeasonEntry?> entries)
    {
        var result = new Dictionary<long, int>(entries.Count);
        for (var index = 0; index < entries.Count; index++)
        {
            var entry = RequireEquipmentItem(entries[index], $"StrengthSeasons[{index}]");
            if (!result.TryAdd(MakeEquipmentKey(entry.Kind, entry.Id), entry.Season))
            {
                throw new InvalidDataException($"StrengthSeasons has kind {entry.Kind} id {entry.Id} more than once");
            }
        }

        return result.ToFrozenDictionary();
    }

    private static FrozenDictionary<int, int> ReadEquipmentSchools(Dictionary<string, int> entries, string section)
    {
        var result = new Dictionary<int, int>(entries.Count);
        foreach (var (key, school) in entries)
        {
            result[ParseEquipmentKey(key, section)] = school;
        }

        return result.ToFrozenDictionary();
    }

    private static int ParseEquipmentKey(string key, string section)
    {
        return int.TryParse(key, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var id)
            ? id
            : throw new InvalidDataException($"{section} has \"{key}\" which is not numeric");
    }

    /// <summary><c>Equips.json</c> の中の <c>null</c> を形の誤りにする。</summary>
    private static T RequireEquipmentItem<T>(T? item, string itemPath)
        where T : class
    {
        return item ?? throw new InvalidDataException($"{itemPath} is null");
    }

    /// <summary><c>Equips.json</c> の形。項目の意味は <see cref="EquipmentDefinition"/> ほか公開の型と同じ。項目はどれも省略できない。</summary>
    [JsonObject(ItemRequired = Required.Always)]
    private sealed class EquipmentFile
    {
        public Dictionary<string, EquipmentEntry?> Equips { get; set; } = null!;

        /// <summary>属性庫の型 → 庫ID → 庫の行。</summary>
        public Dictionary<string, Dictionary<string, List<EquipmentAttrLibRowEntry?>?>?> AttrLibs { get; set; } = null!;

        public List<EquipmentStrengthSeasonEntry?> StrengthSeasons { get; set; } = null!;

        /// <summary>特化の番号(<c>SubProfessionId</c>)→ 型2 の属性庫の特化。</summary>
        public Dictionary<string, int> SpecSchools { get; set; } = null!;

        /// <summary>職業ID → クラスR1 の人の型2 の属性庫の特化。</summary>
        public Dictionary<string, int> Rank1Schools { get; set; } = null!;
    }

    [JsonObject(ItemRequired = Required.Always)]
    private sealed class EquipmentEntry
    {
        public int Part { get; set; }

        public int Quality { get; set; }

        public int PerfectUpperLimit { get; set; }

        public int MainStat { get; set; }

        public List<EquipmentStageEntry?> Stages { get; set; } = null!;

        public List<int> Recast { get; set; } = null!;

        public List<int> Rare { get; set; } = null!;
    }

    [JsonObject(ItemRequired = Required.Always)]
    private sealed class EquipmentStageEntry
    {
        public int Gs { get; set; }

        public List<int> Basic { get; set; } = null!;

        public List<int> Advanced { get; set; } = null!;
    }

    [JsonObject(ItemRequired = Required.Always)]
    private sealed class EquipmentAttrLibRowEntry
    {
        public int Id { get; set; }

        public List<int> Parts { get; set; } = null!;

        public List<int> Specs { get; set; } = null!;

        public List<EquipmentAttrEffectEntry?> Effects { get; set; } = null!;
    }

    [JsonObject(ItemRequired = Required.Always)]
    private sealed class EquipmentAttrEffectEntry
    {
        public int Kind { get; set; }

        public int Id { get; set; }

        public long Min { get; set; }

        public long Max { get; set; }

        public int Format { get; set; }
    }

    /// <summary>シーズン強度の項目(効果の種類と番号)とそのシーズン。</summary>
    [JsonObject(ItemRequired = Required.Always)]
    private sealed class EquipmentStrengthSeasonEntry
    {
        public int Kind { get; set; }

        public int Id { get; set; }

        public int Season { get; set; }
    }

    /// <summary>
    /// 料理・薬剤の名前テーブル(<c>Data/Localization/CuisineBuffs.json</c> / <c>PotionBuffs.json</c>)を読む。
    /// 形はほかの名前テーブルと同じで、表にある鍵がそのまとまりのバフ。ファイルが無ければエラーログを出して空(そのまとまりが無い)。
    /// 同じバフが両方にあれば止まる。
    /// </summary>
    private static void LoadBuffGroups()
    {
        var cuisineBuffNames = LoadLocalizedText("CuisineBuffs", out var cuisineBuffIds);
        var potionBuffNames = LoadLocalizedText("PotionBuffs", out var potionBuffIds);

        var both = cuisineBuffIds.Intersect(potionBuffIds).ToArray();
        if (both.Length > 0)
        {
            throw new InvalidDataException($"CuisineBuffs and PotionBuffs share buffs: {string.Join(", ", both)}");
        }

        _cuisineBuffNames = cuisineBuffNames;
        _cuisineBuffIds = cuisineBuffIds;
        _potionBuffNames = potionBuffNames;
        _potionBuffIds = potionBuffIds;
    }

    /// <summary>
    /// 4言語をまとめた名前テーブル <c>Data/Localization/{fileName}.json</c> を読む。
    /// 形は <c>{ "鍵": { "zh-CN": "名前", "en-US": "名前", "ja-JP": "名前", "ko-KR": "名前" } }</c>。
    ///
    /// <para>
    /// ファイルが無ければエラーログを出して <c>null</c>(呼び出し側は全言語を空で続ける)。
    /// 言語名は <see cref="CheckLocalizedNames"/> で検める。
    /// </para>
    /// </summary>
    private static Dictionary<string, Dictionary<string, string>>? ReadLocalizedFile(string fileName)
    {
        var path = Path.Combine(CombatRuntimePaths.LocalizationDirectory, $"{fileName}.json");
        if (!File.Exists(path))
        {
            Log.Error("Missing localization {FileName}", fileName);
            return null;
        }

        var entries = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>?>>(File.ReadAllText(path))
            ?? throw new InvalidDataException($"{path} is empty.");
        var result = new Dictionary<string, Dictionary<string, string>>(entries.Count);
        var missingNames = 0;
        foreach (var entry in entries)
        {
            result[entry.Key] = CheckLocalizedNames(fileName, entry.Key, entry.Value, ref missingNames);
        }

        LogMissingLocalizedNames(fileName, missingNames);
        Log.Information("Loaded localization {FileName}", fileName);
        return result;
    }

    /// <summary>
    /// 1つの鍵の「言語 → 名前」を検める。<b>知らない言語名はエラーログを出して外す。</b>
    /// 欠けた言語は空欄と同じ扱い(zh-CN へ落ちる)にし、数を <paramref name="missingNames"/> に足す。
    /// </summary>
    private static Dictionary<string, string> CheckLocalizedNames(
        string fileName,
        string key,
        Dictionary<string, string>? names,
        ref int missingNames)
    {
        var checkedNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in names ?? [])
        {
            if (!SupportedCultures.Contains(name.Key, StringComparer.OrdinalIgnoreCase))
            {
                Log.Error("{FileName}: key {Key} has unknown culture \"{Culture}\". Available cultures are {Cultures}",
                    fileName, key, name.Key, string.Join(" / ", SupportedCultures));
                continue;
            }

            checkedNames[name.Key] = name.Value ?? string.Empty;
        }

        missingNames += SupportedCultures.Count(cultureName => !checkedNames.ContainsKey(cultureName));
        return checkedNames;
    }

    /// <summary>4言語のどれかが欠けていた数を、ファイルごとに1回だけエラーログに出す。</summary>
    private static void LogMissingLocalizedNames(string fileName, int missingNames)
    {
        if (missingNames > 0)
        {
            Log.Error("{FileName}: {Count} names are missing one or more cultures (treated as blank)", fileName, missingNames);
        }
    }

    /// <summary>
    /// 鍵が番号の名前テーブルを、言語ごとの「番号 → 名前」にして返す。
    /// <paramref name="keys"/> は名前の有無を問わない全部の鍵。番号でない鍵はエラーログを出して飛ばす。
    /// </summary>
    private static FrozenDictionary<string, FrozenDictionary<int, string>> LoadLocalizedText(
        string fileName,
        out FrozenSet<int> keys)
    {
        var namesByCulture = SupportedCultures.ToDictionary(
            cultureName => cultureName,
            _ => new Dictionary<int, string>(),
            StringComparer.OrdinalIgnoreCase);
        var allKeys = new HashSet<int>();

        foreach (var entry in ReadLocalizedFile(fileName) ?? [])
        {
            if (!int.TryParse(entry.Key, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var id))
            {
                Log.Error("{FileName}: skipping \"{Key}\" because the key is not numeric", fileName, entry.Key);
                continue;
            }

            allKeys.Add(id);
            foreach (var name in entry.Value)
            {
                if (!string.IsNullOrWhiteSpace(name.Value))
                {
                    namesByCulture[name.Key][id] = name.Value.Trim();
                }
            }
        }

        keys = allKeys.ToFrozenSet();
        return namesByCulture.ToFrozenDictionary(
            pair => pair.Key,
            pair => pair.Value.ToFrozenDictionary(),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// <c>DungeonTypeNames.json</c> を、言語ごとの「<see cref="MakeDungeonTypeKey"/> → 名前」にして返す。
    /// 鍵は <c>番号</c> か <c>番号:段階</c>。形の合わない鍵はエラーログを出して飛ばす。
    /// </summary>
    private static FrozenDictionary<string, FrozenDictionary<long, string>> LoadDungeonTypeNames()
    {
        var namesByCulture = SupportedCultures.ToDictionary(
            cultureName => cultureName,
            _ => new Dictionary<long, string>(),
            StringComparer.OrdinalIgnoreCase);

        foreach (var entry in ReadLocalizedFile("DungeonTypeNames") ?? [])
        {
            var separator = entry.Key.IndexOf(':');
            var idText = separator < 0 ? entry.Key.AsSpan() : entry.Key.AsSpan(0, separator);
            var difficulty = 0;
            if (!int.TryParse(idText, out var dungeonId)
                || dungeonId <= 0
                || (separator >= 0 && (!int.TryParse(entry.Key.AsSpan(separator + 1), out difficulty) || difficulty <= 0)))
            {
                Log.Error("DungeonTypeNames: skipping \"{Key}\" because the key is not in id or id:difficulty form", entry.Key);
                continue;
            }

            foreach (var name in entry.Value)
            {
                if (!string.IsNullOrWhiteSpace(name.Value))
                {
                    namesByCulture[name.Key][MakeDungeonTypeKey(dungeonId, difficulty)] = name.Value.Trim();
                }
            }
        }

        return namesByCulture.ToFrozenDictionary(
            pair => pair.Key,
            pair => pair.Value.ToFrozenDictionary(),
            StringComparer.OrdinalIgnoreCase);
    }

    private static string ResolveText<TKey>(
        IReadOnlyDictionary<string, FrozenDictionary<TKey, string>> localizedNames,
        string cultureName,
        TKey id)
        where TKey : notnull
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
