"""
装備中スキル枠用のスキル名テーブルを生成する。

  Data/Localization/skills.{言語}.json

**収録するのは枠に置けるIDだけ。** メーターの行名は見出し表(recount)が決めるので、
このテーブルに要るのはスキル枠ウィジェットが名前を引くIDだけ。
枠ウィジェットはイマジンとロールしか表示しない
(`ResolvePlayerSkillLevels` も `CreateSelfActionBarLoadout` もクラススキルを通す前に落とす)。

  イマジン  Skill.IsImagineSlot() = SlotPositionId に 7 か 8
  ロール    Skill.IsRoleSlot()    = SlotPositionId に 21〜24

**判定はこの枠番号だけで行う。** ID帯で絞ると、同じ枠に置けるのに範囲から外れるIDが
枠に名無しで出る。

枠の判定は同梱の `Data/SkillTable.json` で行う。アプリの実行時判定と同じ表を使うため。
名前だけを各言語の入力から取る。
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
