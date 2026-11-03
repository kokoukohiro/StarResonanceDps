"""
アイテムの名前テーブルを生成する。

  Data/Localization/ItemNames.json

鍵はアイテムID(`ItemTable` の全行、4言語ぶんの和)。値は `ItemTable.Name`。名前の空の鍵も残す。
装備IDもアイテムIDなので、装備の名前もここから引く。

名前の出所の土台は言語フォルダごと(`_common.table`)。
"""
from _common import LANGS, name_of, table, write_localized

ITEM_TABLE = "ItemTable"


def main():
    names = {}
    for lang, lang_dir in LANGS.items():
        items = table(lang_dir, ITEM_TABLE)
        names[lang] = {item_id: name_of(row) for item_id, row in items.items()}
    keys = set().union(*[set(values) for values in names.values()])
    write_localized("ItemNames", names, list(keys))


if __name__ == "__main__":
    main()
