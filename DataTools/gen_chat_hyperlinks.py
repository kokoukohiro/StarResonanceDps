"""
チャットのリンクの発言の文の型と、ギルドの建物の名前のテーブルを生成する。

  Data/Localization/ChatHyperlinks.json
  Data/Localization/UnionBuildingNames.json

ChatHyperlinks: 鍵はリンクの番号(`ChatHyperlink` の全行、4言語ぶんの和)。値は `Content`(文の型)。型の空の鍵も残す。
型の中の `{*…*}` は差し込みの値、`<u>` `<link=1>` `<style=…>` などは書式で、アプリが値を埋めて書式を外す。

UnionBuildingNames: 鍵はギルドの建物の番号(`UnionBuildingTable` の全行、4言語ぶんの和)。値は `BuildingName`。
リンクの文の `{*union.build*}` に入る。

型と名前の出所の土台は言語フォルダごと(`_common.table`)。
"""
from _common import LANGS, name_of, table, write_localized

HYPERLINK_TABLE = "ChatHyperlink"
UNION_BUILDING_TABLE = "UnionBuildingTable"


def column_by_lang(table_name, field):
    """表の1つの列を言語ごとに読む。返すのは「言語 → 鍵 → 値」と、4言語ぶんの鍵の和。"""
    values = {}
    for lang, lang_dir in LANGS.items():
        rows = table(lang_dir, table_name)
        values[lang] = {key: name_of(row, field) for key, row in rows.items()}
    keys = set().union(*[set(column) for column in values.values()])
    return values, keys


def main():
    contents, content_keys = column_by_lang(HYPERLINK_TABLE, "Content")
    write_localized("ChatHyperlinks", contents, list(content_keys))
    names, name_keys = column_by_lang(UNION_BUILDING_TABLE, "BuildingName")
    write_localized("UnionBuildingNames", names, list(name_keys))


if __name__ == "__main__":
    main()
