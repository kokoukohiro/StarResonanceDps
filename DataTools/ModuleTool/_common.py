# DataTools(モジュールツール用)共通。パスと入出力、データソースのマージ、名前の判定と取り出しを持つ。
#
# 各ツールは単体で実行できる。作業ディレクトリはどこでもよい
# (このファイルの位置からリポジトリを求める)。
import io
import json
import os

# …/StarResonanceDps/DataTools/ModuleTool/_common.py → …/StarResonanceDps
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

# 出力の置き場。プラグインのDLLに埋め込む。
DATA = os.path.join(SOLUTION, "StarResonanceDps.Plugins.ModuleTool", "Data")

# 表示言語 → 入力側のフォルダ名。
# 言語ごとにテーブルの版が違うことがあり、収録IDも一致しない。
LANGS = {"zh-CN": "cn", "en-US": "en", "ja-JP": "jp", "ko-KR": "kr"}

# 未翻訳の行に入っているプレースホルダ。名前として扱わない。
PLACEHOLDERS = {"场地标记01", "气刃突刺计数"}


def sources_for(lang_dir):
    """その言語フォルダで使うデータソースの順。先にあるほうがベース。"""
    return SOURCES_BY_LANG_DIR[lang_dir]


def load(path):
    """JSONを読む。無ければNone。"""
    if not os.path.exists(path):
        return None
    with io.open(path, encoding="utf-8") as fh:
        return json.load(fh)


def _path(source, lang_dir, name):
    return os.path.join(TABLES_DIR, source, "Ztable", lang_dir, "ZTable", name + ".json")


def table_of_source(source, lang_dir, name):
    """データソースを指定して生テーブルを読む。無ければNone。"""
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

    **キーがIDそのもののテーブル専用。**
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


def dump(name, obj):
    """`DATA`の`{name}.json`を書く。差分が見やすいよう整形して改行で終える。"""
    path = os.path.join(DATA, name + ".json")
    os.makedirs(DATA, exist_ok=True)
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
