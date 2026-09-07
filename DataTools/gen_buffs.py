"""
バフ/デバフウィジェット用のバフ名テーブルを生成する。

  Data/Localization/buffs.{言語}.json

**収録するのは `BuffPriority != NotShow(0)` のバフだけ。** ゲーム内のバフバー
(`Abnormal_stateView`)が `buffVm:GetEntityBuffList(entity, EBuffPriority.NotShow, …)` を呼び、
`NotShow` のバフを出さないため。自分のバーもボスHPバーのバーも同じ関数で、対象が違うだけ。

鍵は4言語の和集合。cn は1606件、アジア版は1414件(192件はアジア版に存在しない)。
**表示条件そのものは同梱 `Data/BuffTable.json`(cn) で判定すること。** 言語別テーブルで
判定すると日本語利用者だけ192件表示されなくなる。
"""
from _common import LANGS, name_of, table, write_localized


def main():
    tables = {lang: table(lang_dir, "BuffTable") for lang, lang_dir in LANGS.items()}

    keys = set()
    for lang, buff_table in tables.items():
        shown = {i for i, row in buff_table.items() if (row.get("BuffPriority") or 0) != 0}
        keys |= shown
        print("%-6s BuffTable %5d件 / BuffPriority!=0 %4d件" % (lang, len(buff_table), len(shown)))

    values = {lang: {i: name_of(t.get(i)) for i in keys} for lang, t in tables.items()}
    print("")
    write_localized("buffs", values, keys)


if __name__ == "__main__":
    main()
