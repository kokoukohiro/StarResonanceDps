"""
シーン名テーブルを生成する。

  Data/Localization/scenes.{言語}.json

`SceneTable.Name` が本体。`SceneTable` に無いシーンIDだけ `DungeonsTable` で補う。

鍵は4言語の和集合。言語によって `SceneTable` の収録数に差があるので、
無い行は空文字で入り、表示時に zh-CN へ落ちる。
"""
from _common import LANGS, name_of, table, write_localized


def main():
    scene_tables = {lang: table(lang_dir, "SceneTable") for lang, lang_dir in LANGS.items()}
    dungeon_tables = {lang: table(lang_dir, "DungeonsTable") for lang, lang_dir in LANGS.items()}

    keys = set()
    for lang in LANGS:
        keys |= set(scene_tables[lang])
    # SceneTable に無いシーンIDをダンジョン表で補う
    extra = set()
    for lang in LANGS:
        extra |= {i for i in dungeon_tables[lang] if i not in keys}
    keys |= extra
    for lang in LANGS:
        print("%-6s SceneTable %4d件 / DungeonsTable で補う %d件"
              % (lang, len(scene_tables[lang]), len(extra & set(dungeon_tables[lang]))))
    print("補った鍵: %s" % sorted(extra, key=int))

    values = {}
    for lang in LANGS:
        scenes, dungeons = scene_tables[lang], dungeon_tables[lang]
        values[lang] = {i: (name_of(scenes.get(i)) or name_of(dungeons.get(i))) for i in keys}
    print("")
    write_localized("scenes", values, keys)


if __name__ == "__main__":
    main()
