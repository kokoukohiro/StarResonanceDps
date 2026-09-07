# DataTools 共通。パスと入出力だけを持つ。
#
# 各ツールは単体で実行できる。呼び出しは DataTools を作業ディレクトリにしなくてよい
# (このファイルからの相対でリポジトリの位置を求める)。
import io
import json
import os

# …\StarResonanceDps\DataTools\_common.py → …\StarResonanceDps
SOLUTION = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
WORKSPACE = os.path.dirname(SOLUTION)

# 一次データ。2026-09-07 に構成が変わり、言語フォルダは Ztable の下へ移った。
# 外側の Ztable は t が小文字、その下の ZTable は大文字。
UNPACK = os.path.join(WORKSPACE, "Star-Unpack", "Ztable")

DATA = os.path.join(SOLUTION, "StarResonanceDps.Core", "Data")
LOCALIZATION = os.path.join(DATA, "Localization")
MAPPINGS = os.path.join(DATA, "Mappings")

# 表示言語 → Star-Unpack のフォルダ名。
# cn / en は中国サーバー、jp / kr はアジアサーバーのビルドで、テーブルの版が違う。
LANGS = {"zh-CN": "cn", "en-US": "en", "ja-JP": "jp", "ko-KR": "kr"}

# 訳が用意されていない行に入っているゲーム側のプレースホルダ。名前として扱わない。
PLACEHOLDERS = {"场地标记01", "气刃突刺计数"}


def load(path):
    """JSON を読む。無ければ None。"""
    if not os.path.exists(path):
        return None
    with io.open(path, encoding="utf-8") as fh:
        return json.load(fh)


def table(lang_dir, name):
    """Star-Unpack の ZTable を読む。"""
    path = os.path.join(UNPACK, lang_dir, "ZTable", name + ".json")
    data = load(path)
    if data is None:
        raise FileNotFoundError(path)
    return data


def dump(path, obj):
    """JSON を書く。差分が見やすいよう整形して改行で終える。"""
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with io.open(path, "w", encoding="utf-8") as fh:
        fh.write(json.dumps(obj, ensure_ascii=False, indent=2) + "\n")


def named(value):
    """名前として使える文字列か。空とプレースホルダは名前ではない。"""
    text = (value or "").strip()
    return bool(text) and text not in PLACEHOLDERS


def name_of(row):
    """ZTable の1行から名前を取る。プレースホルダは空にする。"""
    text = ((row or {}).get("Name") or "").strip()
    return "" if text in PLACEHOLDERS else text


def write_localized(basename, values_by_lang, keys):
    """
    4言語ぶんの `{basename}.{言語}.json` を書く。

    鍵は全言語で同じにする。版差で収録IDが違っても、鍵集合が言語で変わると
    畳み込みの停止条件が言語で変わってしまうため。足りない側は空文字で埋め、
    表示時に zh-CN へ落ちるのに任せる。

    **既存の名前は消さない。** 新しい値が空のときは既存値を残す。
    テーブルには旧プロジェクトからの移植や実測で入れた名前があり、
    unpack から素直に作り直すと失われる(実測: skills の zh 16件 / en 5件)。
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
