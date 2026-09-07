"""
装備中スキル枠用のスキル名テーブルを生成する。

  Data/Localization/skills.{言語}.json

**収録するのは枠に置けるIDだけ。** メーターの行名は見出し表(recount)が決めるので、
このテーブルに要るのはスキル枠ウィジェットが名前を引くIDだけ。
枠ウィジェットはイマジンとロールしか表示しない
(`ResolvePlayerSkillLevels` も `CreateSelfActionBarLoadout` もクラススキルを通す前に落とす)。

  イマジン  Skill.IsImagineSlot() = SlotPositionId に 7 か 8   → 141件
  ロール    Skill.IsRoleSlot()    = SlotPositionId に 21〜24   →  20件
                                    (職務専用12 + 全職務共通 3021〜3028 の8)

イマジンは `39xx` 帯の95件だけではない。`2350` `3000` `1002xxx` `2900xxx` 等も同じ枠判定に
入るので、落とすと枠に名無しで出る。

枠の判定は<b>同梱の `Data/SkillTable.json`</b>(cn の生ファイル)で行う。アプリの実行時判定と
同じ表を使うため。名前だけを各言語の unpack から取る。
"""
import os

from _common import DATA, LANGS, load, name_of, table, write_localized

IMAGINE_SLOTS = (7, 8)
ROLE_SLOTS = (21, 22, 23, 24)


def main():
    skill_table = load(os.path.join(DATA, "SkillTable.json"))
    if skill_table is None:
        raise FileNotFoundError(os.path.join(DATA, "SkillTable.json"))

    def slots(row):
        return row.get("SlotPositionId") or []

    imagine = {i for i, row in skill_table.items() if any(s in slots(row) for s in IMAGINE_SLOTS)}
    role = {i for i, row in skill_table.items() if any(s in slots(row) for s in ROLE_SLOTS)}
    keys = imagine | role
    print("イマジン(slot 7/8) %d件 / ロール(slot 21-24) %d件 → 鍵 %d件"
          % (len(imagine), len(role), len(keys)))

    tables = {lang: table(lang_dir, "SkillTable") for lang, lang_dir in LANGS.items()}
    values = {lang: {i: name_of(t.get(i)) for i in keys} for lang, t in tables.items()}
    print("")
    write_localized("skills", values, keys)


if __name__ == "__main__":
    main()
