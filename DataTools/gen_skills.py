"""
スキル名テーブルを生成する。

  Data/Localization/SkillNames.json

**`SkillTable` の全行を収録する。** 使い道を決めて絞らない。
スキル枠(イマジン・ロール)に加えて、モンスターの技など枠に置かれないスキルも名前を引ける。

鍵は出所と言語の和集合。版差でどれか1つにしか無い行でも落とさない。

未翻訳の行は埋め草(`_common.PLACEHOLDERS`)が入っているので空にする。
ただし埋め草と同じ文字列を本物の名前として持つ行がある(`GENUINE_PLACEHOLDER_NAME_IDS`)。
"""
from _common import LANGS, name_of, table, write_localized

# 埋め草と同じ文字列が本物の名前になっている行。
# 1101 は連番の「フィールドマーク」01〜06 の先頭で、他言語には訳語が入っている。
# 埋め草として空にすると zh-CN だけ名前が消え、表示時の落とし先も失う。
GENUINE_PLACEHOLDER_NAME_IDS = {"1101"}


def skill_name(skill_id, row):
    if skill_id in GENUINE_PLACEHOLDER_NAME_IDS:
        return ((row or {}).get("Name") or "").strip()
    return name_of(row)


def main():
    tables = {lang: table(lang_dir, "SkillTable") for lang, lang_dir in LANGS.items()}

    keys = set()
    for lang, skill_table in tables.items():
        keys |= set(skill_table)
        print("%-6s 行 %d" % (lang, len(skill_table)))
    print("→ 和集合 %d" % len(keys))
    print("")

    values = {lang: {i: skill_name(i, t.get(i)) for i in keys} for lang, t in tables.items()}
    write_localized("SkillNames", values, keys)


if __name__ == "__main__":
    main()
