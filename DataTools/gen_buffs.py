"""
バフ/デバフウィジェット用のバフ名テーブルを生成する。

  Data/Localization/buffs.{言語}.json

**収録するのは `BuffPriority` が 0 でないバフだけ。**
ゲーム内のバフ表示と対象をそろえるため。

鍵は4言語の和集合。言語によって収録数に差がある。

> **表示条件そのものはこのファイルで判定しない。**
> 同梱 `Data/BuffTable.json` で判定する。言語別テーブルで判定すると、
> 片方にしか無いバフが特定の表示言語でだけ出なくなる。
"""
from _common import LANGS, name_of, table, write_localized


def main():
    tables = {lang: table(lang_dir, "BuffTable") for lang, lang_dir in LANGS.items()}

    keys = set()
    for lang, buff_table in tables.items():
        shown = {i for i, row in buff_table.items() if (row.get("BuffPriority") or 0) != 0}
        keys |= shown
        print("%-6s BuffTable %5d / BuffPriority!=0 %4d" % (lang, len(buff_table), len(shown)))

    values = {lang: {i: name_of(t.get(i)) for i in keys} for lang, t in tables.items()}
    print("")
    write_localized("buffs", values, keys)


if __name__ == "__main__":
    main()
