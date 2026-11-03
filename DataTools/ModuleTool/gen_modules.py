"""
モジュールツールの表と名前を生成する。

  Data/ModuleTables.json   数値と絵(言語に依らない)
  Data/ModuleNames.json    名前(4言語)

ModuleTables.json:
  SlotCount            モジュールの枠の数(`ModHoleTable` の行の数)
  Effects              効果の番号 → 絵のファイル名と段階ごとの「必要なリンク数・戦闘力」(`ModEffectTable`)
  Modules              モジュールのアイテムID → 絵のファイル名と品質(`ModTable` の行、`ItemTable` の `Icon` と `Quality`)
  LinkFightValues      出所ごとの「リンクの総和 → 戦闘力」の並び(`ModLinkEffectTable`、添字がリンクの総和)
  RecommendedEffects   特化の番号 → おすすめの効果の並び(`TalentStageTable.RecommendModEffectId`)

ModuleNames.json:
  Effects / Modules / Specs   鍵ごとの4言語の名前。効果は `ModEffectTable.EffectName`、モジュールは `ItemTable.Name`、
                              特化は `TalentStageTable.Name` の2番目から全特化に共通する語尾を落としたもの

特化の番号は 職業ID × 10000 ＋ 特化の並び(1始まり、`ProfessionSystemTable.ShowTalentStage` の並び)。
名前の出所の土台は言語フォルダごと(`_common.table`)。数値は出所をまたいで同じでなければならない。

次のときは一覧を出して止める。
- 効果の段階の数値(必要なリンク数・戦闘力)か絵が出所で違う、効果の段階が 0 から続いていない
- リンクの総和が 0 から続いていない、出所の両方にあるリンクの総和で戦闘力が違う
- 枠の数が出所で違う
- モジュールの行がアイテムの表に無い、絵が無い、絵か品質が言語で違う
- おすすめの効果が出所で違う、効果の表に無い
- 特化の段階の行が無い、段階の名前が2要素でない、名前が共通の語尾で終わっていない
"""
import sys

# _common を読み込む前に置く(後ろだと __pycache__ が作られる)
sys.dont_write_bytecode = True

from _common import LANGS, SOURCES, dump, name_of, named, per_lang, table, table_of_source, tables_by_source

# 数値を比べる言語フォルダ(出所ごとに1つ)。数値は言語に依らない。
NUMBER_LANG_DIRS = {"Star": "cn", "StarASIA": "jp"}
# 特化の番号は 職業ID × これ ＋ 特化の並び(1始まり)。アプリの特化の番号と同じ決め方。
PROFESSION_DIGIT = 10000
# 言語フォルダごとの、全特化の名前に共通する語尾。名前から落とす。
COMMON_SUFFIX = {"cn": "流", "en": " Spec", "jp": "型", "kr": " 계열"}


def file_name(path):
    return (path or "").split("/")[-1]


def build_effects(problems):
    """効果の番号 → 絵と段階の並び。出所で違えば問題に積む。"""
    by_source = {}
    for source in SOURCES:
        rows = table_of_source(source, NUMBER_LANG_DIRS[source], "ModEffectTable")
        if rows is None:
            problems.append("%s に ModEffectTable が無い" % source)
            continue
        effects = {}
        for row in rows.values():
            effect = effects.setdefault(str(row["EffectID"]), {"Icon": set(), "Levels": {}})
            effect["Icon"].add(file_name(row["EffectConfigIcon"]))
            effect["Levels"][row["Level"]] = {"Links": row["EnhancementNum"], "FightValue": row["FightValue"]}
        by_source[source] = effects

    result = {}
    for source, effects in by_source.items():
        for effect_id, effect in effects.items():
            icons = sorted(name for name in effect["Icon"] if name)
            if len(icons) != 1:
                problems.append("%s 効果 %s の絵が1つでない: %s" % (source, effect_id, icons))
                continue
            levels = [effect["Levels"][level] for level in sorted(effect["Levels"])]
            if sorted(effect["Levels"]) != list(range(len(levels))):
                problems.append("%s 効果 %s の段階が 0 から続いていない: %s" % (source, effect_id, sorted(effect["Levels"])))
                continue
            built = {"Icon": icons[0], "Levels": levels}
            if effect_id in result and result[effect_id] != built:
                problems.append("効果 %s の数値か絵が出所で違う" % effect_id)
                continue
            result[effect_id] = built
    return result


def build_link_fight_values(problems):
    """出所ごとの「リンクの総和 → 戦闘力」の並び。"""
    result = {}
    for source in SOURCES:
        rows = table_of_source(source, NUMBER_LANG_DIRS[source], "ModLinkEffectTable")
        if rows is None:
            problems.append("%s に ModLinkEffectTable が無い" % source)
            continue
        values = {row["LinkTime"]: row["FightValue"] for row in rows.values()}
        if sorted(values) != list(range(len(values))):
            problems.append("%s のリンクの総和が 0 から続いていない" % source)
            continue
        result[source] = [values[link] for link in range(len(values))]

    if len(result) == 2:
        star, asia = result["Star"], result["StarASIA"]
        differ = [link for link in range(min(len(star), len(asia))) if star[link] != asia[link]]
        if differ:
            problems.append("両方の出所にあるリンクの総和で戦闘力が違う: %s" % differ)
    return result


def build_slot_count(problems):
    counts = {}
    for source in SOURCES:
        rows = table_of_source(source, NUMBER_LANG_DIRS[source], "ModHoleTable")
        if rows is None:
            problems.append("%s に ModHoleTable が無い" % source)
            continue
        counts[source] = len(rows)
    if len(set(counts.values())) != 1:
        problems.append("枠の数が出所で違う: %s" % counts)
        return 0
    return next(iter(counts.values()))


def build_modules(problems):
    """モジュールのアイテムID → 絵と4言語の名前。"""
    module_ids = set()
    for source in SOURCES:
        rows = table_of_source(source, NUMBER_LANG_DIRS[source], "ModTable")
        if rows is None:
            problems.append("%s に ModTable が無い" % source)
            continue
        module_ids |= set(rows)

    modules = {}
    names = {lang: {} for lang in LANGS}
    for lang, lang_dir in LANGS.items():
        items = table(lang_dir, "ItemTable")
        for module_id in module_ids:
            item = items.get(module_id)
            if item is None:
                problems.append("%s モジュール %s がアイテムの表に無い" % (lang_dir, module_id))
                continue
            names[lang][module_id] = name_of(item)
            icon = file_name(item.get("Icon"))
            if not icon:
                problems.append("%s モジュール %s の絵が無い" % (lang_dir, module_id))
                continue
            built = {"Icon": icon, "Quality": item["Quality"]}
            if modules.setdefault(module_id, built) != built:
                problems.append("モジュール %s の絵か品質が言語で違う" % module_id)
    return {module_id: modules[module_id] for module_id in sorted(modules, key=int)}, names


def build_effect_names(effect_ids):
    names = {lang: {} for lang in LANGS}
    for lang, lang_dir in LANGS.items():
        for _, rows in reversed(tables_by_source(lang_dir, "ModEffectTable")):
            for row in rows.values():
                name = name_of(row, "EffectName")
                if name:
                    names[lang][str(row["EffectID"])] = name
    return {effect_id: per_lang(names, effect_id) for effect_id in effect_ids}


def specs_of(label, professions, stages, problems):
    """1つの出所の表から「特化の番号 → 段階の行」を作る。"""
    found = {}
    for profession_id, profession in professions.items():
        for index, stage_id in enumerate(profession.get("ShowTalentStage") or []):
            stage = stages.get(str(stage_id))
            if stage is None:
                problems.append("%s 職業 %s の段階 %s が TalentStageTable に無い" % (label, profession_id, stage_id))
                continue
            found[str(int(profession_id) * PROFESSION_DIGIT + index + 1)] = stage
    return found


def build_recommended(effect_ids, problems):
    """特化の番号 → おすすめの効果の並び。おすすめの無い特化は入れない。"""
    by_source = {}
    for source in SOURCES:
        lang_dir = NUMBER_LANG_DIRS[source]
        professions = table_of_source(source, lang_dir, "ProfessionSystemTable")
        stages = table_of_source(source, lang_dir, "TalentStageTable")
        if professions is None or stages is None:
            problems.append("%s に ProfessionSystemTable か TalentStageTable が無い" % source)
            continue
        by_source[source] = {
            spec: list(stage.get("RecommendModEffectId") or [])
            for spec, stage in specs_of(source, professions, stages, problems).items()
        }

    result = {}
    for source, specs in by_source.items():
        for spec, effects in specs.items():
            if not effects:
                continue
            unknown = [effect for effect in effects if str(effect) not in effect_ids]
            if unknown:
                problems.append("%s 特化 %s のおすすめの効果が効果の表に無い: %s" % (source, spec, unknown))
                continue
            if spec in result and result[spec] != effects:
                problems.append("特化 %s のおすすめの効果が出所で違う: %s / %s" % (spec, result[spec], effects))
                continue
            result[spec] = effects
    return {spec: result[spec] for spec in sorted(result, key=int)}


def build_spec_names(spec_ids, problems):
    """特化の番号 → 4言語の名前(共通の語尾を落とす)。土台にその鍵の名前があれば土台だけを使う。"""
    names = {lang: {} for lang in LANGS}
    for lang, lang_dir in LANGS.items():
        suffix = COMMON_SUFFIX[lang_dir]
        stages_by_source = dict(tables_by_source(lang_dir, "TalentStageTable"))
        decided = set()
        for source, professions in tables_by_source(lang_dir, "ProfessionSystemTable"):
            label = "%s %s" % (lang_dir, source)
            stages = stages_by_source.get(source)
            if stages is None:
                problems.append("%s に TalentStageTable が無い" % label)
                continue
            for spec, stage in specs_of(label, professions, stages, problems).items():
                if spec not in spec_ids or spec in decided:
                    continue
                stage_names = stage.get("Name") or []
                if len(stage_names) != 2:
                    problems.append("%s 特化 %s の段階の名前が2要素でない: %s" % (label, spec, stage_names))
                    continue
                name = (stage_names[1] or "").strip()
                if not named(name):
                    continue
                decided.add(spec)
                if not name.endswith(suffix):
                    problems.append("%s 特化 %s の名前「%s」が共通の語尾「%s」で終わらない" % (label, spec, name, suffix))
                    continue
                names[lang][spec] = name[:-len(suffix)].strip()
    return {spec: per_lang(names, spec) for spec in sorted(spec_ids, key=int)}


def main():
    problems = []
    effects = build_effects(problems)
    link_fight_values = build_link_fight_values(problems)
    slot_count = build_slot_count(problems)
    modules, module_names = build_modules(problems)
    recommended = build_recommended(set(effects), problems)
    spec_names = build_spec_names(set(recommended), problems)
    effect_names = build_effect_names(sorted(effects, key=int))

    if problems:
        for text in problems:
            print("  問題: %s" % text)
        raise ValueError("モジュールの表を作れない(%d件)" % len(problems))

    dump("ModuleTables", {
        "SlotCount": slot_count,
        "Effects": {effect_id: effects[effect_id] for effect_id in sorted(effects, key=int)},
        "Modules": modules,
        "LinkFightValues": link_fight_values,
        "RecommendedEffects": recommended,
    })
    dump("ModuleNames", {
        "Effects": effect_names,
        "Modules": {module_id: per_lang(module_names, module_id) for module_id in modules},
        "Specs": spec_names,
    })

    print("効果 %d / モジュール %d / 枠 %d / リンクの表 %s / おすすめのある特化 %d"
          % (len(effects), len(modules), slot_count,
             " / ".join("%s 0〜%d" % (source, len(values) - 1) for source, values in link_fight_values.items()),
             len(recommended)))
    for title, names in (("効果", effect_names), ("モジュール", module_names), ("特化", spec_names)):
        if title == "モジュール":
            counts = {lang: sum(1 for module_id in modules if names[lang].get(module_id)) for lang in LANGS}
        else:
            counts = {lang: sum(1 for value in names.values() if value[lang]) for lang in LANGS}
        print("  %s の名前: %s" % (title, " / ".join("%s %d" % (lang, count) for lang, count in counts.items())))


if __name__ == "__main__":
    main()
