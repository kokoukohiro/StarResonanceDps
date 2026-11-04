# DataTools共通。パスと入出力、データソースのマージ、名前の判定と取り出し、上書きファイルへの名前の書き足しを持つ。
#
# 各ツールは単体で実行できる。作業ディレクトリはどこでもよい
# (このファイルの位置からリポジトリを求める)。
import io
import json
import os

# …/StarResonanceDps/DataTools/Core/_common.py → …/StarResonanceDps
SOLUTION = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

# 入力の置き場。環境変数があればそれを使い、無ければリポジトリのフォルダと同じ階層のJSONSを見る。
# 中身は「データソース → Ztable → 言語フォルダ → ZTable → *.json」。
# リポジトリには含まれないので各自で用意する。
TABLES_ENV = "BPSR_TABLES"
TABLES_DIR = os.environ.get(TABLES_ENV) or os.path.join(os.path.dirname(SOLUTION), "JSONS")

# データソース。**言語フォルダごとにベースが違う**。先にあるほうがベースで、後ろは先に無い行を足し、空欄を補うだけ。
# cn / enはStar、jp / krはStarASIAがベース(その言語が入っているビルドの表)。
SOURCES = ("StarASIA", "Star")
SOURCES_BY_LANG_DIR = {
    "cn": ("Star", "StarASIA"),
    "en": ("Star", "StarASIA"),
    "jp": ("StarASIA", "Star"),
    "kr": ("StarASIA", "Star"),
}


def sources_for(lang_dir):
    """その言語フォルダで使うデータソースの順。先にあるほうがベース。"""
    return SOURCES_BY_LANG_DIR[lang_dir]


def unk_dir(source):
    """データソースごとの入力`Unk`。アドレス一覧が入る。"""
    return os.path.join(TABLES_DIR, source, "Unk")


def bundles_dir(source):
    """データソースごとの入力`Bundles`(`<番号>.ab`)。同じデータソースのアドレス一覧と対になる。"""
    return os.path.join(TABLES_DIR, source, "Bundles")

DATA = os.path.join(SOLUTION, "StarResonanceDps.Core", "Data")
LOCALIZATION = os.path.join(DATA, "Localization")
GENERATED = os.path.join(DATA, "Generated")
OVERRIDES = os.path.join(DATA, "Overrides")

# 表示言語 → 入力側のフォルダ名。
# 言語ごとにテーブルの版が違うことがあり、収録IDも一致しない。
LANGS = {"zh-CN": "cn", "en-US": "en", "ja-JP": "jp", "ko-KR": "kr"}

# 未翻訳の行に入っているプレースホルダ。名前として扱わない。
PLACEHOLDERS = {"场地标记01", "气刃突刺计数"}


def load(path):
    """JSONを読む。無ければNone。"""
    if not os.path.exists(path):
        return None
    with io.open(path, encoding="utf-8") as fh:
        return json.load(fh)


def _path(source, lang_dir, name):
    return os.path.join(TABLES_DIR, source, "Ztable", lang_dir, "ZTable", name + ".json")


def table_of_source(source, lang_dir, name):
    """
    データソースを指定して生テーブルを読む。無ければNone。

    **行をIDそのもので引けないテーブル用。** `RecountTable`のキーは行番号で、データソースが違えば
    同じ番号が別の行を指す。マージすると
    無関係な行が混ざるので、データソースごとに組み立ててから行の対応を取る。
    """
    return load(_path(source, lang_dir, name))


def tables_by_source(lang_dir, name):
    """
    データソースごとの生テーブルを`sources_for(lang_dir)`の順で返す。`table`もこれで読む。

    どこにも無ければ、置き場の設定方法を示して止まる。
    """
    order = sources_for(lang_dir)
    found = [(s, load(_path(s, lang_dir, name))) for s in order]
    found = [(s, t) for s, t in found if t is not None]
    if not found:
        raise FileNotFoundError(
            "%sが%sのどこにも無い。入力の置き場は環境変数%sか、_common.pyのTABLES_DIRで設定する"
            % (name, " / ".join(order), TABLES_ENV))
    return found


def table(lang_dir, name):
    """
    データソースをまたいでマージした入力テーブルを読む。

    キーは和集合。**行は先のデータソースを優先し**(順は言語フォルダごとに`sources_for`)、後ろは無い行を足すだけ。
    そのうえで、採った行の**空欄の文字列項目**だけを別のデータソースで補う。

    **キーがIDそのもののテーブル専用。**行番号で引くものは`table_of_source`を使う。
    """
    found = tables_by_source(lang_dir, name)
    merged = {}
    for key in set().union(*[set(t) for _, t in found]):
        rows = [t[key] for _, t in found if key in t]
        row = dict(rows[0])
        for other in rows[1:]:
            for field, value in other.items():
                if isinstance(value, str) and value.strip() and not (row.get(field) or "").strip():
                    row[field] = value
        merged[key] = row
    return merged


def dump(path, obj):
    """JSONを書く。差分が見やすいよう整形して改行で終える。"""
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with io.open(path, "w", encoding="utf-8") as fh:
        fh.write(json.dumps(obj, ensure_ascii=False, indent=2) + "\n")


def named(value):
    """名前として使える文字列か。空とプレースホルダは名前ではない。"""
    text = (value or "").strip()
    return bool(text) and text not in PLACEHOLDERS


def name_of(row, field="Name"):
    """入力の1行から名前を取る。プレースホルダは空にする。"""
    text = ((row or {}).get(field) or "").strip()
    return "" if text in PLACEHOLDERS else text


def per_lang(values_by_lang, key):
    """1つのキーの4言語ぶんの名前。`{ "zh-CN": ..., "en-US": ..., "ja-JP": ..., "ko-KR": ... }`の順で、無い言語は空文字。"""
    return {lang: (values_by_lang.get(lang, {}).get(key) or "").strip() for lang in LANGS}


def write_localized(basename, values_by_lang, keys, sort_key=int, dropped_names_overrides=None):
    """
    `{basename}.json`を書く。形は`{ "キー": { "zh-CN": ..., "en-US": ..., "ja-JP": ..., "ko-KR": ... } }`。

    どのキーも4言語をすべて持つ。版によって収録IDが違っても、キーの集合が言語で変わると
    まとめる処理の停止条件が言語で変わってしまうため。足りない言語は空文字で入り、
    表示時にzh-CNにフォールバックするのに任せる。

    **完全な上書き。**出力は入力だけで決まる。入力に名前が無ければ空になる。
    前の出力を読むのは、`dropped_names_overrides`(上書きファイルの名前)を渡したときに、
    空になった名前をその上書きへ書き足すためだけ(→ `carry_dropped_names`)。

    `sort_key`はキーの並べ方。キーが数字だけでないテーブルで渡す。
    """
    keys = sorted(keys, key=sort_key)
    merged = {key: per_lang(values_by_lang, key) for key in keys}
    path = os.path.join(LOCALIZATION, "%s.json" % basename)
    if dropped_names_overrides:
        carry_dropped_names(load(path), merged, dropped_names_overrides, sort_key)
    dump(path, merged)
    print("  %s %5dキー / 名前あり%s"
          % (basename, len(keys),
             " / ".join("%s %d" % (lang, sum(1 for names in merged.values() if names[lang])) for lang in LANGS)))


def carry_dropped_names(previous, merged, override_basename, sort_key=int):
    """
    前の出力`previous`にあって今回の`merged`で空になった名前を、
    上書きファイル`Data/Overrides/{override_basename}.json`へ書き足す。比べるのはキーと言語ごと。

    入力の表の更新で名前が消された行の名前を残すため(名前の上書きファイルの役割と同じ)。

    - 上書きにキーが無ければ、空になった言語だけを持つ項目を足す。空になっていない言語は書かない
      (上書きの空文字は生成値を消す意味になるため)
    - キーがあれば、空になった言語だけ前の名前で上書きする。ほかの言語はそのまま
    - 今回の入力に行ごと無いキーは書き足さない(上書きは行が残って名前だけ消えたものを戻すもの)。キーを表示する

    名前の表より先に呼ぶ。上書きファイルが無い・形が違うときは止まり、名前の表も書かれない。
    前の出力が無ければ何もしない。
    """
    if previous is None:
        print("  %s: 前の出力が無いので書き足さない" % override_basename)
        return

    path = os.path.join(OVERRIDES, "%s.json" % override_basename)
    overrides = load(path)
    if overrides is None:
        raise FileNotFoundError("%sが無い。空になった名前の書き足し先" % path)

    added = []
    replaced = []
    removed_rows = []
    for key in sorted(previous, key=sort_key):
        old_names = previous[key]
        dropped = {lang: old_names[lang].strip() for lang in LANGS
                   if (old_names.get(lang) or "").strip() and not ((merged.get(key) or {}).get(lang) or "").strip()}
        if not dropped:
            continue
        if key not in merged:
            removed_rows.append(key)
            continue

        entry = overrides.get(key)
        if entry is None:
            overrides[key] = {"Name": {lang: dropped[lang] for lang in LANGS if lang in dropped}}
            added.extend((key, lang, dropped[lang]) for lang in LANGS if lang in dropped)
            continue
        names = entry.get("Name")
        if not isinstance(names, dict):
            raise ValueError("%sのキー%sにNameが無い" % (path, key))
        for lang in LANGS:
            if lang in dropped:
                replaced.append((key, lang, names.get(lang), dropped[lang]))
        combined = dict(names)
        combined.update(dropped)
        entry["Name"] = {lang: combined[lang] for lang in LANGS if lang in combined}

    if added or replaced:
        dump(path, {key: overrides[key] for key in sorted(overrides, key=sort_key)})
    print("  %s: 空になった名前を書き足した キー%d(新しく足した%d / 上書きした%d)"
          % (override_basename, len({key for key, *_ in added + replaced}),
             len({key for key, *_ in added}), len({key for key, *_ in replaced})))
    for key, lang, name in added:
        print("    %s %-6s %s" % (key, lang, name))
    for key, lang, before, name in replaced:
        print("    %s %-6s %s → %s(上書き)" % (key, lang, before if before is not None else "(無し)", name))
    if removed_rows:
        print("  %s: 行ごと消えたキー(書き足さない) %d: %s"
              % (override_basename, len(removed_rows), ", ".join(removed_rows)))
