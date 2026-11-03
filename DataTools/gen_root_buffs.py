"""
根のバフの名前・種類・並び・絵と、タレントの木の形・タレントの名前を生成する。

  Data/Localization/RootBuffNames.json
  Data/Localization/TalentNames.json
  Data/Generated/RootBuffs.json

根のバフは、タレント・心相晶のノード・心相晶の上級・因子・モジュール効果を付けている間、ずっと乗るバフ。
それぞれの効果の `[3, バフID, 1]` がそのバフ。アプリはプレイヤーに乗っている根のバフから、
アビリティ情報(タレントの木)・潜在心相晶情報(ノード・上級・因子)・モジュール情報(モジュール効果)を出す。
付与元をたどってモジュールの根に着いたバフの名前も `RootBuffNames.json` から引く。

タレントの木(`RootBuffs.json` の `TalentTrees`)は、アビリティ情報がゲームと同じ形の木を描くのに使う。
サーバーごとに、その出所(`Star` / `StarASIA`)の表だけで作る(出所でタレントが違う節や、2つのタレントの位置が入れ替わった節がある)。
木は段階の行(`TalentStageTable` の `RootId` が 0 でない行)ごとに1本で、節はその行と同じ
(`WeaponType`, `TalentStage`, `BdType`)の `TalentTreeTable` の行。位置は `TalentPosition`、線は `NextTalent`。
クラスR1(`TalentStage` 0)の木は特化の番号 0、クラスR2 の木は段階が職業の `ShowTalentStage` の何番目かで
特化の番号(職業ID × 10000 ＋ 並び、アプリの特化の番号)を決める。クラスR2 の根は特化型のバフなので入れない。
両方の出所で中身の違う節は一覧を出す(止めない)。

木の節は、タレントの番号(`TalentTreeTable.TalentId`)・アビリティの節か(`TalentTable.TalentType` が 4。1 は能力値の小さい節、
5 は特化型の根)・絵(`TalentIcon`)・役割のタグ・技の置き換え(`TalentEffect` の `[6, 元, 先]` の元と先の組の一覧)を持つ。
役割のタグは、その木の職業(`TalentTreeTable.WeaponType`)の `ProfessionSystemTable.Talent`(`TalentTagTable` の番号。1 攻撃 / 2 支援 / 3 防御)。
アプリは取ったアビリティの節を、節の絵・役割(絵の背景の色)・タレントの名前(`TalentNames.json`、鍵はタレントの番号)で描く。
タレントの名前の対象は、木の節に出てくるタレント全部。

| 種類 | 表 | 名前 | 絵 | 並び |
|---|---|---|---|---|
| タレント | `TalentTable.TalentEffect` | なし(木の節のタレントの名前を使う) | なし(木の節の絵を使う) | `Id` |
| 心相晶のノード | `SeasonTalentEffectOrdinaryTable.Effect` | `Name` | `IconNormal` | `Id` |
| 心相晶の上級 | `SeasonTalentEffectAdvancedTable.Effect` | `Name` | `Icon`(型の上級の番号ごと) | `Id` |
| 因子 | `SeasonTalentFactorItemTable.FactorItemEffect` | `ItemTable.Name` から等級を外した名前 | `ItemTable.Icon` | バフID |
| モジュール効果 | `ModEffectTable.EffectConfig` | `EffectName` | `EffectConfigIcon` | `EffectID` |

タレントは、段階の根のタレント(`TalentStageTable.RootId` → `TalentTreeTable.TalentId`)のバフを入れない。
特化型のバフで、特化はプレイヤーリストが出す。

心相晶のノードは、型の根のノード(`SeasonTalentTemplateTable.NoteRootId`。型はプレイヤーリストが出す)と、
効く遊びの種類(`EffectiveGameplayType`)が決まっている型の木(`SeasonTalentTreeTable.TemplateId`)のノードを入れない。

心相晶の上級は、1つの型の上級(`EffectId`)にしか出てこないバフ(型の専用効果)を入れない。入れるのは全部の型が使う共通の効果。
上級の絵は、同じバフでも型ごとに違う(型の `AdvancedEffectId` = 上級の行の `EffectId`)。
`RootBuffs.json` は上級の番号ごとの絵と、型の根のバフ → 上級の番号(0 の型は上級を持たないので入れない)を持つ。

因子は、等級を外した名前が2つ以上になる根(シーズンが変わって効果を失った因子の共通の説明のバフ)を入れない。

**名前の土台はバフIDの単位で決める**(cn / en は `Star`、jp / kr は `StarASIA`)。土台にそのバフIDの名前があれば土台だけを使い、
無いときだけもう一方を使う。心相晶のノードで同じ出所の中の1つのバフIDに複数の行が当たったら、型の `BelongSeasonId` が一番大きい行を採る。
ほかの種類で1つのバフIDに名前の違う行が当たったら、並びの一番小さい行を採り、一覧を出す。
タレントの名前も同じ土台で、タレントの番号の単位で決める。

**種類・並び・絵は `Star` を土台にし、無いバフだけ `StarASIA` で補う。** 出所で絵が違うバフは一覧を出す。

次のときは一覧を出して止める。
- 1つのバフIDが2つの種類に当たる
- 段階の根が木の表に無い、上級の番号が1つしか無い(型の専用効果を見分けられない)
- 心相晶のノードの行がどの型の木にも無い、主の型とローグライクの型の両方の木に入る
- 一番新しいシーズンの中で、同じノードのバフIDの名前が食い違う
- 型の根のバフに上級の番号が2つ以上当たる、1つのモジュールの根のバフに効果が2つ以上当たる
- タレントの木が空、つなぎが同じ木の節に無い、クラスR2 の根が木の節に無い、クラスR2 の段階が職業の `ShowTalentStage` に無い、
  1つの職業・特化に木が2本ある
- 節のバフが `Talents` に無い、1つのバフが同じ木の2つ以上の節にある、クラスR2 の木がある職業にクラスR1 の木が無い(どれも出所ごと)
- 節のタレントが表に無い、`TalentType` が 1 / 4 / 5 のどれでもない、バフのある節がアビリティの節でない、
  役割のタグが 1〜3 のどれでもない(どれも出所ごと)
- アビリティの節のタレントに、どの言語の名前も無い
"""
import os
import re

from _common import GENERATED, LANGS, dump, name_of, table_of_source, tables_by_source, write_localized

# 効果の要素 `[種類, 値, …]` のうち、バフを付ける種類。
EFFECT_ADD_BUFF = 3

# 効果の要素のうち、技を置き換える種類(`[6, 元の技, 置き換え後の技]`)。
EFFECT_REPLACE_SKILL = 6

# タレントの種類(`TalentTable.TalentType`)。1 能力値の小さい節 / 4 アビリティの節 / 5 特化型の根。
ABILITY_TALENT_TYPE = 4
TALENT_TYPES = (1, ABILITY_TALENT_TYPE, 5)

# 種類・並び・絵を読む出所と言語フォルダ。先にあるほうが土台。
STRUCTURE_SOURCES = (("Star", "cn"), ("StarASIA", "jp"))

KINDS = ("Talents", "SeasonTalentNodes", "SeasonTalentAdvanced", "Factors", "ModuleEffects")

# タレントの役割のタグ(`TalentTagTable` の番号)。1 攻撃 / 2 支援 / 3 防御。
TALENT_TAGS = (1, 2, 3)

# クラスR1 の段階(`TalentStage`)。ほかはクラスR2。
RANK1_STAGE = 0

# 特化の番号は 職業ID × これ ＋ 特化の並び(1始まり)。gen_class_specs.py と同じ決め方。
PROFESSION_DIGIT = 10000

# 因子のアイテムの名前の末尾の等級(jp / kr「・G1」、cn「·G1」、en「 - G1」)。
FACTOR_GRADE = re.compile(r"\s*(?:・|·|-)\s*G\d+\s*$")


def buff_ids_of(effects):
    """効果の配列のうち、バフを付けるもののバフID。"""
    return [effect[1] for effect in effects or []
            if isinstance(effect, list) and len(effect) >= 2 and effect[0] == EFFECT_ADD_BUFF
            and isinstance(effect[1], int) and effect[1] > 0]


def stage_root_buffs(source, lang_dir, talents, tree, problems, label):
    """段階の根のタレント(`TalentStageTable.RootId` → `TalentTreeTable.TalentId`)のバフ。特化型のバフ。"""
    buffs = set()
    for stage in (table_of_source(source, lang_dir, "TalentStageTable") or {}).values():
        root_id = stage.get("RootId") or 0
        if root_id <= 0:
            continue
        node = tree.get(str(root_id))
        if node is None:
            problems.append("%s 段階 %s の根 %s が木の表に無い" % (label, stage.get("Id"), root_id))
            continue
        buffs.update(buff_ids_of(talents.get(str(node.get("TalentId")), {}).get("TalentEffect")))
    return buffs


def skill_replacements_of(effects):
    """効果の配列のうち、技を置き換えるものの `[元の技, 置き換え後の技]`。"""
    return [[effect[1], effect[2]] for effect in effects or []
            if isinstance(effect, list) and len(effect) >= 3 and effect[0] == EFFECT_REPLACE_SKILL
            and isinstance(effect[1], int) and isinstance(effect[2], int) and effect[1] > 0 and effect[2] > 0]


def talent_trees_of(source, lang_dir, problems):
    """
    1つの出所の表から、タレントの木を「(職業, 特化の番号) → 木」で作る。
    節は「節の番号 → {X, Y, Next, Buffs, Talent, Ability, Icon, TalentTag, Replace}」。位置は木の左上を 0 にした表の単位。
    """
    label = "%s %s" % (lang_dir, source)
    talents = table_of_source(source, lang_dir, "TalentTable") or {}
    tree = table_of_source(source, lang_dir, "TalentTreeTable") or {}
    professions = table_of_source(source, lang_dir, "ProfessionSystemTable") or {}
    spec_of_stage = {}
    for profession_id, profession in professions.items():
        for index, stage_id in enumerate(profession.get("ShowTalentStage") or []):
            spec_of_stage[stage_id] = int(profession_id) * PROFESSION_DIGIT + index + 1
    members = {}
    for node in tree.values():
        members.setdefault((node.get("WeaponType"), node.get("TalentStage"), node.get("BdType")), []).append(node)

    trees = {}
    for stage in (table_of_source(source, lang_dir, "TalentStageTable") or {}).values():
        root_id = stage.get("RootId") or 0
        if root_id <= 0:
            continue
        profession_id = stage.get("WeaponType")
        stage_label = "%s 段階 %s" % (label, stage.get("Id"))
        if stage.get("TalentStage") == RANK1_STAGE:
            spec, root = 0, 0
        else:
            spec = spec_of_stage.get(stage.get("Id"))
            if spec is None:
                problems.append("%s が職業 %s の ShowTalentStage に無い" % (stage_label, profession_id))
                continue
            root = root_id

        rows = members.get((profession_id, stage.get("TalentStage"), stage.get("BdType")), [])
        if not rows:
            problems.append("%s の木に節が無い" % stage_label)
            continue
        ids = {row["Id"] for row in rows}
        if root and root not in ids:
            problems.append("%s の根 %s が木の節に無い" % (stage_label, root))
            continue
        min_x = min(row["TalentPosition"][0] for row in rows)
        min_y = min(row["TalentPosition"][1] for row in rows)
        tag = professions.get(str(profession_id), {}).get("Talent")
        if tag not in TALENT_TAGS:
            problems.append("%s の役割のタグ %s が 1〜3 のどれでもない" % (stage_label, tag))
        nodes = {}
        for row in rows:
            outside = [next_id for next_id in row.get("NextTalent") or [] if next_id not in ids]
            if outside:
                problems.append("%s の節 %s のつなぎ %s が同じ木に無い" % (stage_label, row["Id"], outside))
            talent = talents.get(str(row.get("TalentId")))
            if talent is None:
                problems.append("%s の節 %s のタレント %s が表に無い" % (stage_label, row["Id"], row.get("TalentId")))
                continue
            talent_type = talent.get("TalentType")
            if talent_type not in TALENT_TYPES:
                problems.append("%s の節 %s のタレント %s の種類 %s が 1 / 4 / 5 のどれでもない"
                                % (stage_label, row["Id"], talent["Id"], talent_type))
            buffs = [] if row["Id"] == root else buff_ids_of(talent.get("TalentEffect"))
            if buffs and talent_type != ABILITY_TALENT_TYPE:
                problems.append("%s の節 %s はバフを付けるのにアビリティの節でない(種類 %s)" % (stage_label, row["Id"], talent_type))
            nodes[row["Id"]] = {
                "X": row["TalentPosition"][0] - min_x,
                "Y": row["TalentPosition"][1] - min_y,
                "Next": list(row.get("NextTalent") or []),
                "Buffs": buffs,
                "Talent": talent["Id"],
                "Ability": talent_type == ABILITY_TALENT_TYPE,
                "Icon": (talent.get("TalentIcon") or "").strip(),
                "TalentTag": tag,
                "Replace": skill_replacements_of(talent.get("TalentEffect")),
            }

        key = (profession_id, spec)
        if key in trees:
            problems.append("%s 職業 %s の特化 %s に木が2本ある" % (label, profession_id, spec))
            continue
        trees[key] = {"Profession": profession_id, "Spec": spec, "Root": root, "Nodes": nodes}
    return trees


def talent_tree_differences(trees_by_source):
    """2つの出所の木を比べ、片方にしか無い木・節と、中身(位置・つなぎ・バフ・根)の違う節を一覧にする。"""
    (left_source, left), (right_source, right) = trees_by_source.items()
    differences = []
    for key in sorted(set(left) | set(right)):
        if key not in left or key not in right:
            differences.append("木 %s/%s は %s にだけある" % (key[0], key[1], left_source if key in left else right_source))
            continue
        left_nodes, right_nodes = left[key]["Nodes"], right[key]["Nodes"]
        for node_id in sorted(set(left_nodes) | set(right_nodes)):
            if node_id not in left_nodes or node_id not in right_nodes:
                differences.append("木 %s/%s の節 %s は %s にだけある"
                                   % (key[0], key[1], node_id, left_source if node_id in left_nodes else right_source))
            elif left_nodes[node_id] != right_nodes[node_id]:
                differences.append("木 %s/%s の節 %s: %s %s / %s %s"
                                   % (key[0], key[1], node_id, left_source, left_nodes[node_id], right_source, right_nodes[node_id]))
        if left[key]["Root"] != right[key]["Root"]:
            differences.append("木 %s/%s の根: %s %s / %s %s"
                               % (key[0], key[1], left_source, left[key]["Root"], right_source, right[key]["Root"]))
    return differences


def season_talent_scope(templates, tree, problems, label):
    """
    心相晶の木の範囲。返すのは
    (入れないノードのグループ, ノードのグループ → シーズン, 型の根のノードのグループ → 型)。
    """
    rogue = {str(tid) for tid, t in templates.items() if t.get("EffectiveGameplayType")}
    root_groups = {t.get("NoteRootId"): t for t in templates.values()}
    groups_of = {}
    for row in tree.values():
        groups_of.setdefault(row.get("GroupId"), set()).add(str(row.get("TemplateId")))

    excluded = set(root_groups)
    seasons = {}
    for group, template_ids in groups_of.items():
        main_ids = template_ids - rogue
        if main_ids and template_ids & rogue:
            problems.append("%s ノードのグループ %s が主の型とローグライクの型の両方の木に入る(%s)" % (label, group, sorted(template_ids)))
        if not main_ids:
            excluded.add(group)
            continue
        seasons[group] = max(templates[tid].get("BelongSeasonId") or 0 for tid in main_ids if tid in templates)
    return excluded, seasons, root_groups, groups_of


def rows_of_source(source, lang_dir):
    """
    1つの出所の表から「種類 → バフID → [(並び, 名前, 絵, 付記)]」を作る。付記はノードのシーズン、上級の番号。
    タレントは並びだけを持つ(名前と絵は木の節のタレントが持つ)。外したバフは「種類 → [バフID]」で返す。
    """
    label = "%s %s" % (lang_dir, source)
    problems = []
    found = {kind: {} for kind in KINDS}

    def add(kind, buff_id, order, name, icon, extra=None):
        found[kind].setdefault(buff_id, []).append((order, name, (icon or "").strip(), extra))

    talents = table_of_source(source, lang_dir, "TalentTable") or {}
    talent_tree = table_of_source(source, lang_dir, "TalentTreeTable") or {}
    spec_buffs = stage_root_buffs(source, lang_dir, talents, talent_tree, problems, label)
    for row in talents.values():
        for buff_id in buff_ids_of(row.get("TalentEffect")):
            if buff_id in spec_buffs:
                continue
            add("Talents", buff_id, row["Id"], "", "")

    templates = table_of_source(source, lang_dir, "SeasonTalentTemplateTable") or {}
    tree = table_of_source(source, lang_dir, "SeasonTalentTreeTable") or {}
    excluded, seasons, _, groups_of = season_talent_scope(templates, tree, problems, label)
    for row in (table_of_source(source, lang_dir, "SeasonTalentEffectOrdinaryTable") or {}).values():
        group = row.get("GroupId")
        if group in excluded:
            continue
        if group not in groups_of:
            problems.append("%s ノードの行 %s のグループ %s がどの型の木にも無い" % (label, row.get("Id"), group))
            continue
        for buff_id in buff_ids_of(row.get("Effect")):
            add("SeasonTalentNodes", buff_id, row["Id"], name_of(row), row.get("IconNormal"), seasons.get(group, 0))
    for row in (table_of_source(source, lang_dir, "SeasonTalentEffectAdvancedTable") or {}).values():
        for buff_id in buff_ids_of(row.get("Effect")):
            add("SeasonTalentAdvanced", buff_id, row["Id"], name_of(row), row.get("Icon"), row.get("EffectId"))

    items = table_of_source(source, lang_dir, "ItemTable") or {}
    for row in (table_of_source(source, lang_dir, "SeasonTalentFactorItemTable") or {}).values():
        item = items.get(str(row["Id"]), {})
        name = FACTOR_GRADE.sub("", name_of(item))
        for buff_id in buff_ids_of(row.get("FactorItemEffect")):
            add("Factors", buff_id, buff_id, name, item.get("Icon"), row["Id"])

    for row in (table_of_source(source, lang_dir, "ModEffectTable") or {}).values():
        for buff_id in buff_ids_of(row.get("EffectConfig")):
            add("ModuleEffects", buff_id, (row["EffectID"], row.get("Level") or 0, row["Id"]),
                name_of(row, "EffectName"), row.get("EffectConfigIcon"), row["EffectID"])

    # 1つの型の上級にしか出てこないバフは、その型の専用効果。
    advanced = found["SeasonTalentAdvanced"]
    advanced_ids = {extra for rows in advanced.values() for _, _, _, extra in rows}
    if advanced and len(advanced_ids) < 2:
        problems.append("%s 上級の番号が %s だけで、型の専用効果を見分けられない" % (label, sorted(advanced_ids)))
    dedicated = sorted(b for b, rows in advanced.items() if len({extra for _, _, _, extra in rows}) == 1)
    for buff_id in dedicated:
        del advanced[buff_id]

    # 等級を外した名前が2つ以上になる因子の根は、1つの因子ではない(共通の説明のバフ)。
    shared = sorted(b for b, rows in found["Factors"].items() if len({name for _, name, _, _ in rows if name}) > 1)
    for buff_id in shared:
        del found["Factors"][buff_id]

    for buff_id, rows in found["ModuleEffects"].items():
        effect_ids = {extra for _, _, _, extra in rows}
        if len(effect_ids) > 1:
            problems.append("%s モジュールの根のバフ %d に効果が %s" % (label, buff_id, sorted(effect_ids)))

    excluded = {"Talents": sorted(spec_buffs), "SeasonTalentAdvanced": dedicated, "Factors": shared}
    return found, problems, excluded, templates


def pick_name(kind, buff_id, rows, conflicts, resolved, lang):
    """1つの出所の行から名前を1つ選ぶ。"""
    named = [row for row in rows if row[1]]
    if not named:
        return ""
    if kind == "SeasonTalentNodes":
        newest = max(season for _, _, _, season in named)
        names = sorted({name for _, name, _, season in named if season == newest})
        if len(names) > 1:
            conflicts.append("%s %d %s" % (lang, buff_id, " / ".join(names)))
            return ""
        if len({name for _, name, _, _ in named}) > 1:
            resolved.append("%s ノード %d → シーズン %d の「%s」" % (lang, buff_id, newest, names[0]))
        return names[0]

    named.sort(key=lambda row: row[0])
    if len({name for _, name, _, _ in named}) > 1:
        resolved.append("%s %s %d → 並びの一番小さい行の「%s」(ほか %s)"
                        % (lang, kind, buff_id, named[0][1],
                           " / ".join(sorted({name for _, name, _, _ in named[1:] if name != named[0][1]}))))
    return named[0][1]


def main():
    problems = []
    conflicts = []
    resolved = []

    # 名前(言語ごと、出所の土台はバフIDの単位)
    names = {lang: {} for lang in LANGS}
    for lang, lang_dir in LANGS.items():
        counts = []
        for source, _ in tables_by_source(lang_dir, "TalentTable"):
            found, source_problems, _, _ = rows_of_source(source, lang_dir)
            problems.extend(source_problems)
            for kind in KINDS:
                for buff_id, rows in found[kind].items():
                    key = str(buff_id)
                    if names[lang].get(key):
                        continue
                    name = pick_name(kind, buff_id, rows, conflicts, resolved, lang)
                    if name:
                        names[lang][key] = name
            counts.append("%s %d" % (source, sum(len(found[kind]) for kind in KINDS)))
        print("%-6s 根のバフ %4d(%s)" % (lang, len(names[lang]), " / ".join(counts)))

    # 種類・並び・絵(StarASIA を土台に Star で補う)
    structure = {kind: {} for kind in KINDS}
    advanced_icons = {}
    type_advanced = {}
    icon_differences = []
    excluded_by_kind = {}
    talent_trees = {}
    for source, lang_dir in STRUCTURE_SOURCES:
        found, source_problems, excluded, templates = rows_of_source(source, lang_dir)
        problems.extend(source_problems)
        talent_trees[source] = talent_trees_of(source, lang_dir, problems)
        for kind, buff_ids in excluded.items():
            excluded_by_kind.setdefault(kind, set()).update(buff_ids)
        for kind in KINDS:
            for buff_id, rows in found[kind].items():
                rows = sorted(rows, key=lambda row: row[0])
                order, _, icon, _ = rows[0]
                if buff_id in structure[kind]:
                    if kind != "SeasonTalentAdvanced" and icon and structure[kind][buff_id][1] != icon:
                        icon_differences.append("%s %d %s「%s」/ 土台「%s」" % (kind, buff_id, source, icon, structure[kind][buff_id][1]))
                    continue
                structure[kind][buff_id] = (order, icon)
                if kind == "SeasonTalentAdvanced":
                    for _, _, row_icon, effect_id in rows:
                        advanced_icons.setdefault(buff_id, {}).setdefault(str(effect_id), row_icon)

        ordinary = table_of_source(source, lang_dir, "SeasonTalentEffectOrdinaryTable") or {}
        root_buffs = {}
        for row in ordinary.values():
            root_buffs.setdefault(row.get("GroupId"), []).extend(buff_ids_of(row.get("Effect")))
        for template in templates.values():
            advanced_id = template.get("AdvancedEffectId") or 0
            if advanced_id <= 0 or template.get("EffectiveGameplayType"):
                continue
            for buff_id in root_buffs.get(template.get("NoteRootId"), []):
                type_advanced.setdefault(buff_id, set()).add(advanced_id)

    for buff_id, advanced_ids in sorted(type_advanced.items()):
        if len(advanced_ids) > 1:
            problems.append("型の根のバフ %d に上級の番号が %s" % (buff_id, sorted(advanced_ids)))

    kinds_of = {}
    for kind in KINDS:
        for buff_id in structure[kind]:
            kinds_of.setdefault(buff_id, []).append(kind)
    for buff_id, kinds in sorted(kinds_of.items()):
        if len(kinds) > 1:
            problems.append("バフ %d が2つの種類に当たる(%s)" % (buff_id, " / ".join(kinds)))

    for source, trees in talent_trees.items():
        for (profession_id, spec), tree in sorted(trees.items()):
            nodes_of_buff = {}
            for node_id, node in sorted(tree["Nodes"].items()):
                for buff_id in node["Buffs"]:
                    nodes_of_buff.setdefault(buff_id, []).append(node_id)
                    if buff_id not in structure["Talents"]:
                        problems.append("%s タレントの木 %s/%s の節 %s のバフ %d が Talents に無い"
                                        % (source, profession_id, spec, node_id, buff_id))
            for buff_id, node_ids in sorted(nodes_of_buff.items()):
                if len(node_ids) > 1:
                    problems.append("%s タレントの木 %s/%s のバフ %d が2つ以上の節にある(%s)"
                                    % (source, profession_id, spec, buff_id, node_ids))
        for profession_id in sorted({profession_id for profession_id, spec in trees if spec}):
            if (profession_id, 0) not in trees:
                problems.append("%s 職業 %s にクラスR1 の木が無い" % (source, profession_id))

    # タレントの名前(言語ごと、出所の土台はタレントの番号の単位)。対象は木の節に出てくるタレント。
    tree_nodes = [node for trees in talent_trees.values() for tree in trees.values() for node in tree["Nodes"].values()]
    talent_ids = {node["Talent"] for node in tree_nodes}
    talent_names = {lang: {} for lang in LANGS}
    for lang, lang_dir in LANGS.items():
        for _, talents in tables_by_source(lang_dir, "TalentTable"):
            for talent_id in talent_ids:
                key = str(talent_id)
                if talent_names[lang].get(key):
                    continue
                name = name_of(talents.get(key), "TalentName")
                if name:
                    talent_names[lang][key] = name
    for talent_id in sorted({node["Talent"] for node in tree_nodes if node["Ability"]}):
        if not any(talent_names[lang].get(str(talent_id)) for lang in LANGS):
            problems.append("アビリティの節のタレント %d にどの言語の名前も無い" % talent_id)

    if problems or conflicts:
        for text in sorted(set(problems)):
            print("  構造の問題: %s" % text)
        for text in conflicts:
            print("  一番新しいシーズンで名前が食い違う: %s" % text)
        raise ValueError("RootBuffs を作れない(構造の問題 %d件 / 名前の食い違い %d件)" % (len(set(problems)), len(conflicts)))

    output = {}
    for kind in KINDS:
        entries = []
        for buff_id, (order, icon) in sorted(structure[kind].items(), key=lambda item: (item[1][0], item[0])):
            if kind == "SeasonTalentAdvanced":
                entries.append({"Buff": buff_id, "Icons": dict(sorted(advanced_icons.get(buff_id, {}).items(), key=lambda item: int(item[0])))})
            elif kind == "Talents":
                entries.append({"Buff": buff_id})
            else:
                entries.append({"Buff": buff_id, "Icon": icon})
        output[kind] = entries
    output["SeasonTalentAdvancedEffects"] = {
        str(buff_id): next(iter(advanced_ids)) for buff_id, advanced_ids in sorted(type_advanced.items())}
    output["TalentTrees"] = {
        source: [
            {"Profession": profession_id, "Spec": spec, "Root": tree["Root"],
             "Nodes": [dict({"Id": node_id}, **node) for node_id, node in sorted(tree["Nodes"].items())]}
            for (profession_id, spec), tree in sorted(trees.items())]
        for source, trees in talent_trees.items()}
    tree_differences = talent_tree_differences(talent_trees)

    print("→ 種類ごとの根のバフ: %s" % " / ".join("%s %d" % (kind, len(output[kind])) for kind in KINDS))
    for kind, label in (("Talents", "特化型"), ("SeasonTalentAdvanced", "上級の型の専用効果"), ("Factors", "因子の共通の説明")):
        buff_ids = sorted(excluded_by_kind.get(kind, ()))
        print("  外した%sのバフ %d件: %s" % (label, len(buff_ids), ", ".join(map(str, buff_ids))))
    print("  型の上級の番号: %d件" % len(output["SeasonTalentAdvancedEffects"]))
    for source, trees in output["TalentTrees"].items():
        print("  タレントの木 %s: クラスR1 %d本 / クラスR2 %d本 / 節 %d" % (
            source,
            sum(1 for tree in trees if not tree["Spec"]),
            sum(1 for tree in trees if tree["Spec"]),
            sum(len(tree["Nodes"]) for tree in trees)))
    print("  両方の出所で中身の違うタレントの木の節 %d件" % len(tree_differences))
    for text in tree_differences:
        print("    %s" % text)
    for source, trees in output["TalentTrees"].items():
        nodes = [node for tree in trees for node in tree["Nodes"]]
        print("  タレントの木 %s のアビリティの節 %d(バフ無し %d / 技の置き換え %d)" % (
            source,
            sum(1 for node in nodes if node["Ability"]),
            sum(1 for node in nodes if node["Ability"] and not node["Buffs"]),
            sum(1 for node in nodes if node["Replace"])))
    print("  名前を選んだ %d件" % len(resolved))
    for text in resolved:
        print("    %s" % text)
    print("  出所で絵が違う %d件" % len(icon_differences))
    for text in icon_differences:
        print("    %s" % text)
    print()

    keys = set()
    for kind in KINDS:
        if kind != "Talents":
            keys |= {str(buff_id) for buff_id in structure[kind]}
    write_localized("RootBuffNames", names, keys)
    write_localized("TalentNames", talent_names, {str(talent_id) for talent_id in talent_ids})
    dump(os.path.join(GENERATED, "RootBuffs.json"), output)
    print("  RootBuffs.json %s" % " / ".join("%s %d" % (kind, len(output[kind])) for kind in KINDS))


if __name__ == "__main__":
    main()
