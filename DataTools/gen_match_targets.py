"""
マッチング先の名前テーブルを生成する。

  Data/Localization/TeamTargetNames.json
  Data/Localization/SeasonActNames.json

マッチング成立の通知の `MatchKeyInfo.matchTypeUuid` から、マッチング先の名前を引くのに使う。
番号がどの表の行かは `MatchKeyInfo.matchType` で決まる。

  パーティのマッチング   `TeamTargetTable` の行(名前は難易度まで含む)
  活動のマッチング       `SeasonActTable` の行

鍵はそれぞれの表の行(4言語ぶんの和)。2つの表は同じ番号が別のものを指すので、ファイルを分ける。
"""
from _common import LANGS, name_of, table, write_localized

OUTPUTS = (
    ("TeamTargetTable", "TeamTargetNames"),
    ("SeasonActTable", "SeasonActNames"),
)


def main():
    for source_table, basename in OUTPUTS:
        tables = {lang: table(lang_dir, source_table) for lang, lang_dir in LANGS.items()}

        keys = set()
        for lang, source in tables.items():
            keys |= set(source)
            print("%-6s %s %5d件" % (lang, source_table, len(source)))
        print("→ 4言語の和集合 %d件" % len(keys))

        values = {lang: {key: name_of(source.get(key)) for key in keys} for lang, source in tables.items()}
        write_localized(basename, values, keys)
        print()


if __name__ == "__main__":
    main()
