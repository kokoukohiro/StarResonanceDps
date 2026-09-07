"""
メーターの行の見出しを持つ表から、発生源ID → 行名と、行畳みマッピングを作る。

  Data/Localization/recount.{言語}.json  … 発生源ID → メーターの行名
  Data/Mappings/RecountSourceMap.json    … 発生源ID → 畳み先ID
  Data/Mappings/BuffNameAlias.json       … 見出し表に載るイマジンのパッシブへ絞る

各言語は自前のフォルダだけで完結させる。言語によってテーブルの版が違い、
行数も `DamageId` も一致しない。`Id` は単なる連番なので行の同一性の根拠にならない。
**言語をまたいで行を突き合わせない。**
"""
import collections
import os

from _common import DATA, LANGS, MAPPINGS, dump, load, table

# 総括行(その他)。抱えるIDが突出して多い行として特定し、この名前で検算する。
CATCHALL = {"zh-CN": "其他", "en-US": "Other", "ja-JP": "その他", "ko-KR": "기타"}

# 職ごとに1つずつ持つ見出しは、同名でも別物なので畳まない。
# 判別は「同じ名前がこの本数以上の行に出る」。
PER_CLASS_ROW_THRESHOLD = 3

# 訳が食い違うため自動判定には載らないが、実物では同じと分かっている組。
# 逆に、一部の言語だけ同名でも実物では別物のものは、ここに入れない。
CONFIRMED_SAME = {"149904": "1424"}


def build_rows(lang, lang_dir):
    """その言語の RecountTable から「ID → 行キー」と「行キー → IDの列」を作る。"""
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
    print("%-6s 行%3d / 鍵%5d / 総括行 key=%-4s %-6s ID%4d件 → 空欄"
          % (lang, len(recount), len(row_of), catchall, got, len(by_row[catchall])))

    # 総括行は行名を空にする。鍵の大半を占め、個別の名前を持たない。
    names = {i: ("" if k == catchall else (recount[k].get("RecountName") or "").strip())
             for i, k in row_of.items()}
    return names, by_row, catchall


def main():
    names, rows = {}, {}
    for lang, lang_dir in LANGS.items():
        names[lang], by_row, catchall = build_rows(lang, lang_dir)
        rows[lang] = (by_row, catchall)

    all_ids = sorted(set().union(*[set(v) for v in names.values()]), key=int)
    for lang in LANGS:
        # ここは write_localized を使わない。総括行の空欄は意図した状態で、
        # 「既存値を残す」規則を当てると消せなくなる。
        dump(os.path.join(DATA, "Localization", "recount.%s.json" % lang),
             {i: names[lang].get(i, "") for i in all_ids})

    # --- 行畳みマッピング ---------------------------------------------------
    alias_path = os.path.join(MAPPINGS, "BuffNameAlias.json")
    alias = load(alias_path)
    if alias is None:
        raise FileNotFoundError(
            "BuffNameAlias.json が要る。この表は自身を絞り込む形で維持しており、"
            "失うと元のイマジンのパッシブ一覧を作り直せない: " + alias_path)
    alias = set(alias)

    mapping, conflicts = {}, []
    # 同じ行に属するIDを、その行の最若IDへ寄せる。
    # 行の構成は zh-CN を権威にし、そこに無いIDだけ ja-JP で補う。
    for lang in ("zh-CN", "ja-JP"):
        by_row, catchall = rows[lang]
        for row_key, members in by_row.items():
            if row_key == catchall or len(members) < 2:
                continue
            lead = min(members, key=int)
            for member in members:
                if member == lead or member in alias:
                    continue  # パッシブは本体へ畳まない(分離表示のため)
                if member in mapping and mapping[member] != int(lead):
                    conflicts.append((member, mapping[member], lead))
                    continue
                if lang == "ja-JP" and member in names["zh-CN"]:
                    continue
                mapping[member] = int(lead)

    # パッシブ同士は畳む。同じ行にパッシブが2件あると、接尾辞を付けても同名の行が2つ出る。
    by_row, catchall = rows["zh-CN"]
    for row_key, members in by_row.items():
        if row_key == catchall:
            continue
        passives = sorted([m for m in members if m in alias], key=int)
        for member in passives[1:]:
            mapping[member] = int(passives[0])

    # --- 同名の別行も畳む ---------------------------------------------------
    # ゲーム内メーターは同名の行を2つ出さない。同名＝同じダメージソース。
    #
    #  (1) 全言語で名前が食い違わないときだけ畳む。一部の言語だけ同名になるのは
    #      訳が足りていないだけで別物のことがあり、畳むと表示言語で粒度が変わる。
    #      片方だけ名前が無いのは「食い違い」ではないので畳む。
    #  (2) 職ごとに1つずつ持つ見出し(PER_CLASS_ROW_THRESHOLD 行以上)は畳まない。
    #
    # 畳み先は「名前を持つ言語が最も多いID」。同数なら最若ID。単純に最若へ寄せると、
    # 名前の無いIDが畳み先になって行名が消えることがある。
    rest = [i for i in all_ids
            if i not in mapping and i not in alias
            and any((names[l].get(i) or "").strip() for l in LANGS)]
    candidates = {}
    for lang in LANGS:
        by_name = collections.defaultdict(list)
        for i in rest:
            value = (names[lang].get(i) or "").strip()
            if value:
                by_name[value].append(i)
        for group in by_name.values():
            if len(group) > 1:
                candidates[frozenset(group)] = None

    merged = 0
    for group in candidates:
        ids = sorted(group, key=int)
        if len(ids) >= PER_CLASS_ROW_THRESHOLD:
            continue
        if any(len({(names[l].get(i) or "").strip() for i in ids} - {""}) > 1 for l in LANGS):
            continue
        lead = max(ids, key=lambda i: (sum(1 for l in LANGS if (names[l].get(i) or "").strip()),
                                       -int(i)))
        for i in ids:
            if i != lead:
                mapping[i] = int(lead)
                merged += 1

    for src, dst in CONFIRMED_SAME.items():
        if src not in all_ids or dst not in all_ids:
            raise AssertionError("CONFIRMED_SAME のIDが見出し表に無い: %s → %s" % (src, dst))
        mapping[src] = int(dst)

    # 行畳みの結果がさらに同名畳みの対象になり、2段になることがある。
    # 実行時は1ホップしか引かないので、ここで連鎖を潰す。
    for src in list(mapping):
        seen, dst = {src}, mapping[src]
        while str(dst) in mapping and str(dst) not in seen:
            seen.add(str(dst))
            dst = mapping[str(dst)]
        mapping[src] = dst
    if any(str(v) in mapping for v in mapping.values()):
        raise AssertionError("畳み先がまだ畳まれている")

    dump(os.path.join(MAPPINGS, "RecountSourceMap.json"),
         {i: mapping[i] for i in sorted(mapping, key=int)})
    dump(alias_path, {i: {"Suffix": "passive"} for i in sorted(alias & set(all_ids), key=int)})

    print("\n同名の別行を畳んだ %d件 / ゲーム内確認 %d件" % (merged, len(CONFIRMED_SAME)))
    print("RecountSourceMap %d件 / 言語間の行構成の食い違い %d件 %s"
          % (len(mapping), len(conflicts), conflicts[:4]))
    print("BuffNameAlias %d件" % len(alias & set(all_ids)))
    print("recount 鍵 %d件" % len(all_ids))


if __name__ == "__main__":
    main()
