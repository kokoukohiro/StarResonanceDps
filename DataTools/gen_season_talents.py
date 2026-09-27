"""
シーズンタレントの型の根ノードの名前テーブルを生成する。

  Data/Localization/SeasonTalentNames.json

シーズンタレントは型ごとに木があり、型を付けるとその根ノードの効果のバフが乗る。
そのバフから、その人が付けている型の根ノードを引くための「根ノードのバフID → 根ノードの名前」。

根ノードは `SeasonTalentTemplateTable.NoteRootId`。`SeasonTalentEffectOrdinaryTable` のうち `GroupId` がそれに一致する行が
根ノードの効果で、`Effect` の `[3, バフID, 1]` がそのバフ、`Name` が根ノードの名前。根以外のノードは入れない。

入れるのは主の型だけ。効く遊びの種類(`EffectiveGameplayType`)が決まっている型は、その遊びの中だけで主の型の上に足されるので入れない。

**出所の土台はバフIDの単位で決める**(cn / en は `Star`、jp / kr は `StarASIA`)。土台にそのバフIDの名前があれば土台だけを使い、
無いときだけもう一方を使う(`gen_rogue_entries.py` と同じ)。

**同じ出所の中で1つのバフIDに複数の根ノードが当たったら、型の `BelongSeasonId` が一番大きい行を採り、一覧を出す。**
シーズンをまたいで同じバフを根に使う型がある。行やノードの番号の大小はシーズンの新旧と一致しないので使わない。

次のときは一覧を出して止める。
- 一番新しいシーズンの中で、同じバフIDの名前が食い違う
- 型の根ノードに当たる行が無い
- 根ノードの行のバフIDが1つでない
- 出所に型の表が無い
"""
from _common import LANGS, name_of, tables_by_source, write_localized

TEMPLATE_TABLE = "SeasonTalentTemplateTable"
ORDINARY_TABLE = "SeasonTalentEffectOrdinaryTable"
# `Effect` の要素 `[種類, 値, …]` のうち、バフを付ける種類。
EFFECT_ADD_BUFF = 3


def buff_ids_of(row):
    """行の `Effect` のうち、バフを付けるもののバフID。"""
    return [effect[1] for effect in row.get("Effect") or []
            if isinstance(effect, list) and len(effect) >= 2 and effect[0] == EFFECT_ADD_BUFF
            and isinstance(effect[1], int) and effect[1] > 0]


def roots_of(label, templates, ordinary, problems):
    """1つの出所の表から「バフID → [(シーズン, 型ID, 名前)]」を作る。"""
    rows_by_group = {}
    for row in ordinary.values():
        rows_by_group.setdefault(row.get("GroupId"), []).append(row)

    found = {}
    for template_id, template in templates.items():
        # 効く遊びの種類が決まっている型は、特定の遊びの中だけで主の型の上に足される型なので入れない。
        if template.get("EffectiveGameplayType"):
            continue
        root = template.get("NoteRootId")
        rows = rows_by_group.get(root, [])
        if not rows:
            problems.append("%s 型 %s の根ノード %s に当たる行が無い" % (label, template_id, root))
            continue
        for row in rows:
            buff_ids = buff_ids_of(row)
            if len(buff_ids) != 1:
                problems.append("%s 型 %s の根ノードの行 %s のバフIDが %s" % (label, template_id, row.get("Id"), buff_ids))
                continue
            found.setdefault(str(buff_ids[0]), []).append(
                (template.get("BelongSeasonId") or 0, int(template_id), name_of(row)))
    return found


def main():
    values = {lang: {} for lang in LANGS}
    problems = []
    conflicts = []
    resolved = []
    for lang, lang_dir in LANGS.items():
        templates_by_source = dict(tables_by_source(lang_dir, TEMPLATE_TABLE))
        counts = []
        for source_name, ordinary in tables_by_source(lang_dir, ORDINARY_TABLE):
            label = "%s %s" % (lang_dir, source_name)
            templates = templates_by_source.get(source_name)
            if templates is None:
                problems.append("%s に %s が無い" % (label, TEMPLATE_TABLE))
                continue
            found = roots_of(label, templates, ordinary, problems)
            for key, candidates in found.items():
                # 先の出所(土台)で名前が決まったバフIDには、後の出所を混ぜない。
                if values[lang].get(key):
                    continue
                named = [c for c in candidates if c[2]]
                if not named:
                    continue
                newest = max(season for season, _, _ in named)
                newest_names = sorted({name for season, _, name in named if season == newest})
                listing = " / ".join("型 %d シーズン %d「%s」" % (template_id, season, name)
                                     for season, template_id, name in sorted(named))
                if len(newest_names) > 1:
                    conflicts.append("%s %s %s" % (lang, key, listing))
                    continue
                values[lang][key] = newest_names[0]
                if len({name for _, _, name in named}) > 1:
                    resolved.append("%s %s %s(%s)→ シーズン %d を採る" % (lang, key, source_name, listing, newest))
            counts.append("%s %d" % (source_name, len(found)))
        print("%-6s 根ノードのバフID %3d(%s)" % (lang, len(values[lang]), " / ".join(counts)))

    if problems or conflicts:
        for text in problems:
            print("  構造の問題: %s" % text)
        for text in conflicts:
            print("  一番新しいシーズンで名前が食い違う: %s" % text)
        raise ValueError("%s を作れない(構造の問題 %d件 / 名前の食い違い %d件)"
                         % (TEMPLATE_TABLE, len(problems), len(conflicts)))

    keys = set()
    for lang in LANGS:
        keys |= set(values[lang])

    print("→ 4言語の和集合 %d件" % len(keys))
    print("  シーズンで決めた %d件" % len(resolved))
    for text in resolved:
        print("    %s" % text)
    print()

    write_localized("SeasonTalentNames", values, keys)


if __name__ == "__main__":
    main()
