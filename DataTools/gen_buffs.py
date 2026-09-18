"""
バフ名テーブルを生成する。

  Data/Localization/BuffNames.json

**`BuffTable` の全行を収録する。** 使い道を決めて絞らない。
バフ/デバフウィジェットに加えて、バフ由来の被ダメなどバフ欄に出ないバフも名前を引ける。

鍵は4言語の和集合。言語によって収録数に差がある。

未翻訳の行は埋め草(`_common.PLACEHOLDERS`)が入っているので空にする。
埋め草 `气刃突刺计数` は先頭行 `201` の名前だが、`201` 自身も4言語とも中国語のままなので同じく空にする。

> **表示条件そのものはこのファイルで判定しない。**
> 同梱 `Data/Raw/BuffTable.json` の `BuffPriority` で判定する。言語別テーブルで判定すると、
> 片方にしか無いバフが特定の表示言語でだけ出なくなる。
"""
from _common import LANGS, name_of, table, write_localized


def main():
    tables = {lang: table(lang_dir, "BuffTable") for lang, lang_dir in LANGS.items()}

    keys = set()
    for lang, buff_table in tables.items():
        keys |= set(buff_table)
        print("%-6s BuffTable %5d" % (lang, len(buff_table)))
    print("→ 和集合 %d" % len(keys))

    values = {lang: {i: name_of(t.get(i)) for i in keys} for lang, t in tables.items()}
    print("")
    write_localized("BuffNames", values, keys)


if __name__ == "__main__":
    main()
