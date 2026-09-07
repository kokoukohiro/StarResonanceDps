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
# 中身は「言語フォルダ → ZTable → *.json」。リポジトリには含まれないので各自で用意する。
TABLES_ENV = "BPSR_TABLES"
UNPACK = os.environ.get(TABLES_ENV) or os.path.join(os.path.dirname(SOLUTION), "JSONS")

DATA = os.path.join(SOLUTION, "StarResonanceDps.Core", "Data")
LOCALIZATION = os.path.join(DATA, "Localization")
MAPPINGS = os.path.join(DATA, "Mappings")

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


def table(lang_dir, name):
    """入力テーブルを読む。無ければ、どこを設定すればよいか示して止まる。"""
    path = os.path.join(UNPACK, lang_dir, "ZTable", name + ".json")
    data = load(path)
    if data is None:
        raise FileNotFoundError(
            "%s が無い。入力の置き場は環境変数 %s か、_common.py の UNPACK で設定する"
            % (path, TABLES_ENV))
    return data


def dump(path, obj):
    """JSON を書く。差分が見やすいよう整形して改行で終える。"""
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with io.open(path, "w", encoding="utf-8") as fh:
        fh.write(json.dumps(obj, ensure_ascii=False, indent=2) + "\n")


def named(value):
    """名前として使える文字列か。空と埋め草は名前ではない。"""
    text = (value or "").strip()
    return bool(text) and text not in PLACEHOLDERS


def name_of(row):
    """入力の1行から名前を取る。埋め草は空にする。"""
    text = ((row or {}).get("Name") or "").strip()
    return "" if text in PLACEHOLDERS else text


def write_localized(basename, values_by_lang, keys):
    """
    4言語ぶんの `{basename}.{言語}.json` を書く。

    鍵は全言語で同じにする。版差で収録IDが違っても、鍵集合が言語で変わると
    畳み込みの停止条件が言語で変わってしまうため。足りない側は空文字で埋め、
    表示時に zh-CN へ落ちるのに任せる。

    **既存の名前は消さない。** 新しい値が空のときは既存値を残す。
    テーブルには入力側が持っていない名前が入っており、素直に作り直すと失われる。
    """
    keys = sorted(keys, key=int)
    for lang in LANGS:
        path = os.path.join(LOCALIZATION, "%s.%s.json" % (basename, lang))
        current = load(path) or {}
        fresh = values_by_lang.get(lang, {})
        merged = {}
        for key in keys:
            value = (fresh.get(key) or "").strip()
            merged[key] = value if value else (current.get(key) or "").strip()
        dump(path, merged)
        kept = sum(1 for k in keys if not (fresh.get(k) or "").strip() and merged[k])
        print("  %s.%-6s %5d鍵 / 名前あり %5d%s"
              % (basename, lang, len(keys), sum(1 for v in merged.values() if v),
                 " (うち既存値を維持 %d)" % kept if kept else ""))
