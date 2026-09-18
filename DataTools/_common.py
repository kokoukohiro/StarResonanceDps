# DataTools 共通。パスと入出力だけを持つ。
#
# 各ツールは単体で実行できる。作業ディレクトリはどこでもよい
# (このファイルの位置からリポジトリを求める)。
import io
import json
import os

# …/StarResonanceDps/DataTools/_common.py → …/StarResonanceDps
SOLUTION = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

# 入力の置き場。環境変数があればそれを使い、無ければリポジトリの隣の JSONS を見る。
# 中身は「出所 → Ztable → 言語フォルダ → ZTable → *.json」。
# リポジトリには含まれないので各自で用意する。
TABLES_ENV = "BPSR_TABLES"
TABLES_DIR = os.environ.get(TABLES_ENV) or os.path.join(os.path.dirname(SOLUTION), "JSONS")

# 出所。**言語フォルダごとに土台が違う**。先にあるほうが土台で、後ろは空欄を補うだけ。
# cn / en は Star、jp / kr は StarASIA が土台(その言語が入っているビルドの表)。
SOURCES = ("StarASIA", "Star")
SOURCES_BY_LANG_DIR = {
    "cn": ("Star", "StarASIA"),
    "en": ("Star", "StarASIA"),
    "jp": ("StarASIA", "Star"),
    "kr": ("StarASIA", "Star"),
}


def sources_for(lang_dir):
    """その言語フォルダで使う出所の順。先にあるほうが土台。"""
    return SOURCES_BY_LANG_DIR[lang_dir]


def unk_dir(source):
    """出所ごとの入力 `Unk`。アドレス一覧が入る。"""
    return os.path.join(TABLES_DIR, source, "Unk")


def bundles_dir(source):
    """出所ごとの入力 `Bundles`(`<番号>.ab`)。同じ出所のアドレス一覧と対になる。"""
    return os.path.join(TABLES_DIR, source, "Bundles")

DATA = os.path.join(SOLUTION, "StarResonanceDps.Core", "Data")
LOCALIZATION = os.path.join(DATA, "Localization")
GENERATED = os.path.join(DATA, "Generated")

# 表示言語 → 入力側のフォルダ名。
# 言語ごとにテーブルの版が違うことがあり、収録IDも一致しない。
LANGS = {"zh-CN": "cn", "en-US": "en", "ja-JP": "jp", "ko-KR": "kr"}

# 未翻訳の行に入っている埋め草。名前として扱わない。
PLACEHOLDERS = {"场地标记01", "气刃突刺计数"}


def load(path):
    """JSON を読む。無ければ None。"""
    if not os.path.exists(path):
        return None
    with io.open(path, encoding="utf-8") as fh:
        return json.load(fh)


def _path(source, lang_dir, name):
    return os.path.join(TABLES_DIR, source, "Ztable", lang_dir, "ZTable", name + ".json")


def table_of_source(source, lang_dir, name):
    """
    出所を名指しで生テーブルを読む。無ければ None。

    **行が実IDで引けないテーブル用。** `RecountTable` の鍵は行番号で、出所が違えば
    同じ番号が別の行を指す。合併すると
    無関係な行が混ざるので、出所ごとに組み立ててから行の対応を取る。
    """
    return load(_path(source, lang_dir, name))


def tables_by_source(lang_dir, name):
    """
    出所ごとの生テーブルを `sources_for(lang_dir)` の順で返す。`table` の下請け。

    どこにも無ければ、置き場の設定方法を示して止まる。
    """
    order = sources_for(lang_dir)
    found = [(s, load(_path(s, lang_dir, name))) for s in order]
    found = [(s, t) for s, t in found if t is not None]
    if not found:
        raise FileNotFoundError(
            "%s が %s のどこにも無い。入力の置き場は環境変数 %s か、_common.py の TABLES_DIR で設定する"
            % (name, " / ".join(order), TABLES_ENV))
    return found


def table(lang_dir, name):
    """
    出所をまたいで合併した入力テーブルを読む。

    鍵は和集合。**行は先の出所が勝ち**(順は言語フォルダごとに `sources_for`)、後ろは無い行を足すだけ。
    そのうえで、採った行の**空欄の文字列項目**だけを別の出所で補う。

    **鍵が実IDのテーブル専用。** 行番号で引くものは `table_of_source` を使う。
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
    """JSON を書く。差分が見やすいよう整形して改行で終える。"""
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with io.open(path, "w", encoding="utf-8") as fh:
        fh.write(json.dumps(obj, ensure_ascii=False, indent=2) + "\n")


def named(value):
    """名前として使える文字列か。空と埋め草は名前ではない。"""
    text = (value or "").strip()
    return bool(text) and text not in PLACEHOLDERS


def name_of(row, field="Name"):
    """入力の1行から名前を取る。埋め草は空にする。"""
    text = ((row or {}).get(field) or "").strip()
    return "" if text in PLACEHOLDERS else text


def per_lang(values_by_lang, key):
    """1つの鍵の4言語ぶんの名前。`{ "zh-CN": ..., "en-US": ..., "ja-JP": ..., "ko-KR": ... }` の順で、無い言語は空文字。"""
    return {lang: (values_by_lang.get(lang, {}).get(key) or "").strip() for lang in LANGS}


def write_localized(basename, values_by_lang, keys, sort_key=int):
    """
    `{basename}.json` を書く。形は `{ "鍵": { "zh-CN": ..., "en-US": ..., "ja-JP": ..., "ko-KR": ... } }`。

    どの鍵も4言語をすべて持つ。版差で収録IDが違っても、鍵集合が言語で変わると
    畳み込みの停止条件が言語で変わってしまうため。足りない言語は空文字で入り、
    表示時に zh-CN へ落ちるのに任せる。

    **完全な上書き。** 出力は入力だけで決まる。前の出力は読まない。
    入力に名前が無ければ空になる。

    `sort_key` は鍵の並べ方。鍵が数字だけでないテーブルで渡す。
    """
    keys = sorted(keys, key=sort_key)
    merged = {key: per_lang(values_by_lang, key) for key in keys}
    dump(os.path.join(LOCALIZATION, "%s.json" % basename), merged)
    print("  %s %5d鍵 / 名前あり %s"
          % (basename, len(keys),
             " / ".join("%s %d" % (lang, sum(1 for names in merged.values() if names[lang])) for lang in LANGS)))
