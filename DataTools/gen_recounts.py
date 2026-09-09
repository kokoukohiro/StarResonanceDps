"""
メーターの行の見出しを持つ表から、発生源ID → 行名を作る。

  Data/Localization/recounts.{言語}.json

**出所も言語も、それぞれ自前のフォルダだけで完結させる。**
`RecountTable` の鍵は行番号なので、版が違えば同じ番号が別の行を指す。
`Id` は単なる連番で行の同一性の根拠にならない。合併してよいのは
「発生源ID → 行名」を作り終えたあとだけ。

畳み込み(`Data/Mappings/RecountSourceMap.json`)と手修正(`Data/Overrides/RecountOverrides.json`)は
**手動編集のファイル**で、このツールは読みも書きもしない。
"""
import collections
import os

from _common import DATA, LANGS, SOURCES, dump, name_of, tables_by_source

# 総括行(その他)。抱えるIDが突出して多い行として特定し、この名前で検算する。
CATCHALL = {"zh-CN": "其他", "en-US": "Other", "ja-JP": "その他", "ko-KR": "기타"}


def build_names(lang, lang_dir, source, recount, damage_attr):
    """1つの出所の表から「発生源ID → 行名」を作る。総括行の行名は空にする。"""
    by_row = collections.defaultdict(list)
    row_of = {}
    for row_key in sorted(recount, key=int):
        for damage_id in recount[row_key].get("DamageId") or []:
            attr = damage_attr.get(str(damage_id))
            if not attr:
                continue
            # TypeEnum を「生のまま」鍵にする。親スキルへ畳むと弾IDが鍵から消え、
            # 別途「弾ID規則」が要る。生のまま入れれば弾IDも直接載る。
            type_enum = str(attr.get("TypeEnum"))
            if not type_enum or type_enum == "None" or type_enum in row_of:
                continue
            row_of[type_enum] = row_key
            by_row[row_key].append(type_enum)

    # 総括行を「抱えるIDが突出して多い行」として特定し、名前で検算する。
    #
    # **落ちたらその出所×言語の名前列は使えない。** 行の中身(`DamageId`)は正しくても
    # 名前列だけが詰まっていることがあり(実測: 366行に294件ぶんが詰まっている)、
    # そのまま採るとIDと名前の対応が丸ごとずれる。名前は捨て、鍵も他の出所に任せる。
    catchall = max(by_row, key=lambda k: len(by_row[k]))
    got = name_of(recount[catchall], "RecountName")
    named = sum(1 for k in recount if name_of(recount[k], "RecountName"))
    if got != CATCHALL[lang]:
        print("  ★%-8s %-6s 総括行の検算に失敗 key=%s name=%r (行%d / 名前あり%d) → この出所の名前は使わない"
              % (source, lang, catchall, got, len(recount), named))
        return None

    print("  %-9s %-6s 行%3d / 鍵%5d / 総括行 key=%-4s %-6s ID%4d → 空欄"
          % (source, lang, len(recount), len(row_of), catchall, got, len(by_row[catchall])))

    # 総括行は行名を空にする。鍵の大半を占め、個別の名前を持たない。
    return {i: ("" if k == catchall else name_of(recount[k], "RecountName"))
            for i, k in row_of.items()}


def main():
    names = {}
    for lang, lang_dir in LANGS.items():
        recounts = dict(tables_by_source(lang_dir, "RecountTable"))
        attrs = dict(tables_by_source(lang_dir, "DamageAttrTable"))
        merged = {}
        # 出所ごとに独立して作り、あとから重ねる。
        # **先の出所が持つ鍵は上書きしない。** 総括行の空欄は意図した状態なので、
        # 「空だから補う」を当てると後ろの出所が埋め戻してしまう。
        for source in SOURCES:
            if source not in recounts or source not in attrs:
                continue
            built = build_names(lang, lang_dir, source, recounts[source], attrs[source])
            if built is None:
                continue
            for key, value in built.items():
                merged.setdefault(key, value)
        names[lang] = merged

    all_ids = sorted(set().union(*[set(v) for v in names.values()]), key=int)

    # ここは write_localized を使わない。総括行の空欄は意図した状態で、
    # 「既存値を残す」規則を当てると消せなくなる。
    for lang in LANGS:
        dump(os.path.join(DATA, "Localization", "recounts.%s.json" % lang),
             {i: names[lang].get(i, "") for i in all_ids})

    print("\nrecounts 鍵 %d" % len(all_ids))


if __name__ == "__main__":
    main()
