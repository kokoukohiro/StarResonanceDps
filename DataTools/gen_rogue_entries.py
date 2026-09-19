"""
オプション(ローグ系モード)の名前テーブルを生成する。

  Data/Localization/RogueEntryNames.json

メーターの行が見出し表で名前を持たないとき、記録時に付与元をたどってオプションのバフに着いたら、
そのオプションの名前を出す。そのための「オプションのバフID → オプション名」。

鍵は **`RogueEntryTable.BuffId`**(4言語ぶんの和)、値は `EntryName`。
`EntryId` は鍵にしない。同じバフを複数のオプションの行が指す。

**出所の土台はバフIDの単位で決める**(cn / en は `Star`、jp / kr は `StarASIA`)。土台にそのバフIDの名前があれば土台だけを使い、
無いときだけもう一方を使う。`_common.table` の行単位の合併を使うと、土台に無い `EntryId` の行が
もう一方の出所から混ざり、同じバフIDに2つの出所の訳が並んでしまう。

**同じバフIDで名前が食い違う言語は、`EntryId` が一番若い行の名前を採り、一覧を出す。**
空にも除外にもしない。`EntryId` は表の鍵で重ならないので、必ず1つに決まる。
"""
from _common import LANGS, name_of, tables_by_source, write_localized

SOURCE_TABLE = "RogueEntryTable"


def buff_ids_of(row):
    """行の `BuffId`。数か数の配列で、0 は持たない扱い。"""
    value = row.get("BuffId")
    values = value if isinstance(value, list) else [value]
    return [v for v in values if isinstance(v, int) and v > 0]


def main():
    names = {lang: {} for lang in LANGS}
    conflicts = {}
    for lang, lang_dir in LANGS.items():
        counts = []
        for source_name, source in tables_by_source(lang_dir, SOURCE_TABLE):
            found = {}
            for entry_id, row in source.items():
                name = name_of(row, "EntryName")
                for buff_id in buff_ids_of(row):
                    seen = found.setdefault(str(buff_id), {})
                    if name:
                        seen[int(entry_id)] = name
            for key, seen in found.items():
                # 先の出所(土台)で名前が決まったバフIDには、後の出所を混ぜない。
                if not names[lang].get(key):
                    names[lang][key] = seen
            counts.append("%s %d" % (source_name, len(found)))
        print("%-6s %s バフID %4d(%s)" % (lang, SOURCE_TABLE, len(names[lang]), " / ".join(counts)))

    keys = set()
    for lang in LANGS:
        keys |= set(names[lang])

    values = {lang: {} for lang in LANGS}
    for key in keys:
        for lang in LANGS:
            by_entry = names[lang].get(key, {})
            if not by_entry:
                values[lang][key] = ""
                continue
            youngest = min(by_entry)
            values[lang][key] = by_entry[youngest]
            if len(set(by_entry.values())) > 1:
                conflicts.setdefault(key, {})[lang] = "EntryId %d「%s」を採る(%s)" % (
                    youngest, by_entry[youngest],
                    " / ".join("%d「%s」" % (entry_id, by_entry[entry_id]) for entry_id in sorted(by_entry)))

    print("→ 4言語の和集合 %d件" % len(keys))
    print("  名前が食い違う %d件" % len(conflicts))
    for key in sorted(conflicts, key=int):
        for lang, text in conflicts[key].items():
            print("    %s %-6s %s" % (key, lang, text))
    print()

    write_localized("RogueEntryNames", values, keys)


if __name__ == "__main__":
    main()
