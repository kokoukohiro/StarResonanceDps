# DataTools(モジュールツール用)

モジュールツール(`StarResonanceDps.Plugins.ModuleTool`)が使う表と名前を、言語別のJSONテーブルから生成するツール。
本体用のDataTools(`../Core`)とは別のツール。

Python 3の標準ライブラリだけで動く。追加のパッケージは要らない。

```bash
cd StarResonanceDps/DataTools/ModuleTool
python gen_modules.py    # モジュールの表と名前
```

作業ディレクトリはどこでもよい。`_common.py`は共有モジュールなので、単体では実行しない。

## 入力

**`Star`と`StarASIA`の両方の、言語別のJSONテーブルが要る。** リポジトリには含まれないので、各自で用意する(用意のしかたは`../Core/README.md`の「入力」)。

```
JSONS/
  Star/Ztable/{cn,en,jp,kr}/ZTable/*.json
  StarASIA/Ztable/{cn,en,jp,kr}/ZTable/*.json
```

既定ではリポジトリのフォルダと同じ階層の`JSONS`を使う。別の場所に置くなら、環境変数`BPSR_TABLES`で指定する。

使う表: `ModEffectTable` / `ModLinkEffectTable` / `ModHoleTable` / `ModTable` / `ItemTable`。

## 出力

`StarResonanceDps.Plugins.ModuleTool/Data/`の次の2つを書き直す。本体の`Data`には書かない。

| ファイル | 中身 |
|---|---|
| `ModuleTables.json` | 効果・モジュールの数値(レベルごとのリンク数と能力スコアなど)とアイコンのファイル名 |
| `ModuleNames.json` | 効果・モジュールの4言語の名前 |

**2つともプラグインのDLLに埋め込まれる。** 生成したら、プラグインをビルドし直して反映する。
