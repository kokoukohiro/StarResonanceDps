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
    /// <summary>技ID → ボス大技の予告(<c>DbmTable</c>)の名前。<c>Data/Localization/dbms.*.json</c>。</summary>
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
    /// <summary>行代表キー → ゲーム内メーターの行名。<c>Data/Localization/recounts.*.json</c>。</summary>
    private static FrozenDictionary<string, FrozenDictionary<long, string>> _recountNames =
        new Dictionary<string, FrozenDictionary<long, string>>(StringComparer.OrdinalIgnoreCase)
            .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>発生源キー → 行代表キー。<c>recounts.*.json</c> の行構成に手修正を重ねたもの。</summary>
    private static FrozenDictionary<long, long> _recountRows = FrozenDictionary<long, long>.Empty;

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
            _skillNames = LoadLocalizedText("skills");
            _buffNames = LoadLocalizedText("buffs");
            _monsterNames = LoadLocalizedText("monsters");
            _dbmNames = LoadLocalizedText("dbms");
            _skillIdByEffectId = BuildSkillIdByEffectId(_skills);
            _monsterIdsBySkillId = BuildMonsterIdsBySkillId();
            _warningSkillLevelIds = LoadWarningSkillLevels();
            _sceneNames = LoadLocalizedText("scenes");
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
    /// メーターの行に出す名前。<b>ゲーム内メーターの見出し表だけが決める。</b>
    ///
    /// <para>
    /// <c>skills</c> / <c>buffs</c> の4言語テーブルは引かない。あちらは装備中スキル枠や
    /// バフ/デバフウィジェットのためのもので、メーターの行とは対象も粒度も違う。
    /// </para>
    ///
    /// <para>
    /// 引数は<b>畳んだあとの行代表キー</b>。注記もそのキーで付けるので、
    /// 「どの行か」と「注記のID」が必ず一致する。
    /// </para>
    /// </summary>
    public static string GetSourceDisplayName(long rowKey, bool isBuffSource)
    {
        return AppendSourceInternalId(
            ResolveText(_recountNames, Volatile.Read(ref _cultureName), rowKey),
            isBuffSource ? InternalIdDisplayMode.BuffOnly : InternalIdDisplayMode.SkillOnly,
            rowKey);
    }

    /// <summary>
    /// 発生源キー → 行代表キー。<c>recounts.*.json</c> の行構成に手修正を重ねたもの。
    ///
    /// <para>
    /// ゲーム内メーターは <c>RecountTable</c> の1行に複数の <c>DamageId</c> をまとめる。
    /// 生成物はその所属をそのまま写したもので、<b>手書きの判断は入っていない</b>。
    /// 判断が要るぶんは <c>Data/Overrides/RecountOverrides.json</c> にある。
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
    ///   <item>生成物 <c>recounts.*.json</c> の行から、行の集まりを作る</item>
    ///   <item>手修正の <c>Row</c> を当てる。<b>外すほうを先に</b>当てないと、外した鍵へ寄せられない</item>
    ///   <item>行代表を取り直す。外した鍵が代表だった行は代表が変わる</item>
    ///   <item>生成物の名前を行代表へ配り、手修正の <c>Name</c> を重ねる</item>
    /// </list>
    /// </summary>
    private static void LoadRecounts()
    {
        // 生成物は生の見出し表と同じ形。行ごとに RecountName と、その行に属する発生源キーの一覧。
        // 項目名は SourceId。中身は TypeEnum:枝番 で、生の DamageId とは別の値。
        var byCulture = new Dictionary<string, Dictionary<string, RecountRow>>(StringComparer.OrdinalIgnoreCase);
        foreach (var cultureName in SupportedCultures)
        {
            var path = Path.Combine(
                Utils.DATA_DIR_NAME, "Localization", $"recounts.{cultureName}.json");
            if (!File.Exists(path))
            {
                Log.Warning("Missing recounts localization for {CultureName}", cultureName);
                byCulture[cultureName] = [];
                continue;
            }

            byCulture[cultureName] =
                JsonConvert.DeserializeObject<Dictionary<string, RecountRow>>(File.ReadAllText(path)) ?? [];
        }

        // 行の構成はどの言語でも同じ。名前の受け皿でもある zh-CN を土台にする。
        var layout = byCulture.TryGetValue("zh-CN", out var chinese) && chinese.Count > 0
            ? chinese
            : byCulture.Values.FirstOrDefault(rows => rows.Count > 0) ?? [];

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
                    Log.Error("recounts: 行 {Row} の \"{Key}\" が ownerId:枝番 の形でない", row.Key, text);
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

        // 外すほうを先に。寄せ先が「外したばかりの鍵」であることがある(パッシブ2件がこの形)。
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
                Log.Error("RecountOverrides: {Key} の Row \"{Row}\" が ownerId:枝番 の形でない",
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
        _recountNames = BuildRecountNames(membersOfRep, originRow, byCulture, repOf, overrides);
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
        public string? RecountName { get; set; }

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
        // 付け直さないと、抜いた鍵で行を作り直したときに残りのメンバーごと消える
        // (幻影共鳴の行から先頭の 1850:1 を外して、残り4件が名無しになった)。
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

    private static Dictionary<long, RecountOverrideEntry> LoadRecountOverrides()
    {
        var result = new Dictionary<long, RecountOverrideEntry>();
        var path = Path.Combine(Utils.DATA_DIR_NAME, "Overrides", "RecountOverrides.json");
        if (!File.Exists(path))
        {
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
                Log.Error("RecountOverrides: 鍵が ownerId:枝番 の形でないので飛ばす \"{Key}\"", pair.Key);
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
                            "RecountOverrides: {Key} に未知の言語 \"{Culture}\"。言語は {Cultures}",
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
        Dictionary<string, Dictionary<string, RecountRow>> byCulture,
        Dictionary<long, long> repOf,
        Dictionary<long, RecountOverrideEntry> overrides)
    {
        var result = new Dictionary<string, FrozenDictionary<long, string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var cultureName in SupportedCultures)
        {
            var rows = byCulture.TryGetValue(cultureName, out var forCulture) ? forCulture : [];
            var names = new Dictionary<long, string>();

            foreach (var group in membersOfRep)
            {
                foreach (var member in group.Value)
                {
                    if (originRow.TryGetValue(member, out var rowId)
                        && rows.TryGetValue(rowId, out var row)
                        && !string.IsNullOrWhiteSpace(row.RecountName))
                    {
                        names[group.Key] = row.RecountName.Trim();
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
    /// サーバは「詠唱中」を送らない。ゲームは技の開始とこの設定から詠唱バーを出す(2026-09-14 実測)。
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
        return ResolveText(_buffNames, Volatile.Read(ref _cultureName), buffId);
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

    /// <summary>形は技レベルIDの配列。同梱の生成物なので、無ければ読み込みごと失敗させる。</summary>
    private static FrozenSet<int> LoadWarningSkillLevels()
    {
        var path = Path.Combine(Utils.DATA_DIR_NAME, "Generated", "SkillWarnings.json");
        var skillLevelIds = JsonConvert.DeserializeObject<int[]>(File.ReadAllText(path))
            ?? throw new InvalidDataException($"{path} is empty.");
        return skillLevelIds.ToFrozenSet();
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
    private static FrozenDictionary<string, FrozenDictionary<int, string>> LoadLocalizedText(
        string dataName)
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
