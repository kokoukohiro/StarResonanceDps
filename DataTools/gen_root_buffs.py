"""
根のバフの名前・種類・並び・絵を生成する。

  Data/Localization/RootBuffNames.json
  Data/Generated/RootBuffs.json

根のバフは、タレント・心相晶のノード・心相晶の上級・因子・モジュール効果を付けている間、ずっと乗るバフ。
それぞれの効果の `[3, バフID, 1]` がそのバフ。アプリはプレイヤーに乗っている根のバフから、
アビリティ情報(タレント)・潜在心相晶情報(ノード・上級・因子)・モジュール情報(モジュール効果)の一覧を出す。
付与元をたどってモジュールの根に着いたバフの名前も `RootBuffNames.json` から引く。

| 種類 | 表 | 名前 | 絵 | 並び |
|---|---|---|---|---|
| タレント | `TalentTable.TalentEffect` | `TalentName` | `TalentIcon` | `Id` |
| 心相晶のノード | `SeasonTalentEffectOrdinaryTable.Effect` | `Name` | `IconNormal` | `Id` |
| 心相晶の上級 | `SeasonTalentEffectAdvancedTable.Effect` | `Name` | `Icon`(型の上級の番号ごと) | `Id` |
| 因子 | `SeasonTalentFactorItemTable.FactorItemEffect` | `ItemTable.Name` から等級を外した名前 | `ItemTable.Icon` | バフID |
| モジュール効果 | `ModEffectTable.EffectConfig` | `EffectName` | `EffectConfigIcon` | `EffectID` |

タレントは、段階の根のタレント(`TalentStageTable.RootId` → `TalentTreeTable.TalentId`)のバフを入れない。
特化型のバフで、特化はプレイヤーリストが出す。
タレントは役割のタグ `TalentTag` を持つ。タレントの木の職業(`TalentTreeTable.WeaponType`)の `ProfessionSystemTable.Talent`
(`TalentTagTable` の番号。1 攻撃 / 2 支援 / 3 防御)。アプリは絵の背景の色を選ぶのに使う。

心相晶のノードは、型の根のノード(`SeasonTalentTemplateTable.NoteRootId`。型はプレイヤーリストが出す)と、
効く遊びの種類(`EffectiveGameplayType`)が決まっている型の木(`SeasonTalentTreeTable.TemplateId`)のノードを入れない。

心相晶の上級は、1つの型の上級(`EffectId`)にしか出てこないバフ(型の専用効果)を入れない。入れるのは全部の型が使う共通の効果。
上級の絵は、同じバフでも型ごとに違う(型の `AdvancedEffectId` = 上級の行の `EffectId`)。
`RootBuffs.json` は上級の番号ごとの絵と、型の根のバフ → 上級の番号(0 の型は上級を持たないので入れない)を持つ。

因子は、等級を外した名前が2つ以上になる根(シーズンが変わって効果を失った因子の共通の説明のバフ)を入れない。

**名前の土台はバフIDの単位で決める**(cn / en は `Star`、jp / kr は `StarASIA`)。土台にそのバフIDの名前があれば土台だけを使い、
無いときだけもう一方を使う。心相晶のノードで同じ出所の中の1つのバフIDに複数の行が当たったら、型の `BelongSeasonId` が一番大きい行を採る。
ほかの種類で1つのバフIDに名前の違う行が当たったら、並びの一番小さい行を採り、一覧を出す。

**種類・並び・絵は `StarASIA` を土台にし、無いバフだけ `Star` で補う。** 出所で絵が違うバフは一覧を出す。

次のときは一覧を出して止める。
- 1つのバフIDが2つの種類に当たる
- 段階の根が木の表に無い、上級の番号が1つしか無い(型の専用効果を見分けられない)
- タレントの役割のタグが1つに決まらない、1〜3 のどれでもない
- 心相晶のノードの行がどの型の木にも無い、主の型とローグライクの型の両方の木に入る
- 一番新しいシーズンの中で、同じノードのバフIDの名前が食い違う
- 型の根のバフに上級の番号が2つ以上当たる、1つのモジュールの根のバフに効果が2つ以上当たる
"""
import os
import re

from _common import GENERATED, LANGS, dump, name_of, table_of_source, tables_by_source, write_localized

# 効果の要素 `[種類, 値, …]` のうち、バフを付ける種類。
EFFECT_ADD_BUFF = 3

# 種類・並び・絵を読む出所と言語フォルダ。先にあるほうが土台。
STRUCTURE_SOURCES = (("StarASIA", "jp"), ("Star", "cn"))

KINDS = ("Talents", "SeasonTalentNodes", "SeasonTalentAdvanced", "Factors", "ModuleEffects")

# タレントの役割のタグ(`TalentTagTable` の番号)。1 攻撃 / 2 支援 / 3 防御。
TALENT_TAGS = (1, 2, 3)

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


def talent_tags_of(source, lang_dir, tree):
    """タレント → 役割のタグの集合。タレントの木の職業(`TalentTreeTable.WeaponType`)の `ProfessionSystemTable.Talent`。"""
    professions = table_of_source(source, lang_dir, "ProfessionSystemTable") or {}
    tags = {}
    for node in tree.values():
        tags.setdefault(node.get("TalentId"), set()).add(professions.get(str(node.get("WeaponType")), {}).get("Talent"))
    return tags


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
    1つの出所の表から「種類 → バフID → [(並び, 名前, 絵, 付記)]」を作る。付記はタレントの役割のタグ、ノードのシーズン、上級の番号。
    外したバフは「種類 → [バフID]」で返す。
    """
    label = "%s %s" % (lang_dir, source)
    problems = []
    found = {kind: {} for kind in KINDS}

    def add(kind, buff_id, order, name, icon, extra=None):
        found[kind].setdefault(buff_id, []).append((order, name, (icon or "").strip(), extra))

    talents = table_of_source(source, lang_dir, "TalentTable") or {}
    talent_tree = table_of_source(source, lang_dir, "TalentTreeTable") or {}
    spec_buffs = stage_root_buffs(source, lang_dir, talents, talent_tree, problems, label)
    talent_tags = talent_tags_of(source, lang_dir, talent_tree)
    for row in talents.values():
        for buff_id in buff_ids_of(row.get("TalentEffect")):
            if buff_id in spec_buffs:
                continue
            tags = talent_tags.get(row["Id"], set())
            tag = next(iter(tags)) if len(tags) == 1 else None
            if tag not in TALENT_TAGS:
                problems.append("%s タレント %s の役割のタグが決まらない(%s)" % (label, row["Id"], sorted(tags, key=str)))
            add("Talents", buff_id, row["Id"], name_of(row, "TalentName"), row.get("TalentIcon"), tag)

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
    talent_tags = {}
    type_advanced = {}
    icon_differences = []
    excluded_by_kind = {}
    for source, lang_dir in STRUCTURE_SOURCES:
        found, source_problems, excluded, templates = rows_of_source(source, lang_dir)
        problems.extend(source_problems)
        for kind, buff_ids in excluded.items():
            excluded_by_kind.setdefault(kind, set()).update(buff_ids)
        for kind in KINDS:
            for buff_id, rows in found[kind].items():
                rows = sorted(rows, key=lambda row: row[0])
                order, _, icon, _ = rows[0]
                if kind == "Talents":
                    tags = {tag for _, _, _, tag in rows}
                    if len(tags) > 1 or talent_tags.get(buff_id, rows[0][3]) != rows[0][3]:
                        problems.append("タレントのバフ %d の役割のタグが食い違う(%s %s / 前の出所 %s)"
                                        % (buff_id, source, sorted(tags, key=str), talent_tags.get(buff_id)))
                    talent_tags.setdefault(buff_id, rows[0][3])
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
                entries.append({"Buff": buff_id, "Icon": icon, "TalentTag": talent_tags[buff_id]})
            else:
                entries.append({"Buff": buff_id, "Icon": icon})
        output[kind] = entries
    output["SeasonTalentAdvancedEffects"] = {
        str(buff_id): next(iter(advanced_ids)) for buff_id, advanced_ids in sorted(type_advanced.items())}

    print("→ 種類ごとの根のバフ: %s" % " / ".join("%s %d" % (kind, len(output[kind])) for kind in KINDS))
    for kind, label in (("Talents", "特化型"), ("SeasonTalentAdvanced", "上級の型の専用効果"), ("Factors", "因子の共通の説明")):
        buff_ids = sorted(excluded_by_kind.get(kind, ()))
        print("  外した%sのバフ %d件: %s" % (label, len(buff_ids), ", ".join(map(str, buff_ids))))
    print("  型の上級の番号: %d件" % len(output["SeasonTalentAdvancedEffects"]))
    print("  タレントの役割のタグ: %s" % " / ".join(
        "%d が %d件" % (tag, sum(1 for e in output["Talents"] if e["TalentTag"] == tag)) for tag in TALENT_TAGS))
    print("  名前を選んだ %d件" % len(resolved))
    for text in resolved:
        print("    %s" % text)
    print("  出所で絵が違う %d件" % len(icon_differences))
    for text in icon_differences:
        print("    %s" % text)
    print()

    keys = set()
    for kind in KINDS:
        keys |= {str(buff_id) for buff_id in structure[kind]}
    write_localized("RootBuffNames", names, keys)
    dump(os.path.join(GENERATED, "RootBuffs.json"), output)
    print("  RootBuffs.json %s" % " / ".join("%s %d" % (kind, len(output[kind])) for kind in KINDS))


if __name__ == "__main__":
    main()
