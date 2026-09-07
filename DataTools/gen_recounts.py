"""
メーターの行の見出しを持つ表から、発生源ID → 行名を作る。

  Data/Localization/recounts.{言語}.json

各言語は自前のフォルダだけで完結させる。言語によってテーブルの版が違い、
行数も `DamageId` も一致しない。`Id` は単なる連番なので行の同一性の根拠にならない。
**言語をまたいで行を突き合わせない。**

畳み込み(`Data/Mappings/RecountSourceMap.json`)と手修正(`Data/Overrides/RecountOverrides.json`)は
**手動編集のファイル**で、このツールは読みも書きもしない。
"""
import collections
import os

from _common import DATA, LANGS, dump, table

# 総括行(その他)。抱えるIDが突出して多い行として特定し、この名前で検算する。
CATCHALL = {"zh-CN": "其他", "en-US": "Other", "ja-JP": "その他", "ko-KR": "기타"}


def build_names(lang, lang_dir):
    """その言語の表から「発生源ID → 行名」を作る。総括行の行名は空にする。"""
    recount = table(lang_dir, "RecountTable")
    damage_attr = table(lang_dir, "DamageAttrTable")

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

    catchall = max(by_row, key=lambda k: len(by_row[k]))
    got = (recount[catchall].get("RecountName") or "").strip()
    if got != CATCHALL[lang]:
        raise AssertionError(
            "総括行の検算に失敗 %s: key=%s name=%r 抱えるID=%d"
            % (lang, catchall, got, len(by_row[catchall])))
    print("%-6s 行%3d / 鍵%5d / 総括行 key=%-4s %-6s ID%4d → 空欄"
          % (lang, len(recount), len(row_of), catchall, got, len(by_row[catchall])))

    # 総括行は行名を空にする。鍵の大半を占め、個別の名前を持たない。
    return {i: ("" if k == catchall else (recount[k].get("RecountName") or "").strip())
            for i, k in row_of.items()}


def main():
    names = {lang: build_names(lang, lang_dir) for lang, lang_dir in LANGS.items()}
    all_ids = sorted(set().union(*[set(v) for v in names.values()]), key=int)

    # ここは write_localized を使わない。総括行の空欄は意図した状態で、
    # 「既存値を残す」規則を当てると消せなくなる。
    for lang in LANGS:
        dump(os.path.join(DATA, "Localization", "recounts.%s.json" % lang),
             {i: names[lang].get(i, "") for i in all_ids})

    print("\nrecounts 鍵 %d" % len(all_ids))


if __name__ == "__main__":
    main()
