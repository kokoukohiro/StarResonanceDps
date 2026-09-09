"""
装備中スキル枠用のスキル名テーブルを生成する。

  Data/Localization/skills.{言語}.json

**収録するのは枠に置けるIDだけ。** メーターの行名は見出し表(recounts)が決めるので、
このテーブルに要るのはスキル枠ウィジェットが名前を引くIDだけ。

  イマジン  Icon に `skill_aoyi` を含む
  ロール    SlotPositionId に 21〜24

**イマジンは枠番号で判定しない。** 変身の解除スキルは `SlotPositionId` が空なのに、
変身中は実際にイマジン枠へ入る。枠ウィジェットはテーブルの枠指定を見ず、
実行時の枠の中身(`AttrSlot`)をそのまま出すので、枠指定は「名前が要るか」の
判断材料にならない。アイコンのほうが実態に合う。

ロールは枠番号のままでよい。アイコンで判定しても同じ集合になるが、意図が明確。

鍵は出所と言語の和集合。版差でどれか1つにしか無い枠でも落とさない。
"""
from _common import LANGS, name_of, table, write_localized

IMAGINE_ICON = "skill_aoyi"
ROLE_SLOTS = (21, 22, 23, 24)


def main():
    tables = {lang: table(lang_dir, "SkillTable") for lang, lang_dir in LANGS.items()}

    keys = set()
    for lang, skill_table in tables.items():
        imagine = {i for i, r in skill_table.items() if IMAGINE_ICON in (r.get("Icon") or "")}
        role = {i for i, r in skill_table.items()
                if any(s in (r.get("SlotPositionId") or []) for s in ROLE_SLOTS)}
        keys |= imagine | role
        print("%-6s イマジン(アイコン) %d / ロール(slot 21-24) %d" % (lang, len(imagine), len(role)))
    print("→ 和集合 %d" % len(keys))
    print("")

    values = {lang: {i: name_of(t.get(i)) for i in keys} for lang, t in tables.items()}
    write_localized("skills", values, keys)


if __name__ == "__main__":
    main()
