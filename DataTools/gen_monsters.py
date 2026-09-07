"""
モンスター・NPC・訓練用ダミーの名前テーブルを生成する。

  Data/Localization/monsters.{言語}.json

エンティティリストやメーターで `AttrId` から表示名を引くのに使う。

鍵は **`MonsterTable` ∪ `DummyTable` ∪ `NpcTable`** の和集合(4言語ぶんの和)。
名前は SOURCE_TABLES の順で「本物の名前」を持つ最初の表から取る。
同じIDが複数の表にあって名前が食い違うことがあるので順序が要る。

`MonsterTable` は行があっても `Name` が空のことがあり、その数は言語で大きく違う。
埋まらないIDは入力側に名前が無いだけで、他の表にも代わりは無い。
"""
from _common import LANGS, name_of, table, write_localized

# 名前を採る順序。先にあるものが勝つ。
SOURCE_TABLES = ("MonsterTable", "DummyTable", "NpcTable")


def main():
    tables = {lang: [table(lang_dir, name) for name in SOURCE_TABLES]
              for lang, lang_dir in LANGS.items()}

    keys = set()
    for lang, sources in tables.items():
        per_lang = set()
        for source in sources:
            per_lang |= set(source)
        keys |= per_lang
        print("%-6s %s の和集合 %5d件"
              % (lang, " ∪ ".join(SOURCE_TABLES), len(per_lang)))
    print("→ 4言語の和集合 %d件\n" % len(keys))

    values = {}
    for lang, sources in tables.items():
        names = {}
        for key in keys:
            for source in sources:
                value = name_of(source.get(key))
                if value:
                    names[key] = value
                    break
        values[lang] = names

    write_localized("monsters", values, keys)


if __name__ == "__main__":
    main()
