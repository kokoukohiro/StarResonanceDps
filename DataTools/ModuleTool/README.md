# DataTools(モジュールツール用)

モジュールツール(`StarResonanceDps.Plugins.ModuleTool`)が使う表と名前を、言語別のJSONテーブルから生成するツール。
本体用のDataTools(`../Core`)とは別のツール。

Python 3で動く。`gen_module_images.py`の`--write`と`--compare`だけPillowが要る(`--compare`はnumpyも要る)。

```bash
pip install pillow numpy    # gen_module_images.pyの--write / --compareを使うときだけ
```

```bash
cd StarResonanceDps/DataTools/ModuleTool
python gen_modules.py          # モジュールの表と名前
python gen_module_images.py    # モジュールツールの画像の足りないものを表示する(--writeで取り出して置く、--compareで今の画像と比べる)
```

作業ディレクトリはどこでもよい。`_common.py`は共有モジュールなので、単体では実行しない。

## 入力

**`Star`と`StarASIA`の両方の、言語別のJSONテーブルが要る。** リポジトリには含まれないので、各自で用意する(用意のしかたは`../Core/README.md`の「入力」)。

```
JSONS/
  Star/Ztable/{cn,en,jp,kr}/ZTable/*.json
  Star/Unk/*.bin
  Star/Bundles/*.ab
  StarASIA/Ztable/{cn,en,jp,kr}/ZTable/*.json
  StarASIA/Unk/*.bin
  StarASIA/Bundles/*.ab
```

既定ではリポジトリのフォルダと同じ階層の`JSONS`を使う。別の場所に置くなら、環境変数`BPSR_TABLES`で指定する。

`gen_modules.py`が使う表: `ModEffectTable` / `ModLinkEffectTable` / `ModHoleTable` / `ModTable` / `ItemTable`。
`gen_module_images.py`は`gen_modules.py`の出力(`Data/ModuleTables.json`)と、`Unk`・`Bundles`を読む。

## 出力

`StarResonanceDps.Plugins.ModuleTool/Data/`の次の2つを書き直す。本体の`Data`には書かない。

| ファイル | 中身 |
|---|---|
| `ModuleTables.json` | 効果・モジュールの数値(レベルごとのリンク数と能力スコアなど)とアイコンのファイル名 |
| `ModuleNames.json` | 効果・モジュールの4言語の名前 |

**2つともプラグインのDLLに埋め込まれる。** 生成したら、プラグインをビルドし直して反映する。

`gen_module_images.py`は、`--write`のときだけ`StarResonanceDps.Plugins.ModuleTool/Assets/`に足りない画像を書く(無いファイルだけ)。
画像もプラグインのDLLに埋め込まれるので、書いたらプラグインをビルドし直す。

- 先に`gen_modules.py`を流す(必要な画像は`Data/ModuleTables.json`から決まる)
- 入力を新しくするときは、`Data`を写してから`gen_modules.py`を流し、`gen_module_images.py --before <写し>`で新しく足りなくなった画像だけを見る
- `--write`は今あるファイルを上書きしない。ゲームの画像が変わったかは`--compare`で見て、更新する画像はファイルを消してから`--write`を流す
- 「バンドルに無い(用意が要る)」と出た画像は手で用意する
