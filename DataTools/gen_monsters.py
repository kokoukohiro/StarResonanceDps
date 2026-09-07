"""
モンスター・NPC・訓練用ダミーの名前テーブルを生成する。

  Data/Localization/monsters.{言語}.json

エンティティリストやメーターで `AttrId` から表示名を引くのに使う。

鍵は **`MonsterTable` ∪ `DummyTable` ∪ `NpcTable`** の和集合（4言語ぶんの和）。
名前はこの順で「本物の名前」を持つ最初の表から取る。同じIDが複数の表にあり、
名前が食い違うことがあるため順序が要る（実測148件）。

```
52   DummyTable=共鸣技能卷心菜法师弱   NpcTable=西尔维      → NpcTable(Dummy 側は本物の名前でない)
108  MonsterTable=寒霜食人魔          DummyTable=雷        → MonsterTable
```

> **`MonsterTable` を丸ごと差し替えてはいけない。** 新しい unpack のほうが名前を失う。
> 3表で埋まるのは zh-CN で6938件だが、テーブルには472件それ以上の名前が入っている
> （`变身专用-虚蚀蒂娜` など、いまの unpack では空になっているもの）。
> `_common.write_localized` の「既存の名前は消さない」規則がこれを守る。
> **既存の値を捨てて作り直さないこと。**
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
