"""
ボス大技の予告(DBM)の名前テーブルを生成する。

  Data/Localization/DbmNames.json

ゲームが予告に出す正式な技名。`SkillTable.Name` が埋め草の技にも名前がある。

**鍵は技ID。** `DbmTable.Id` は技IDそのものか、ある技の `SkillTable.EffectIDs` の要素のどちらかで、
後者は持ち主の技IDへ寄せる。どちらにも当たらない行は入れない(一覧を出す)。

`DbmTable` の1行が複数の技に当たる(その `EffectIDs` の要素を複数の技が持つ)、1つの技に複数の行が当たる、
のどちらかが起きたら止まる。どれを採るか決められないので、黙って片方を選ばない。
`EffectIDs` の要素を複数の技が共有すること自体は、`DbmTable` に無い番号では普通にある。

鍵は言語の和集合。言語によって収録数に差がある。
"""
from _common import LANGS, name_of, table, write_localized


def effect_owners(skill_table):
    """`EffectIDs` の要素 → 持ち主の技IDの集合。"""
    owners = {}
    for skill_id, row in skill_table.items():
        for effect_id in (row.get("EffectIDs") or []):
            owners.setdefault(str(effect_id), set()).add(skill_id)
    return owners


def owner_skill_id(dbm_id, skill_table, owners):
    """DbmTable の行が指す技ID。どの技にも当たらなければ None。"""
    if dbm_id in skill_table:
        return dbm_id
    candidates = owners.get(dbm_id)
    if not candidates:
        return None
    if len(candidates) > 1:
        raise ValueError("DbmTable の %s が技 %s の複数に当たる" % (dbm_id, sorted(candidates, key=int)))
    return next(iter(candidates))


def main():
    values = {}
    keys = set()
    for lang, lang_dir in LANGS.items():
        skill_table = table(lang_dir, "SkillTable")
        dbm_table = table(lang_dir, "DbmTable")
        owners = effect_owners(skill_table)

        names = {}
        sources = {}
        unmatched = []
        for dbm_id, row in dbm_table.items():
            skill_id = owner_skill_id(dbm_id, skill_table, owners)
            if skill_id is None:
                unmatched.append(dbm_id)
                continue
            if skill_id in sources:
                raise ValueError("技 %s に DbmTable の %s と %s が当たる" % (skill_id, sources[skill_id], dbm_id))
            sources[skill_id] = dbm_id
            names[skill_id] = name_of(row, "Content")

        values[lang] = names
        keys |= set(names)
        print("%-6s DbmTable %4d → 技 %4d / 技に当たらない %d %s"
              % (lang, len(dbm_table), len(names), len(unmatched), sorted(unmatched, key=int)))

    print("→ 和集合 %d" % len(keys))
    print("")
    write_localized("DbmNames", values, keys)


if __name__ == "__main__":
    main()
