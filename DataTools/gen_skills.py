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

枠の判定は言語ごとに行い、鍵はその和集合。版差でどれか1言語にしか無い枠でも落とさない。
"""
from _common import LANGS, name_of, table, write_localized

IMAGINE_SLOTS = (7, 8)
ROLE_SLOTS = (21, 22, 23, 24)


def main():
    tables = {lang: table(lang_dir, "SkillTable") for lang, lang_dir in LANGS.items()}

    def slots(row):
        return row.get("SlotPositionId") or []

    keys = set()
    for lang, skill_table in tables.items():
        imagine = {i for i, r in skill_table.items() if any(s in slots(r) for s in IMAGINE_SLOTS)}
        role = {i for i, r in skill_table.items() if any(s in slots(r) for s in ROLE_SLOTS)}
        keys |= imagine | role
        print("%-6s イマジン(slot 7/8) %d / ロール(slot 21-24) %d" % (lang, len(imagine), len(role)))
    print("→ 4言語の和集合 %d" % len(keys))
    print("")

    values = {lang: {i: name_of(t.get(i)) for i in keys} for lang, t in tables.items()}
    write_localized("skills", values, keys)


if __name__ == "__main__":
    main()
