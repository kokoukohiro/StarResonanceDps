# DataTools

`StarResonanceDps.Core/Data/` に置く名前テーブル類を、言語別の JSON テーブルから作り直すツール。

素の Python 3 だけで動く。依存パッケージは無い。

```bash
cd StarResonanceDps/DataTools
python gen_recounts.py    # メーターの行名
python gen_buffs.py       # バフ名(BuffTable の全行)
python gen_skills.py      # スキル名と公式の技名(SkillTable の全行の Name と SkillTalk)
python gen_dbms.py        # ボス大技の予告(DbmTable)の技名
python gen_scenes.py      # シーン名(ダンジョンではダンジョン名)と難易度名
python gen_monsters.py    # モンスターの名前
python gen_skill_warnings.py  # 戦闘画面の警告バーを出す技
python gen_rogue_entries.py   # オプション(ローグ系モード)の名前
python gen_buff_groups.py     # 料理・薬剤のバフの名前
python gen_season_talents.py  # シーズンタレントの型の根ノードの名前
python gen_class_specs.py     # 職業の特化の名前
python gen_seasons.py         # シーズンの名前とシーズンランクの名前
python gen_equips.py          # 装備の数値・効果の文言
python gen_match_targets.py   # マッチング先(パーティ・活動)の名前
python gen_root_buffs.py      # 根のバフ(タレント・心相晶・因子・モジュール効果)の名前・種類・絵と、タレントの木の形・タレントの名前
python gen_items.py           # アイテムの名前(装備の名前もここ)
python gen_chat_hyperlinks.py # チャットのリンクの発言の文の型と、ギルドの建物の名前
```

作業ディレクトリはどこでもよい（`_common.py` が自身の位置からリポジトリを求める）。
`_common.py` はパスと入出力だけを持つ共有モジュールで、単体では実行しない。

## 入力

**言語別の JSON テーブル一式が要る。リポジトリには含まれない。** 各自で用意する。

[StarResonanceTool](https://github.com/PotRooms/StarResonanceTool)の内部コードに手を入れると、
言語別の Ztable を取り出せる。

期待する構成は「出所 → `Ztable` → 言語フォルダ → `ZTable` → `*.json`」。
`gen_skill_warnings.py` は同じツールの出力の `Unk` と `Bundles` も読む(どちらも出所ごと)。

```
JSONS/
  StarASIA/Ztable/{cn,en,jp,kr}/ZTable/*.json
  StarASIA/Unk/*.bin
  StarASIA/Bundles/*.ab
  Star/Ztable/{cn,en,jp,kr}/ZTable/*.json
  Star/Unk/*.bin
  Star/Bundles/*.ab
```

既定ではリポジトリの隣の `JSONS` を見る。別の場所に置くなら環境変数で指す。

```bash
set BPSR_TABLES=<置き場所>        # Windows
export BPSR_TABLES=<置き場所>     # bash
```

読むファイルは各ツールの節に挙げたものだけ。無ければどこを設定すればよいかを示して止まる。
**パスの決定は `_common.py` の1か所で、各ツールは触らない。**

### 出所は項目で使い分ける

**見出し表(`gen_recounts.py`)の行構成は `Ztable（Star）` だけで組む。** 行名は下の言語別の土台に従う。

`Ztable（Star）` のほうが行構成が新しく、被覆も広い。
一方で `Ztable（Star）` は jp/kr の行名の列が壊れているので、jp/kr の行名は `StarASIA` から取る
(壊れた列は総括行の検算で弾かれる)。

### 土台は言語で分ける — cn / en は `Ztable（Star）`、jp / kr は `Ztable（StarASIA）`

`Ztable（Star）` と `Ztable（StarASIA）` は別のビルドで、版が違う。
**言語フォルダごとに、その言語が入っているビルドの表を土台にする**(`_common.sources_for`)。

| 言語フォルダ | 土台 | 補う側 |
|---|---|---|
| cn / en | `Star` | `StarASIA` |
| jp / kr | `StarASIA` | `Star` |

**行は土台が勝ち、補う側は無い行を足すだけ。** そのうえで、採った行の**空欄の文字列項目**だけを
補う側で埋める(`_common.table`)。版によって同じIDの値が食い違うときも土台を採る。

### 行番号で引くテーブルは合併しない

`RecountTable` の鍵は**行番号**で、版が違えば同じ番号が別の行を指す。
`Id` は単なる連番なので同一性の根拠にならない。

このテーブルは `_common.table_of_source` で出所を名指しして取る。
**行を跨いで重ねるのは、行の対応を鍵の重なりで取ってからだけ。**

**言語をまたぐときも同じ。** 行数も収録IDも一致しないので、
名前を引く言語ごとに、同じ出所の同じ行を指していることを確かめてから取る。

**各ツールが読むのは入力側だけ。** 手動編集ファイルは読みも書きもしない。
例外は `gen_buffs.py` / `gen_skills.py` / `gen_monsters.py` で、空になった名前を名前の上書きファイルへ書き足す(→「空になった名前の書き足し」)。

## 出力

| ツール | 出力 |
|---|---|
| `gen_recounts.py` | `Data/Localization/RecountRows.json` |
| `gen_buffs.py` | `Data/Localization/BuffNames.json`<br>`Data/Overrides/BuffNameOverrides.json`(空になった名前を書き足す) |
| `gen_skills.py` | `Data/Localization/SkillNames.json`<br>`Data/Localization/SkillTalkNames.json`<br>`Data/Overrides/SkillNameOverrides.json`(空になった名前を書き足す) |
| `gen_dbms.py` | `Data/Localization/DbmNames.json` |
| `gen_scenes.py` | `Data/Localization/SceneNames.json`<br>`Data/Localization/DungeonTypeNames.json` |
| `gen_monsters.py` | `Data/Localization/MonsterNames.json`<br>`Data/Overrides/MonsterNameOverrides.json`(空になった名前を書き足す) |
| `gen_skill_warnings.py` | `Data/Generated/SkillWarnings.json` |
| `gen_rogue_entries.py` | `Data/Localization/RogueEntryNames.json` |
| `gen_buff_groups.py` | `Data/Localization/CuisineBuffs.json`<br>`Data/Localization/PotionBuffs.json` |
| `gen_season_talents.py` | `Data/Localization/SeasonTalentNames.json` |
| `gen_class_specs.py` | `Data/Localization/ClassSpecNames.json` |
| `gen_seasons.py` | `Data/Localization/SeasonNames.json`<br>`Data/Localization/SeasonRankNames.json` |
| `gen_equips.py` | `Data/Generated/Equips.json`<br>`Data/Localization/EquipEffectTexts.json` |
| `gen_match_targets.py` | `Data/Localization/TeamTargetNames.json`<br>`Data/Localization/SeasonActNames.json` |
| `gen_root_buffs.py` | `Data/Localization/RootBuffNames.json`<br>`Data/Localization/TalentNames.json`<br>`Data/Generated/RootBuffs.json` |
| `gen_items.py` | `Data/Localization/ItemNames.json` |
| `gen_chat_hyperlinks.py` | `Data/Localization/ChatHyperlinks.json`<br>`Data/Localization/UnionBuildingNames.json` |

## 全ツール共通の仕様

### 4言語を1つのファイルに書く

`Data/Localization` の名前テーブルは、鍵ごとに4言語の名前を持つ。言語の並びは zh-CN → en-US → ja-JP → ko-KR。

```json
{ "<鍵>": { "zh-CN": "<名前>", "en-US": "<名前>", "ja-JP": "<名前>", "ko-KR": "<名前>" } }
```

`RecountRows.json` だけは行の構成も持つので、`RecountName` がこの4言語の形になる(→ gen_recounts.py)。

### 鍵ごとに4言語すべてを書く

**鍵集合は4言語で同じにする。** 版差で収録IDが違っても、言語で変えない。
どの鍵も4言語すべてを持ち、名前の無い言語は空文字で入る。

**鍵集合が言語で変わると、表示言語によって畳み込みの停止条件が変わってしまう。**
名前が無い側は実行時に zh-CN へ落ちる（`ResolveText` はキー欠落でも空欄でも zh-CN へ落ちる）。

`gen_recounts.py` は行構成を1つの出所だけで組むので、鍵は自然に4言語で揃う。
他のツールは4言語の和集合を取る。

**zh-CN 自身に名前が無い鍵はどの言語でも空になる。** 落ちる先が空だから。
埋めたければ上書きファイルに書くか、生成物を直接編集する。

### 完全な上書き

**出力は入力だけで決まる。** 入力に名前が無ければ空になる。
前の出力を読むのは、`gen_buffs.py` / `gen_skills.py` / `gen_monsters.py` が空になった名前を上書きファイルへ書き足すときだけで、出力そのものには使わない。

生成物を直接編集しても次に流せば消える。残したい手修正は上書きファイルへ置く。

### プレースホルダは名前として扱わない

未翻訳の行に入っている埋め草文字列（`_common.PLACEHOLDERS`）は空として扱う。

## 手動編集ファイル

**生成物ではないファイル。** ツールは作り直さないので、再生成しても消えない。
`MonsterNameOverrides.json` / `SkillNameOverrides.json` / `BuffNameOverrides.json` にだけ、生成器が空になった名前を書き足す(→「空になった名前の書き足し」)。

**名前の表に重ねるものはファイル名を `～NameOverrides.json` にする。** 残りは同梱の生の表に重ねる。

| ファイル | 中身 |
|---|---|
| `Data/Overrides/RecountRowOverrides.json` | メーターの行の出入りと名前 |
| `Data/Overrides/BuffOverrides.json` | 同梱 `Data/BuffTable.json` の項目の差し替え |
| `Data/Overrides/SkillOverrides.json` | 同梱 `SkillTable` の `SkillLevelGroup` の補い |
| `Data/Overrides/SkillActivationOverrides.json` | 推移グラフの技のアイコンの名前とアイコン |
| `Data/Overrides/MonsterNameOverrides.json` | モンスターの名前(`MonsterNames.json`)の差し替え |
| `Data/Overrides/SkillNameOverrides.json` | 技の名前(`SkillNames.json`)の差し替え |
| `Data/Overrides/BuffNameOverrides.json` | バフの名前(`BuffNames.json`)の差し替え |
| `Data/Overrides/BuffNameAssignedOverrides.json` | 名前の無いバフと、名前を変えるバフに割り当てる名前 |

`MonsterNameOverrides.json` / `SkillNameOverrides.json` / `BuffNameOverrides.json` に入っているのは、
**入力の表の更新で名前が落とされた行**の名前。以前の版の入力にはあり、今の版では空になっている。

行そのものは今の入力にも残っていて、消えたのは名前だけ。
ゲーム側が名前を引き下げた結果なので、取り込みの失敗ではない。
生成物は入力だけで決まるため、ここに置かないと流すたびに空へ戻る。

**逆に、ゲーム側が名前を入れ直しても上書きのほうが勝つ。** 名前が復活した鍵は手で外す。

### 空になった名前の書き足し

`gen_monsters.py` / `gen_skills.py` / `gen_buffs.py` は、`MonsterNames.json` / `SkillNames.json` / `BuffNames.json` を書く前に前の出力と比べ、
**前に名前があって今回空になった言語**を `MonsterNameOverrides.json` / `SkillNameOverrides.json` / `BuffNameOverrides.json` へ書き足す(`_common.carry_dropped_names`)。
比べるのは鍵と言語ごと。

| | |
|---|---|
| 上書きに鍵が無い | 空になった言語だけを持つ項目を足す。空になっていない言語は書かない(空文字は生成値を消す意味になるため) |
| 上書きに鍵がある | 空になった言語だけ前の名前で上書きする。ほかの言語はそのまま |
| 今回の入力に行ごと無い鍵 | 書き足さず、鍵を表示する(上書きは行が残って名前だけ消えたものを戻すため) |

上書きファイルを先に書き、名前の表は後に書く。上書きファイルが無いか形が違えば止まり、名前の表も書かない。
書き足した鍵・言語・名前は全部表示する。

### MonsterNameOverrides.json

`MonsterNames.json` を読んだ後に重ねる。入力の表で名前が空になっているモンスターの名前を補う。

```json
{ "<モンスターの種別ID>": { "Name": { "zh-CN": "<名前>", "en-US": "<名前>", "ja-JP": "<名前>", "ko-KR": "<名前>" } } }
```

| | |
|---|---|
| `Name` | 書いた言語だけ差し替わる。生成物に名前があっても、こちらが勝つ |
| `Name` に空文字 | **生成値を消す。** 以後は通常どおり zh-CN へ落ちる |
| 生成物に無いキー | 書ける。名前の表に行がある扱いになる |

**`Name` の無い項目は置かない。** 行を作らずに飛ばし、エラーログを出す。
キーが番号の形でない、言語名の打ち間違いも、エラーログを出して飛ばす。ファイルが無ければエラーログを出し、上書き無しのまま続ける。

### SkillNameOverrides.json

`SkillNames.json` を読んだ後に重ねる。入力の表で名前が空になっている技の名前を補う。
**形も振る舞いも `MonsterNameOverrides.json` と同じ**(重ねる先と鍵だけが違う)。

```json
{ "<技ID>": { "Name": { "zh-CN": "<名前>", "en-US": "<名前>", "ja-JP": "<名前>", "ko-KR": "<名前>" } } }
```

**鍵は技ID。** 技レベルID(技ID×100＋レベル)ではない。

### BuffNameOverrides.json

`BuffNames.json` を読んだ後に重ねる。入力の表で名前が空になっているバフの名前を補う。
**形も振る舞いも `MonsterNameOverrides.json` と同じ**(重ねる先と鍵だけが違う)。

```json
{ "<バフID>": { "Name": { "zh-CN": "<名前>", "en-US": "<名前>", "ja-JP": "<名前>", "ko-KR": "<名前>" } } }
```

同じバフIDを `BuffOverrides.json` にも書ける。あちらは生の表の項目(`BuffType` / `BuffPriority` / `Icon` など)で、名前には触らない
(`FoldInto` で畳んだバフだけは、畳み先の名前が写る)。

### BuffNameAssignedOverrides.json

`BuffNameOverrides.json` を重ねた後に重ねる。**どの版の表にも名前の無いバフ**と、**表や古い版の名前とは別の名前で出すバフ**に割り当てる名前を書く
(`BuffNameOverrides.json` は表の更新で落ちた名前を戻したもの)。
形も振る舞いも `MonsterNameOverrides.json` と同じ。こちらが最後に重なるので、同じ鍵ならこちらが勝つ。
書いた言語だけ差し替わるので、ある言語を zh-CN の名前で出すときは、その言語を書かない。
`BuffOverrides.json` の `FoldInto` で畳んだバフには書かない(畳み先の名前が写る)。

```json
{ "<バフID>": { "Name": { "zh-CN": "<名前>", "en-US": "<名前>", "ja-JP": "<名前>", "ko-KR": "<名前>" } } }
```

### SkillOverrides.json

同梱の `SkillTable` を読んだ後に重ねる。**書くのは `SkillLevelGroup` だけ。**
スキル枠のバッジが、付与元の技から装備中のイマジン・ロールスキルへ辿るのに使う。
推移グラフの技のアイコンの枠もこの寄せ先で決まる(枠の番号を持たない技は、`SkillLevelGroup` の先の技の枠になる)。

```json
{
  "<技ID>": {
    "SkillLevelGroup": <寄せ先の技ID>
  }
}
```

キーが `SkillTable` に無いときは、`BuffOverrides` と同じく**技の行を作ってから当てる**(ID と `SkillLevelGroup` だけを持つ行)。
`SkillLevelGroup` が書かれていない項目は当てない。ファイルが無ければエラーログを出し、上書き無しのまま続ける。

### SkillActivationOverrides.json

推移グラフの横軸の下に出す、押した技のアイコンの名前とアイコンの手修正。表に名前もアイコンも無い技に使う。
ほかに次の2つにも使う。

- 通常攻撃の2段目以降。名前は1段目の見出し表の行名、アイコンは1段目のアイコン(段はコードで判定しない)
- 押した技の番号とダメージの発生源キーが違い、見出し表の名前に届かない技。名前は見出し表(その手修正を含む)の名前を写す

鍵は押した技の技ID。名前とアイコンを見出し表の行・技の名前の表・技の表で決めた後に、**書いてある言語の名前とアイコンだけ**を差し替える。

```json
{
  "<技ID>": {
    "Name": { "zh-CN": "…", "en-US": "…", "ja-JP": "…", "ko-KR": "…" },
    "Icon": "<アイコンのパス>"
  }
}
```

- `Name` と `Icon` はどちらも省略できる。空の値は書かなかったのと同じ
- `Icon` はファイル名だけで書く。アプリは最後の部分をファイル名として技のアイコンのフォルダから探す。
  **イマジンの絵だけは頭の `ui/textures/skill_aoyi/` を付ける**(イマジンの絵かどうかをこの頭で決める)。ほかの頭は使わないので書かない
- 鍵が技IDの形でない行と知らない言語の名前はエラーログを出して読まない。ファイルが無ければエラーログを出し、上書き無しのまま続ける

### BuffOverrides.json

同梱の `Data/BuffTable.json` を読むときに重ねる。名前ではなく**項目**の差し替え。

```json
{
  "<バフID>": {
    "BuffType": <値>,
    "BuffPriority": <値>,
    "Icon": "<アイコン名>",
    "FoldInto": <畳み先のバフID>
  }
}
```

鍵がバフID。表に無いIDなら、そのIDの行を作ってから当てる。

| 項目 | |
|---|---|
| `BuffType` | バフ側かデバフ側か。生の値をそのまま使うとバフとデバフの振り分けが実行時と食い違うため、**振り分けは実質このファイルが決めている** |
| `BuffPriority` | ゲーム内のバフ表示に出すか(0 は出さない)。**バフ/デバフリストもこの値(手修正を重ねた値)とアイコンで出すか決まる**(0 なら出さない、0 以外ならアイコンがあるときだけ出す) |
| `FoldInto` | 畳み先のバフID。畳んだバフはバフ/デバフリストで畳み先と同じものとして出る(名前・絵・側・優先度・行とカードの鍵・非表示リストの番号)。読み込みが、全部の手修正を重ねた後の畳み先の `BuffType` / `BuffPriority` / `Icon` / `ShowHUDIcon` と名前を写すので、畳む側にはほかの項目を書かない。同じ行には、同じ付与者(プレイヤーのリストは付与者に関係なく)の分が残り時間の一番長いものでまとまる。畳み先が表に無い・自分自身・さらに畳まれているときはエラーログを出して畳まない |
| `Icon` / `ShowHUDIcon` | アイコンのファイル名。`"-"` は空にする。アプリは最後の部分をファイル名として探すので、**ファイル名だけで書き、頭は判定に使うものだけ付ける**(ほかの頭は使わないので書かない)。**モジュール効果の絵は頭の `ui/atlas/mod_effect/` を付ける**(探すフォルダと絵の大きさをパスの頭で決める)。**因子の絵は頭の `Factors/` を付ける**(アプリの `Data/Images/Factors` を探す)。**`Icon.` で始まる値はアプリの `Styles/Icons.xaml` の絵の鍵**で、白い形の絵として描く(装備効果の `Icon.EquipSlot.202` / `Icon.EquipSlot.200` など) |

必要な項目だけ書く。**中身の無い項目を置かない。** 差し替える中身が無くても、そのIDが登録されている扱いになる。

表の優先度が 0 のバフは出ない。出すなら `BuffPriority` に 0 以外の値を書く。表の優先度が 0 以外で、出したくないものは `BuffPriority` に 0 を書く。
`BuffPriority` を 0 以外にしてもアイコンが無いバフは出ない。出すなら `Icon` も書く。
アイコンを書いたのにアプリの絵のフォルダにファイルが無いバフは、はてなの絵で出る。
モジュール効果のバフの絵は、`Icon` にモジュール効果の絵のパス(`ModEffectTable` の `EffectConfigIcon`)を書く。名前は `BuffNameAssignedOverrides.json` に書く。
装備効果(セット効果・武器効果)のバフの絵は、`Icon` に Icons.xaml の鍵を書く(セット効果 `Icon.EquipSlot.202`、武器効果 `Icon.EquipSlot.200`)。鍵が Icons.xaml に無ければ、はてなの絵で出る。
因子のバフの絵は、`Icon` に `Factors/` と因子のアイテムの絵のファイル名を書く(`ItemTable` の `Icon`)。表に頭上の絵(`ShowHUDIcon`)があるバフは、アプリがそちらを先に使うので `ShowHUDIcon` に `"-"` も書く。

### RecountRowOverrides.json

生成物は流すたびに書き直されるので、手修正はこちらへ置く。**行の出入りと名前の2つを持つ。**

```json
{
  "55355:1":   { "Row": "21427:1" },
  "3210022:3": { "Row": null, "Name": { "ja-JP": "奥義！メテオフォール(パッシブ)" } },
  "50037:3":   { "Name": { "en-US": "Countercrush (Counter Storm)" } }
}
```

| | |
|---|---|
| キー | 発生源キー `ownerId:枝番`。**行のどのメンバーに書いてもよい** |
| `"Row": "<別のキー>"` | **追加** — そのキーが属する行へ入れる |
| `"Row": null` | **削除** — 行から外して単独の行にする |
| `Row` を書かない | 生成物のまま |
| `Name` | **その行**の名前。書いた言語だけ差し替わる |
| `Name` に空文字 | **生成値を消す。** 以後は通常どおり zh-CN へ落ちる |

**行代表ではなくメンバーのキーで書く。** 行代表は版で動くので、
代表で書くと再生成のたびに静かに効かなくなる。

**解決は2巡。** 先に `"Row": null` を全部当ててから `"Row": "<キー>"` を当てるので、
「本体行から外したキーへ、別のキーを寄せる」が1ファイルで書ける。

**生成物に無いキーも書ける。** `Name` だけ書けば単独の行として増える。
**このとき `"Row": null` は要らない。** `null` は「いま属している行から抜く」という指示で、
どの行にも属していないキーには抜く先が無い。書いても書かなくても結果は同じ(実行して確認済み)。

逆に、**生成物の行に載っているキーに `Name` だけ書くと、その行全体の名前が変わる。**
そのキーだけ別の名前で出したいなら `"Row": null` が要る。

**静かに効かない失敗はログに出す。** キーや `Row` の値が `ownerId:枝番` の形でない、言語名の打ち間違い
— いずれもエラーが出て飛ばされる。入れたらログを確認する。

**`Row` の指す先が生成物にも手修正にも無いときは、エラーにならない。** 指す先のキーで行を新しく作り、そこへ入れる
(キーを足す仕様と同じ扱い)。打ち間違えると、名前の無い行が黙って増える。

---

## gen_recounts.py

メーターの行の見出しを持つ表から、**発生源キー → 行名**と**発生源キー → 行代表キー**を作る。

### 鍵は `ownerId:枝番`

ゲームは `RecountTable.DamageId` で行を引く。`DamageId` はワイヤに乗っていないが、
`SyncDamageInfo` の `OwnerId` と `HitEventId` の組がこれと1対1で対応する。

```
DamageId → DamageAttrTable.TypeEnum = ownerId
DamageId の下2桁                    = 枝番(HitEventId)
```

**`TypeEnum` だけでは粗すぎる。** 同じ `TypeEnum` の `DamageId` が別の行に入る例が
26〜28種あり、枝番を見ないと片方の行が消える。

### 行構成と名前で出所を使い分ける

行の組み立ては `Ztable（Star）` だけで行う。行名は言語別の土台(cn / en は `Star`、jp / kr は `StarASIA`)から載せ、
空欄だけもう一方で補う。`StarASIA` の行との対応は**共有する鍵の数**で取る（行番号は版で違う）。

`StarASIA` に対応の無い行は `Star` の名前を使う。jp/kr が空欄のまま残る(`Star` の jp/kr の列は検算で弾く)が、
表示時に zh-CN へ落ちるので読める。

### 総括行は落とす

「その他」に当たる行は鍵の大半を占め、個別の名前を持たない。**行ごと出力しない。**
抱える鍵が突出して多い行として特定し、**名前で検算している**。

落とすと表に無い鍵と同じ扱いになり、実行時は名前が空のまま内部ID注記だけが出る。

**検算が落ちたら、その出所×言語の名前列は使わない。** 行の中身は正しくても
名前列だけが詰まっていることがあり、そのまま採るとIDと名前の対応が丸ごとずれる。落ちたことは実行時に `★` 付きで出る。

### 生の見出し表と同じ形で書く

行ごとに `RecountName`(4言語)と、その行に属する発生源キーの一覧を持つ。行の構成はそのまま写す。

**項目名は `SourceId`。`DamageId` にはしない。** 中身は `DamageId` そのものではなく
`TypeEnum:枝番` で、生の `DamageId`（`117010101` など）とは別の値だから。
同じ名前にすると、生の表と見比べたときに値が食い違って見える。

```json
{ "<行>": { "RecountName": { "zh-CN": "<名前>", "en-US": "<名前>", "ja-JP": "<名前>", "ko-KR": "<名前>" }, "SourceId": ["<ownerId:枝番>", "..."] } }
```

**名前を鍵ごとに繰り返さず、畳み込みの対応表も別に持たない。**
行の所属と行名が同じ1か所に入る。生の表と並べて見比べられる。

行の代表キーは実行時に決める（**行の最小キー**。`ownerId` → 枝番 の順で比べる）。
集計も名前引きもそこまで畳んでから行う。

---

## gen_buffs.py

**`BuffTable` の全行**を収録する。`BuffPriority` で絞らない。

バフ/デバフウィジェットに出るバフに加えて、ゲームがバフバーに出さないバフも名前を引ける。
タイムラインは発生源がバフのダメージ(`OwnerId` がバフID)の名前をここから引く。

言語によって収録数に差があるので、鍵は和集合。

前の出力にあって今回空になった名前は `BuffNameOverrides.json` へ書き足す(→「空になった名前の書き足し」)。

> **表示条件はこのファイルで判定しない。**
> アプリが同梱 `Data/Raw/BuffTable.json` の `BuffPriority`・アイコン・持続などで判定する（`BuffDisplayRules`）。
> 名前テーブルに載っていることは、バフバーに出ることを意味しない。

**`201` も空にする。** 埋め草 `气刃突刺计数` は `BuffTable` の先頭行 `201` の名前だが、
`201` 自身も4言語とも中国語のままで訳が無い。`gen_skills.py` の `1101`(他言語に訳語がある)とは事情が違う。

---

## gen_dbms.py

ボス大技の予告(`DbmTable`)の `Content` を、**技IDを鍵に**書く。ゲームが予告に出す正式な技名で、
`SkillTable.Name` が埋め草の技にも名前がある。

`DbmTable.Id` は技IDそのものか、ある技の `SkillTable.EffectIDs` の要素。後者は持ち主の技IDへ寄せる。
どちらにも当たらない行は入れず、一覧を出す(Star の `339510303` / `339510305` / `339510307`)。

`DbmTable` の1行が複数の技に当たるか、1つの技に複数の行が当たったら止まる。
`EffectIDs` の要素を複数の技が共有すること自体は、`DbmTable` に無い番号では普通にある(`14046001` など)。

**StarASIA の cn 版に仮名の混じった行がある**(`17020801` 终焉の前奏 / `17021101` 虚蚀の圆舞)。
cn は Star 土台なので、zh-CN には Star の `终焉前奏` / `虚蚀圆舞` が出る。

---

## gen_skills.py

**`SkillTable` の全行**を収録する。使い道を決めて絞らない。

スキル枠(イマジン・ロール)に加えて、モンスターの技など枠に置かれないスキルも名前を引ける。
以前は枠に置けるIDだけに絞っていたが、絞り込みの条件は使い道が増えるたびに漏れる。

モンスターの技の大半は未翻訳で、名前が埋め草 `场地标记01` になっている。これは空になる
(→「プレースホルダは名前として扱わない」)。

`SkillNames.json` で前の出力にあって今回空になった名前は `SkillNameOverrides.json` へ書き足す(→「空になった名前の書き足し」)。
`SkillTalkNames.json` は対象外。

### SkillTalkNames.json

**`SkillTalk` は公式の技名。** `Name` が埋め草の技にも名前があり、`Name` と違う名前を持つ技もある。
`SkillNames.json` とは別の表に `SkillTalk` をそのまま書き(埋め草は空)、どちらを先に引くかはアプリが決める。

アプリは技の名前を次の順で重ねる。上ほど優先する。

1. `SkillTalkNames.json`(その言語に名前がある技だけ差し替わる)
2. `Data/Overrides/SkillNameOverrides.json`(表の更新で落ちた名前)
3. `SkillNames.json`

### 埋め草と同じ文字列が本物の名前になっている行

**`1101` の zh-CN `场地标记01` は本物の名前。** 連番の「フィールドマーク」01〜06 の先頭で、
他言語には訳語が入っている(`Marker 1` / `フィールドマーク01` / `필드 표시01`)。
埋め草として空にすると zh-CN だけ名前が消え、表示時の落とし先も失うので、
`gen_skills.GENUINE_PLACEHOLDER_NAME_IDS` に持たせて `Name` をそのまま使う。

**`_common.PLACEHOLDERS` 側で例外にしないこと。** バフなど他の生成器にも効いてしまう。

**埋め草の出所は各テーブルの先頭行の名前。** `SkillTable` の先頭は `1101 场地标记01`、
`BuffTable` の先頭は `201 气刃突刺计数` で(StarASIA・Star とも)、未翻訳の行にはこの名前がそのまま入っている。
`201` は4言語とも訳が無いので、`gen_buffs.py` は例外にしない。

---

## gen_scenes.py

アプリはマップの番号でこのテーブルを引く。ダンジョンではその番号がダンジョンの番号になる。

鍵は **`SceneTable` と `DungeonsTable` の番号の和**。番号ごとに次の表で名前を選ぶ。

| その番号の行 | 名前 |
|---|---|
| 両方の表にある | `SceneTable.SceneSubType` がダンジョン(`5`)なら `DungeonsTable.Name`、それ以外は `SceneTable.Name` |
| `SceneTable` だけ | `SceneTable.Name` |
| `DungeonsTable` だけ | `DungeonsTable.Name` |

**両方にある番号は、どちらの表にあるかでは決めない。** ダンジョンの番号はシーン表にも行があり、
ダンジョン表に行があってもシーンの種類がダンジョンでない番号(ギルドの施設など)はシーン名を出す。

言語によって収録数に差がある。無い行は空文字で入り、表示時に zh-CN へ落ちる。

### 難易度名 `DungeonTypeNames.json`

アプリはダンジョン名に難易度名を `ダンジョン名-難易度名` の形で付ける。
このテーブルは、上の表でダンジョン名を選んだ番号にだけ難易度名を持つ。鍵は2つの形。

| 鍵 | 名前 |
|---|---|
| `番号` | `DungeonsTable.DungeonTypeName`。マスターのダンジョン(`PlayType` が MasterMode)は持たない |
| `番号:段階` | マスターのダンジョンの段階ごとの `MasterChallengeDungeonTable.DungeonTypeName`。鍵は `DungeonId` と `Difficulty` から作る |

アプリはダンジョン同期で届いた段階で `番号:段階` を先に引き、無ければ `番号` を引く。
マスターのダンジョンは `番号` を持たないので、段階が分からなければ難易度名を付けない。

どの言語にも名前が無い鍵は書かない。次のときは出力を書かずに止まる。

- `MasterChallengeDungeonTable` の行のダンジョンが、ダンジョン名を選んだ番号にない
- そのダンジョンがマスターのダンジョンでない
- `Difficulty` が正の整数でない
- 同じ `番号:段階` が2行ある

---

## gen_skill_warnings.py

戦闘画面の警告バーを出す技を、**技レベルID(技ID×100＋レベル)の配列**で書く。名前は持たない。
タイムラインは、この一覧にある技の開始を詠唱として出す。

### 判定

`show_data` の技辞書で、**スロット52に項目を持つ技レベル**を収録する。
同じ技でもレベルによって持たないことがあるので、技IDではなく技レベルIDで持つ。

### 入力の探し方

出所ごとに次を行う。

1. `Unk/*.bin` から、中身に `->>>> bundleHash:` を含むアドレス一覧を探す。1本に決まらなければ止まる
2. アドレス一覧を読む
   - アドレス行 `address:<アドレス> ->>>> hash:<数字> ->>>> bundleHash:<数字>`(アドレスは空白を含むことがある)。行数が見出し `AddressCount` と違えば止まる
   - バンドルの一覧(見出し `DepsDictCount` のあとの `bundleHash:<数字>` の行)。行数か異なる番号の数が見出しと違えば止まる
3. バンドルの一覧が**同じ出所の** `Bundles` に全部そろわなければ止まる(一覧と `Bundles` が別の版)
4. `bin/datas/show_data` の番号で `Bundles/<番号>.ab`(UnityFS)を開き、TextAsset `show_data` を取り出す。無ければ止まる

バンドルの番号は版で変わるので直書きしない。**止まったときは出力を書かない**(前の出力が残る)。

### 出力は `Star` で作る

**`Star` に無い技レベルが `StarASIA` に1件でもあれば止まる。** 版の並びが逆転したときに、黙って取りこぼさないため。
止まったら、どちらを土台にするかを決め直す。

### `show_data` の形

先頭 `06` のあと、整数キーの辞書が 技 → バフ → 弾 の順に並ぶ。各辞書は `int32` 件数とレコード。

| 部分 | 形 |
|---|---|
| レコード | `int32` キー / `int32 0` / `45` / `byte` 中身のある枠の数 / 枠×68 |
| 枠 | `int32 -1`(空)か、`int32` 件数と項目 |
| 型4 | `ff` `uint16` `uint16` `ff`。中身を直接持つときは `ff` の代わりに `10` で始まる50バイト |
| 型5 | `ff` `uint16` `uint16` 文字列 `ff` |
| 型6 | `ff` `uint16` `uint16` 文字列 文字列 `ff` |
| 型8 | `ff` `uint16` `uint16` 文字列 文字列 `ff` `uint16` `ff` |
| 文字列 | 先頭の `int32` が `-1` なら無し、0以上なら4バイトの値、それ以外は `~値` がバイト長で `int32` 文字数と UTF-8 が続く |

**読めない形、件数・枠数・区切りの食い違いがあれば止まる。** 辞書を1件も飛ばさない。

---

## gen_rogue_entries.py

オプション(ローグ系モード)の名前を、**オプションのバフIDを鍵に**書く。値は `RogueEntryTable.EntryName`。

メーターの行が見出し表で名前を持たないとき、アプリは記録時に付与元をたどり、オプションのバフに着いたらこの名前を出す。
`EntryId` は鍵にしない。同じバフを複数のオプションの行が指す。

**出所の土台はバフIDの単位で決める**(cn / en は `Star`、jp / kr は `StarASIA`)。土台にそのバフIDの名前があれば土台だけを使う。

同じバフIDで名前が食い違う言語は、**`EntryId` が一番若い行の名前**を採り、一覧を出す。空にも除外にもしない。

---

## gen_buff_groups.py

料理・薬剤のバフを、まとまりごとの名前テーブルに書く。形はほかの名前テーブルと同じ。

料理・薬剤は食べ直すたびに別のIDへ入れ替わるので、バフバーの行から開くバフカードは、
この一覧にあるバフを個別のIDではなくまとまりとして追う。
アプリはこの一覧にあるバフの名前を、`BuffNames.json` ではなくこの一覧から引く。

### 選ぶ条件

`BuffTable` の2つの項目だけで選ぶ。**名前は見ない。**

| 項目 | 条件 |
|---|---|
| `Tags` | 消耗品のタグ(`CONSUMABLE_TAG`)を持つ |
| アイコン | `ShowHUDIcon`、空なら `Icon`。料理のアイコンなら料理、薬剤のアイコンなら薬剤(`GROUP_ICONS`) |

アイコンだけでは、料理・薬剤でないバフも同じアイコンを使っていて混ざる。

### 名前

**そのバフを付けるアイテムの名前。** バフ自身の名前は料理・薬剤の1語に丸められている。
料理の表(`CookCuisineTable`)・薬剤の表(`ChemistryCuisineTable`)の行の `Id` がアイテムの `Id` で、
`BuffPar` の先頭がそのアイテムが付けるバフ。アイテムの名前は `ItemTable` から引く。

| そのバフを付けるアイテム | 名前 |
|---|---|
| 1つ | そのアイテムの名前 |
| 複数 | アイテムの `Id` の順に ` / ` でつなぐ(どのアイテムで付いたかはバフからは決まらない) |
| 無い | バフ自身の名前 |

言語ごとの出所は上の「土台は言語で分ける」と同じ。**つなぐ名前が1つでも空の言語は、その言語ごと空にする**(一部だけの並びを作らない)。

### 出力は `Star` で作る

`StarASIA` も読み、次のときは**書かずに止まる**(前の出力が残る)。

- `StarASIA` にだけある料理・薬剤のバフがある。料理と薬剤が入れ替わっている
- 料理・薬剤の表に、`StarASIA` にだけある行か、付けるバフが `Star` と違う行がある
- 料理・薬剤の表の行が、`ItemTable` に無い

---

## gen_monsters.py

エンティティリストやタイムラインで、モンスターの実体の `AttrId` から表示名を引くのに使う。

鍵は **`MonsterTable`** の行（4言語ぶんの和）。
同じ番号が別の表で別のものを指すことがあり、どの表の名前かは実体の種類でしか決まらない。アプリはモンスターの実体のときだけこのテーブルを引く。

`MonsterTable` は行があっても `Name` が空のことがあり、その数は言語で大きく違う。

**名前が空の行も鍵を持つ。** エンティティリストは「表に行があって名前が空」と「表に行が無い(アプリの表が古い)」を
鍵の有無で見分ける(前者は HP バーが見えなければ出さず、見えれば「敵」「味方」と出す。後者は空欄のまま出す)。

前の出力にあって今回空になった名前は `MonsterNameOverrides.json` へ書き足す(→「空になった名前の書き足し」)。

---

## gen_season_talents.py

シーズンタレントの型の根ノードの名前を、**根ノードのバフIDを鍵に**書く。値は根ノードの名前。

型を付けると、その型の根ノードの効果のバフが乗る。このバフから、その人が付けている型の根ノードを引くのに使う。

根ノードは `SeasonTalentTemplateTable.NoteRootId`。`SeasonTalentEffectOrdinaryTable` のうち `GroupId` がそれに一致する行が根ノードの効果で、
`Effect` の `[3, バフID, 1]` がそのバフ、`Name` が根ノードの名前。根以外のノードは入れない。

入れるのは主の型だけ。効く遊びの種類(`EffectiveGameplayType`)が決まっている型は、その遊びの中だけで主の型の上に足されるので入れない。

**同じ出所の中で1つのバフIDに複数の根ノードが当たったら、型の `BelongSeasonId` が一番大きい行の名前を採り、一覧を出す。**
シーズンをまたいで同じバフを根に使う型がある。行やノードの番号の大小はシーズンの新旧と一致しないので使わない。

次のときは止まる。

- 一番新しいシーズンの中で、同じバフIDの名前が食い違う
- 型の根ノードに当たる行が無い
- 根ノードの行のバフIDが1つでない
- 出所に型の表が無い

## gen_class_specs.py

職業の特化の名前を、**アプリの特化の番号を鍵に**書く。番号は 職業ID × 10000 ＋ 特化の並び(1始まり)。

職業ごとの特化は `ProfessionSystemTable.ShowTalentStage` が並びつきで持つ。その段階の行 `TalentStageTable` の `Name` は
[段階の名前, 特化の名前] で、2番目を使う。

**名前から、その言語の全特化の名前に共通する語尾を落とす。** 語尾は言語ごとに生成器の `COMMON_SUFFIX` で決めている。
同じ語尾はアプリのリソース `ClassSpec_NameSuffix` にもあり、語尾を付け直して出す表示がそれを使う。変えるときは両方を直す。

出所の土台は鍵の単位で決める(`gen_season_talents.py` と同じ)。土台にその鍵の名前があれば土台だけを使い、無いときだけもう一方を使う。

次のときは止まる。

- 職業の段階の行が段階の表に無い
- 段階の行の名前が2要素でない
- 使う名前が、その言語の共通の語尾で終わっていない
- 出所に段階の表が無い

## gen_seasons.py

シーズンの名前と、シーズンランクの名前を書く。

- `SeasonNames.json`: **鍵はシーズン番号。** 値は `AchievementSeasonClassTable` のうち `Type` がそのシーズン番号の行で、
  `SortID` が一番小さい行の `ClassName`。`Type` 0 はシーズンに属さない分類なので入れない
- `SeasonRankNames.json`: **鍵は シーズン番号 × 100 ＋ 段階(`SeasonRankTable.RankToLevel`)。** 値は、その段階の行のうち
  `StarLevel` が一番小さい行の `Name`。同じ段階の中で名前が分かれていれば一覧を出し、★が一番小さい行を採る

出所の土台は鍵の単位で決める(`gen_season_talents.py` と同じ)。土台にその鍵の名前があれば土台だけを使い、無いときだけもう一方を使う。

次のときは止まる。

- シーズンの分類で、`SortID` が一番小さい行が2行以上ある
- 段階の行で、`StarLevel` が一番小さい行が2行以上ある
- 段階が鍵の桁に収まらない(0 未満か 100 以上)、シーズン番号が 1 未満

## gen_equips.py

装備の数値・効果の文言を書く。装備の名前は `gen_items.py` のアイテムの名前を使う(装備IDもアイテムID)。

- `Equips.json`: 装備ごとの数値と、装備が参照する属性庫の行
- `EquipEffectTexts.json`: **鍵は効果の番号。** 進化・改鋳・レアの側に出る一時属性とバフの文言

文言の出所は上の「土台は言語で分ける」と同じ。

### 数値は `Star` で作る

`Equips.json` は `Star` の表だけで作る(数値の表は言語で変わらないので `cn` フォルダを読む)。

シーズン強度の項目は、表の版によって同じ行が属性か一時属性かで違う(その版の今のシーズンは属性、過去のシーズンは一時属性)。
アプリは有効・無効を表の違いではなく、今のシーズンと項目のシーズンで決める。生成物が持つのは項目のシーズンだけ。

### 形

```json
{
  "Equips": {
    "<装備ID>": {
      "Part": <部位>, "Quality": <品質>, "PerfectUpperLimit": <完成度の上限>, "MainStat": <主能力値の系統>,
      "Stages": [ { "Gs": <装備Lv>, "Basic": [<型>, <庫ID>, ...], "Advanced": [<型>, <庫ID>, ...] } ],
      "Recast": [<型>, <庫ID>, ...], "Rare": [<型>, <庫ID>, ...]
    }
  },
  "AttrLibs": {
    "<型>": { "<庫ID>": [ { "Id": <行ID>, "Parts": [<部位>], "Specs": [<特化>], "Effects": [ { "Kind": <種類>, "Id": <番号>, "Min": <下限>, "Max": <上限>, "Format": <書式> } ] } ] }
  },
  "StrengthSeasons": [ { "Kind": <種類>, "Id": <番号>, "Season": <シーズン> } ],
  "SpecSchools": { "<特化の番号>": <特化> },
  "Rank1Schools": { "<職業ID>": <特化> }
}
```

| 項目 | 中身 |
|---|---|
| `Part` | `EquipTable.EquipPart` |
| `Quality` | `ItemTable.Quality` |
| `PerfectUpperLimit` | `EquipTable.PerfectUpperLimit` の2要素目 |
| `MainStat` | 段階0 の基礎の庫の行(部位で絞る)に出る属性の系統(番号 − 番号%10)のうち、筋力・知力・敏捷の系統。無ければ 0 |
| `Stages` | 添字が突破の段階。段階0 は `EquipTable`、段階1〜 は `EquipBreakThroughTable` の行を `BreakThroughTime` の順に。`Basic` / `Advanced` は表の庫の配列 `[型, 庫ID…]` のまま |
| `Recast` / `Rare` | `EquipTable.RecastingAttrLibId` / `QualityChildAttrLibId`(改鋳 / レアの庫の配列)のまま。アプリは素の項目で届いた行を引く型にだけ使い、空なら型1 で引く(ゲームと同じ。改造の行は見ない) |
| `AttrLibs` | 型(1 = `EquipAttrLibTable`、2 = `EquipAttrSchoolLibTable`)→ `AttrLibId` → その庫の全行を表の順に。装備の全段階の基礎・進化、改鋳、装備と突破の行のレア、改造(`TransformId` → `EquipTransformTable` の `RecastAttrLibId` / `QualityAttrLibId`)から参照される庫だけ。改造の庫で付いた行も自分の装備の行ID として届く。改造の行が表に無ければ止まる |
| `Id` | 行の `Id`。通信で届く自分の装備の値は、この行ID で行を指す |
| `Parts` / `Specs` | 行の `AllowPart` / `TalentSchoolId`(型1 は空) |

「部位で絞る」は、`AllowPart` に装備の部位か 0 を含む行を採ること。

### 効果

`Effects` は行の `AttrEffect` の要素 `[種類, 番号, …]` を順に歩いて作る。`AttrEffectConfig` の添字を種類ごとに次の数だけ進め、
`Min` / `Max` はその効果の最初の設定の `[min, max]`。

| 種類 | 中身 | 設定を進める数 | `Format` |
|---|---|---|---|
| 1 | 属性 | 1 | `FightAttrTable[番号 − 番号%10].AttrNumType` が 1 か、0 で末尾(番号%10)が 4 なら 1。0 で末尾が 2・3 なら 0 |
| 3 | バフ | 要素の3要素目(無ければ 1) | 1 |
| 5 | 一時属性 | 1 | 0 |

`Format` は 0 がそのままの数、1 が %(値 ÷ 100)。

### シーズン強度の項目 `StrengthSeasons`

基礎の庫(全装備・全段階、部位で絞った行)に出る**一時属性の全部**と、**系統 11440 の属性**をシーズン強度の項目とする。

**項目のシーズンは、それを基礎に持つ装備の `EquipTable.SeasonId` の積集合。** ちょうど1つに決まらなければ止まる。
庫の行ごとには取らない。複数のシーズンに属する装備だけが使う行は、行ごとではシーズンが決まらない。

進化にだけ出る一時属性はシーズン強度ではないので入らない。基礎に出る一時属性が進化にも出たら止まる。

### 特化 `SpecSchools` / `Rank1Schools`

型2 の庫の行は特化(`TalentSchoolTable.Id`)で選ぶ。アプリの特化から特化を引く表を2つ持つ。
どちらも、特化の段階を `TalentStage` に含む `TalentSchoolTable` の行を引く。

- `SpecSchools`: 鍵はアプリの特化の番号(職業ID × 10000 ＋ `ShowTalentStage` の並び。`gen_class_specs.py` と同じ)。段階は `ShowTalentStage` のその並びの要素
- `Rank1Schools`: 鍵は職業ID。段階はクラスR1 の段階(`TalentStageTable` のうち `WeaponType` がその職業で、`TalentStage` と `BdType` が 0 の行)

特化に当たらない段階は出さない。

**段階の欄(`TalentStage`)が空の特化は、ほかの特化と同じ形として扱う。** ほかの特化の段階の欄は
[同じ職業のクラスR1 の段階, 自分の段階] なので、空の特化の段階も、その特化の番号を段階に持つ職業の
クラスR1 の段階と自分の番号とする。型2 の庫で特化の欄(`TalentSchoolId`)が空の行は、この特化の行とする
(`Specs` にその特化を入れる)。

### 効果の文言 `EquipEffectTexts.json`

`AttrLibs` の効果のうち、進化・改鋳・レアの側(`Advanced` / `Recast` / `Rare`、突破の行のレア、改造の改鋳・レアから参照される庫)に出る一時属性とバフの文言。

| 種類 | 文言 |
|---|---|
| 一時属性 | `TempAttrTable.AttrDesc` |
| バフ | `AttrDescription[BuffTable.TipsDescription].Description` |

値の差し込み `{*tempAttr.un*}` と `{*Decision.unmarkpercent(1)*}` は `{0}`(そのままの値)、
`{*tempAttr.up*}` は `{1}`(% の値。値 ÷ 100 の末尾の 0 を落として %)に置き換える。
同じ効果でも言語で差し込みが違うことがあるので、`{0}` と `{1}` のどちらが入るかは言語ごとに決まる。
基礎に出るシーズン強度の一時属性は入れない(アプリはシーズン強度の文言で出す)。

### 止まる条件

次のときは**書かずに止まる**(前の出力が残る)。

- 装備の行がアイテムの表に無い、`PerfectUpperLimit` に2要素目が無い
- 突破の段階が 1 からの連番でない
- 参照される庫が表に無い、庫の配列の型が 1・2 以外
- 効果の種類が 1・3・5 以外、設定の数が効果と合わない、設定が `[min, max]` でない、属性の書式が上の表で決まらない
- 主能力値の系統が2つ出る
- シーズン強度の項目のシーズンが1つに決まらない、基礎に出る一時属性が進化にも出る
- 1つの段階に特化が2つ当たる、職業のクラスR1 の段階が2行ある
- 段階の欄が空の特化が2つ以上ある、それがどの職業の BdType0 の段階でもない、特化の欄が空の行があるのに段階の欄が空の特化が無い
- 文言に上の3つ以外の差し込みか `{0}` / `{1}` 以外の波括弧がある、一時属性とバフで番号が重なる

## gen_match_targets.py

プレイヤーリストの通知「マッチング成立」で、マッチング先の名前を引くのに使う。
マッチング成立の通知はマッチング先を種類と番号で持ち、番号がどの表の行かは種類で決まる。

- `TeamTargetNames.json`: **鍵は `TeamTargetTable` の行**(パーティのマッチング)。値は `Name` で、難易度まで含む
- `SeasonActNames.json`: **鍵は `SeasonActTable` の行**(活動のマッチング)。値は `Name`

鍵はそれぞれの表の行(4言語ぶんの和)。2つの表は同じ番号が別のものを指すので、ファイルを分ける。

## gen_root_buffs.py

根のバフの名前を `RootBuffNames.json` に、種類・並び・絵とタレントの木の形を `RootBuffs.json` に、タレントの名前を `TalentNames.json` に書く。
`RootBuffNames.json` と `RootBuffs.json` の鍵は根のバフのバフID(木は除く)、`TalentNames.json` の鍵はタレントの番号(`TalentTable` の `Id`)。

根のバフは、タレント・心相晶のノード・心相晶の上級・因子・モジュール効果を付けている間、ずっと乗るバフ(それぞれの効果の `[3, バフID, 1]`)。
アプリはプレイヤーに乗っている根のバフから、アビリティ情報(タレントの木)・潜在心相晶情報(ノード・上級・因子)・モジュール情報(モジュール効果)を出す。
名前の表と手修正に名前の無いバフで、付与元をたどってモジュールの根のバフ(同梱 `Data/Raw/ModEffectTable.json` で引く)に着いたものも、この名前で出る。
モジュール効果のバフの名前と絵を番号で出すときは、`BuffNameAssignedOverrides.json` と `BuffOverrides.json` の `Icon` に書く
(絵のパスが `EffectConfigIcon` の形なら、アプリは `Data/Images/Modules` からファイル名で探す)。

| 種類(`RootBuffs.json` の項目) | 表 | 名前 | 絵 | 並び |
|---|---|---|---|---|
| `Talents` | `TalentTable.TalentEffect` | なし(木の節のタレントの名前を使う) | なし(木の節の絵を使う) | `Id` |
| `SeasonTalentNodes` | `SeasonTalentEffectOrdinaryTable.Effect` | `Name` | `IconNormal` | `Id` |
| `SeasonTalentAdvanced` | `SeasonTalentEffectAdvancedTable.Effect` | `Name` | `Icon`(型の上級の番号ごと) | `Id` |
| `Factors` | `SeasonTalentFactorItemTable.FactorItemEffect` | `ItemTable.Name` から等級を外した名前 | `ItemTable.Icon` | バフID |
| `ModuleEffects` | `ModEffectTable.EffectConfig` | `EffectName` | `EffectConfigIcon` | `EffectID` |

- 各項目は `{ "Buff": <バフID>, "Icon": "<絵の全体のパス>" }` の配列で、配列の順が並び。上級だけ `Icon` の代わりに `Icons`(型の上級の番号 → 絵)を持つ。
  タレントは `{ "Buff": <バフID> }` だけ(アプリは取っている節を見分けるのに使う。名前と絵は木の節のタレントから出す)
- `SeasonTalentAdvancedEffects` は型の根のバフ → 型の上級の番号(`SeasonTalentTemplateTable.AdvancedEffectId`)。上級の絵はその人の型で選ぶ。番号が 0 の型(上級を持たない)は入れない
- タレントは、段階の根のタレント(`TalentStageTable.RootId` → `TalentTreeTable.TalentId`)のバフを入れない。特化型のバフで、特化はプレイヤーリストが出す
- 心相晶の上級は、1つの型の上級(`EffectId`)にしか出てこないバフ(型の専用効果)を入れない。入れるのは全部の型が使う共通の効果
- 心相晶のノードは、型の根のノード(`NoteRootId`。型はプレイヤーリストが出す)と、効く遊びの種類(`EffectiveGameplayType`)が決まっている型の木(`SeasonTalentTreeTable.TemplateId`)のノードを入れない
- 因子は、等級を外した名前が2つ以上になる根(シーズンが変わって効果を失った因子の共通の説明のバフ)を入れない。等級の書き方は言語で違う(jp / kr「・G1」、cn「·G1」、en「 - G1」)
- アプリは絵をタレント・ノード・上級は `Data/Images/Talents`、因子は `Data/Images/Factors`、モジュール効果は `Data/Images/Modules` から、パスの最後の部分のファイル名で探す

**タレントの木(`TalentTrees`)** は、アビリティ情報がゲームと同じ形の木を描くのに使う。

- 出所ごとに分けて持つ: `{ "Star": [<木>…], "StarASIA": [<木>…] }`。それぞれその出所の表だけで作る(出所でタレントが違う節や、2つのタレントの位置が入れ替わった節がある)。
  アプリはアジアサーバーのゲームのときだけ `StarASIA` の木、それ以外は `Star` の木を使う
- 両方の出所で中身の違う節は一覧を出す(止めない)
- 木は段階の行(`TalentStageTable` の `RootId` が 0 でない行)ごとに1本。節はその行と同じ(`WeaponType`, `TalentStage`, `BdType`)の `TalentTreeTable` の行
- 1本は `{ "Profession": <職業ID>, "Spec": <特化の番号>, "Root": <根の節>, "Nodes": [...] }`。根の絵と名前は根の節のタレントのもの。
  クラスR1(`TalentStage` 0)の木は `Spec` 0・`Root` 0。クラスR2 の木の `Spec` は、段階が職業の `ShowTalentStage` の何番目かで決める(職業ID × 10000 ＋ 並び、アプリの特化の番号)
- 節は `{ "Id": <TalentTreeTable の Id>, "X", "Y", "Next": [<つなぐ先の節>], "Buffs": [<バフID>], "Talent": <タレントの番号>, "Ability": <真偽>, "Icon": "<絵の全体のパス>", "TalentTag": <役割のタグ>, "Replace": [[<元の技>, <置き換え後の技>]] }`。
  位置は `TalentPosition` を木の左上が 0 になるようにずらした値(下へ増える)、つなぎは `NextTalent`
- `Buffs` は節のタレントの `[3, バフID, 1]` で、どれも `Talents` にある。能力値だけの節は空。クラスR2 の根は特化型のバフなので入れない
- `Talent` は `TalentTreeTable.TalentId`、`Ability` は `TalentTable.TalentType` が 4(アビリティの節。1 は能力値の小さい節、5 は特化型の根)、`Icon` は `TalentIcon`
- `TalentTag` は役割のタグ。その木の職業(`TalentTreeTable.WeaponType`)の `ProfessionSystemTable.Talent`(`TalentTagTable` の番号。1 攻撃 / 2 支援 / 3 防御)。アプリは絵の背景の色を選ぶのに使う
- `Replace` は節のタレントの `[6, 元の技, 置き換え後の技]` の元と先の組の一覧(無ければ空)。アプリは、バフの無いアビリティの節を取っているかを、最後に使ったのが置き換え後の技かで決めるのに使う
- タレントの名前(`TalentNames.json`)は、木の節に出てくるタレント全部。値は `TalentName`

**名前の土台はバフIDの単位で決める**(cn / en は `Star`、jp / kr は `StarASIA`)。土台にそのバフIDの名前があれば土台だけを使い、無いときだけもう一方を使う。
心相晶のノードで同じ出所の中の1つのバフIDに複数の行が当たったら、型の `BelongSeasonId` が一番大きい行の名前を採る。
ほかの種類で1つのバフIDに名前の違う行が当たったら、並びの一番小さい行の名前を採り、一覧を出す。
タレントの名前も同じ土台で、タレントの番号の単位で決める。

**種類・並び・絵は `Star` を土台にし、無いバフだけ `StarASIA` で補う。** 出所で絵が違うバフは一覧を出す。

次のときは止まる。

- 1つのバフIDが2つの種類に当たる
- 段階の根が木の表に無い、上級の番号が1つしか無い(型の専用効果を見分けられない)
- 心相晶のノードの行がどの型の木にも無い、主の型とローグライクの型の両方の木に入る
- 一番新しいシーズンの中で、同じノードのバフIDの名前が食い違う
- 型の根のバフに上級の番号が2つ以上当たる、1つのモジュールの根のバフに効果が2つ以上当たる
- タレントの木が空、つなぎが同じ木の節に無い、クラスR2 の根が木の節に無い、クラスR2 の段階が職業の `ShowTalentStage` に無い、1つの職業・特化に木が2本ある
- 節のバフが `Talents` に無い、1つのバフが同じ木の2つ以上の節にある、クラスR2 の木がある職業にクラスR1 の木が無い(どれも出所ごと)
- 節のタレントが表に無い、`TalentType` が 1 / 4 / 5 のどれでもない、バフのある節がアビリティの節でない、役割のタグが 1〜3 のどれでもない(どれも出所ごと)
- アビリティの節のタレントに、どの言語の名前も無い

## gen_items.py

アイテムの名前を `ItemNames.json` に書く。**鍵はアイテムID**(`ItemTable` の全行、4言語ぶんの和)。値は `ItemTable.Name`。名前の空の鍵も残す。

装備IDもアイテムIDなので、装備の名前もここから引く。チャットのリンクの発言では、魚の名前とアイテムの名前に使う。
名前の出所は上の「土台は言語で分ける」と同じ。

## gen_chat_hyperlinks.py

チャットのリンクの発言(個人スペースの共有など)の文の型と、その文に入るギルドの建物の名前を書く。

- `ChatHyperlinks.json`: **鍵はリンクの番号**(`ChatHyperlink` の全行、4言語ぶんの和)。値は `Content`(文の型)。型の空の鍵も残す
- `UnionBuildingNames.json`: **鍵はギルドの建物の番号**(`UnionBuildingTable` の全行、4言語ぶんの和)。値は `BuildingName`

リンクの発言は本文が空で届き、表の番号と差し込みの値だけを持つ。アプリは型の `{*…*}` を差し込みの値で埋め、
`<u>` `<link=1>` `<style=…>` などの書式を外して出す。型が空か値の取れない差し込みがあれば、その発言は「[リンク]」の文字で出る。

型と名前の出所は上の「土台は言語で分ける」と同じ。
