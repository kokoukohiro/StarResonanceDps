"""
モジュール効果の名前テーブルを生成する。

  Data/Localization/ModEffectNames.json

鍵はモジュール効果の番号(`ModEffectTable.EffectID`)。値は `EffectName`。
`ModEffectTable` の行は効果の段階ごとに分かれる(同じ `EffectID` に段階ごとの行がある)ので、
鍵は行ではなく `EffectID` にする。

バフ/デバフリストで名前の無いバフの付与元をたどり、モジュールの根のバフ
(`EffectConfig` の `[3, バフID, 1]`)に着いたとき、アプリはこの名前を出す。

同じ `EffectID` で段階によって名前が食い違う言語は、段階の一番小さい、名前のある行の名前を採り、一覧を出す。
空にも除外にもしない。
"""
from _common import LANGS, name_of, table, write_localized


def main():
    keys = set()
    values = {}
    for lang, lang_dir in LANGS.items():
        rows = table(lang_dir, "ModEffectTable")
        names = {}
        conflicts = {}
        for row in sorted(rows.values(), key=lambda r: (r["EffectID"], r.get("Level") or 0, r["Id"])):
            effect_id = row["EffectID"]
            name = name_of(row, "EffectName")
            keys.add(effect_id)
            if not names.get(effect_id):
                names[effect_id] = name
            elif name and name != names[effect_id]:
                conflicts.setdefault(effect_id, set()).add(name)

        values[lang] = names
        print("%-6s ModEffectTable %4d行 / 効果 %d" % (lang, len(rows), len(names)))
        for effect_id, others in sorted(conflicts.items()):
            print("  %s 効果 %d の名前が段階で食い違う: 採った「%s」 / ほか %s"
                  % (lang, effect_id, names[effect_id], " / ".join("「%s」" % other for other in sorted(others))))

    print("→ 4言語の和集合 %d件" % len(keys))
    write_localized("ModEffectNames", values, keys)


if __name__ == "__main__":
    main()
