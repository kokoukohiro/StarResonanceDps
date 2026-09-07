# DataTools

`StarResonanceDps.Core/Data/` の生成物を、一次データ（`Star-Unpack`）から作り直すツール。

素の Python 3 だけで動く。依存パッケージは無い。

```bash
cd StarResonanceDps/DataTools
python gen_recount.py     # メーターの行名 + 畳みマッピング + パッシブ別名
python gen_buffs.py       # バフ/デバフウィジェットのバフ名
python gen_skills.py      # 装備中スキル枠のスキル名
python gen_scenes.py      # シーン名
python gen_monsters.py    # モンスター・NPC・訓練用ダミーの名前
```

作業ディレクトリはどこでもよい（`_common.py` が自身の位置からリポジトリを求める）。
`_common.py` はパスと入出力だけを持つ共有モジュールで、単体では実行しない。

## 入力

一次データは **`F:\Claude\StarResonanceDps\Star-Unpack\Ztable\{cn,en,jp,kr}\ZTable\*.json`**。
外側の `Ztable` は t が小文字、その下の `ZTable` は大文字。

**`cn` / `en` は中国サーバー、`jp` / `kr` はアジアサーバーの別ビルドで、テーブルの版が違う。**
行数も収録IDも一致しないので、**言語をまたいで行を突き合わせてはいけない**。
各ツールは言語ごとに自前のフォルダだけで完結させている。

`gen_skills.py` だけは枠の判定に同梱の **`Data/SkillTable.json`**（cn の生ファイル）を使う。
アプリの実行時判定と同じ表を使うため。

## 出力

| ツール | 出力 | 鍵 |
|---|---|---:|
| `gen_recount.py` | `Data/Localization/recount.{4言語}.json` | 2807 |
| | `Data/Mappings/RecountSourceMap.json` | 187 |
| | `Data/Mappings/BuffNameAlias.json` | 18 |
| `gen_buffs.py` | `Data/Localization/buffs.{4言語}.json` | 1606 |
| `gen_skills.py` | `Data/Localization/skills.{4言語}.json` | 161 |
| `gen_scenes.py` | `Data/Localization/scenes.{4言語}.json` | 705 |
| `gen_monsters.py` | `Data/Localization/monsters.{4言語}.json` | 8770 |

## 全ツール共通の決まり

### 鍵は4言語で同じにする

版差で収録IDが違っても、鍵集合を言語で変えない。足りない側は空文字で埋める。

**鍵集合が言語で変わると、表示言語によって畳み込みの停止条件が変わる。**
名前が無い側は実行時に zh-CN へ落ちる（`ResolveText` はキー欠落でも空欄でも zh-CN へ落ちる）。

### 既存の名前は消さない

新しい値が空のときは既存値を残す（`_common.write_localized`）。
テーブルには旧プロジェクトからの移植や実測で入れた名前があり、unpack から素直に
作り直すと失われる。実測で `skills` の zh 16件 / en 5件、`buffs` の zh 15件 / en 4件が該当する。

unpack に値があるときは unpack が勝つ。一次データが正だから。

**例外は `gen_recount.py`。** 総括行（`其他`）の空欄は意図した状態なので、この規則を当てない。

### 手修正は上書きファイルへ。生成物を直接編集しない

`skills` / `buffs` / `scenes` は上の「既存の名前は消さない」規則があるので、
**生成物を直接編集してよい**（次に流しても消えない）。

**`recount` だけは例外。** 総括行の空欄が意味を持つので既存値の維持ができず、
`gen_recount.py` は毎回まるごと書き直す。手修正は別ファイルに置く。

    Data/Overrides/RecountOverrides.json

```json
{
  "50037": { "en-US": "Countercrush (Counter Storm)" }
}
```

| | |
|---|---|
| キー | 発生源ID |
| 値 | 言語 → 名前。**言語と値だけ。他のキーは置かない** |
| 書いた言語だけ差し替わる | 書かない言語は生成値のまま |
| **空文字** | **生成値を消す。** 以後は通常どおり zh-CN へ落ちる |

上の例は実際に入っている1件。`50037` の en は生成物では `Countercrush` で `1930` と衝突するため、
ZDPS（`BPSR-ZDPS-master/BPSR-ZDPS/Data/SkillOverrides.en.json`）の値に差し替えている。

ZDPS には `BuffOverrides.en.json`（`Countercrush Triggered`）にも値があるが、採ったのは
`SkillOverrides.en.json` のほう。**どちらを採るかは実物を見て決めること。**

**畳み込み後のID（行キー）に書くこと。** 畳まれる側に書いても表示されないので、
読み込み時に警告を出す。

```
[WRN] RecountOverrides: 149904 は 1424 へ畳まれるので効かない。1424 に書くこと
[ERR] RecountOverrides: 3210051 に未知のキー "jp"。言語は en-US / ja-JP / ko-KR / zh-CN
```

**言語名の打ち間違いは黙って無視しない。** 静かに効かないのが一番困る失敗なので、
未知のキーはログにエラーを出して飛ばす。**入れたら必ずログを見ること。**

**値は実物を確認してから入れる。** ゲーム内表示・スクリーンショットなど根拠のあるものだけ。
埋まっていないことは見れば分かるが、それらしい値が入っていると誤りに気付けない。

### プレースホルダは名前として扱わない

`场地标记01` / `气刃突刺计数` はゲーム側の未翻訳プレースホルダ。空として扱う。

---

## gen_recount.py

ゲーム内メーターの見出し表 `RecountTable` から、発生源ID → 行名と、行畳みマッピングを作る。

### 鍵の作り方

```
RecountTable.DamageId → DamageAttrTable.TypeEnum → そのまま鍵
```

**`TypeEnum` を生のまま使う。親スキルへ畳んではいけない。**
弾IDの `TypeEnum` を親へ寄せると弾IDが鍵から消え、別途「弾ID規則」が必要になる。
生のまま入れれば弾IDも直接載り、規則が要らなくなる。

実測（ログ188本 109万イベント）で **99.91%** が表に直接当たる。
残る0.09%（18種）は弾の親を持たないモンスタースキルで、名前もプレースホルダ。

### 総括行は名前を空にする

`其他` / `Other` / `その他` / `기타` の行は**鍵の8割（2175件）**を占め、個別の名前を持たない。
抱えるIDが突出して多い行として特定し、**名前で検算している**（一致しなければ例外で止まる）。

畳み込みの対象にもしない。

### 行畳みマッピング

3つの規則で作る。**手書きの判断は `CONFIRMED_SAME` の1件だけ。**

| 規則 | 件数 |
|---|---:|
| 同じ行に属するIDをその行の最若IDへ寄せる | 181 |
| 同名の別行を寄せる | 4 |
| 同じ行のパッシブ同士を寄せる | 1 |
| ゲーム内で同じものと確認した組（`CONFIRMED_SAME`） | 1 |

行の構成は **zh-CN（最新版）を権威**にし、そこに無いIDだけ ja-JP で補う。

#### 同名の別行を畳む条件

ゲーム内メーターは同名の行を2つ出さない。同名＝同じダメージソース。ただし2つ条件がある。

1. **全言語で名前が食い違わないこと。** ja/ko だけ同名（`刹那`）や en だけ同名
   （`Countercrush`）は訳が足りていないだけで別物。畳むと表示言語で集計の粒度が変わる。
   片方だけ名前が無いのは「食い違い」ではないので畳む（`1432` / `31901`）。
2. **3行以上に出る名前は畳まない。** 各職ブロックの先頭に繰り返し出る見出しで、職ごとの別物。
   実測で該当は `幸运一击`（9行）と `红光反制攻击`（8行）だけ。他は全部2行。

畳み先は**「名前を持つ言語が最も多いID」。同数なら最若ID。**
単純に最若へ寄せると、名前の無いIDが畳み先になって行名が消える（`1432` は zh/en に名前が無い）。

#### `CONFIRMED_SAME` — 手で維持する

自動判定に載らない組。**再生成しても消えないよう、ここに残すこと。**

| 組 | 判断 | 根拠 |
|---|---|---|
| `149904` → `1424` | 畳む | ゲーム内で同じもの。`149904` は `1424` の連撃段 |
| `1930` / `50037` | **畳まない** | ゲーム内で別物。`50037` の ja `レジスト反撃` はスクリーンショットで確認した値で、`1930` は別行の `護刃の衝撃` |

#### 連鎖を潰す

`55356 → 55355`（行畳み）→ `21427`（同名畳み）の2段が起きる。
**実行時は1ホップしか引かない**ので、生成時に平坦化する（残っていれば例外で止まる）。

### BuffNameAlias.json

イマジンのパッシブに接尾辞を付けるIDの一覧。**このツールは自身の出力を絞り込む形で維持する。**

見出し表に載るIDだけを残し、載らないIDを落とす（89件 → 18件）。
**元の一覧を作り直す手段は無いので、このファイルを消さないこと。**
消えていれば例外で止まる。

パッシブは行の上では本体イマジンと同じ行に居るが、こちらは分離して出す仕様なので
`RecountSourceMap` から除外し、代わりに接尾辞（`Data/Mappings/NameSuffixes.json`）を足す。

---

## gen_buffs.py

**`BuffPriority != NotShow(0)` のバフだけ**を収録する。

ゲーム内のバフバー（`Abnormal_stateView`）が
`buffVm:GetEntityBuffList(entity, EBuffPriority.NotShow, ShowBuffCountMax)` を呼び、
`NotShow` のバフを出さないため。自分のバーもボスHPバーのバーも同じ関数で、対象が違うだけ。

```
EBuffPriority = { NotShow=0, Secondly=1, Highest=2, Notice=3, NoticeAndTeamShow=4 }
分布           P0=9821 / P1=558 / P2=338 / P3=682 / P4=28   ※P1〜4 は全件アイコンあり
```

cn は1606件、アジア版は1414件（192件はアジア版に存在しない）。鍵は和集合。

> **表示条件そのものはこのファイルで判定しない。**
> 同梱 `Data/BuffTable.json`（cn）で判定する（`MeterSnapshotProvider.IsHiddenFromBuffBar`）。
> 言語別テーブルで判定すると**日本語利用者だけ192件表示されなくなる**。

---

## gen_skills.py

**枠に置けるIDだけ**を収録する（5275 → 161件）。

メーターの行名は見出し表が決めるので、このテーブルに要るのはスキル枠ウィジェットが
名前を引くIDだけ。枠ウィジェットはイマジンとロールしか表示しない
（`ResolvePlayerSkillLevels` も `CreateSelfActionBarLoadout` もクラススキルを通す前に落とす）。

```
イマジン  Skill.IsImagineSlot() = SlotPositionId に 7 か 8   → 141件
ロール    Skill.IsRoleSlot()    = SlotPositionId に 21〜24   →  20件
                                  職務専用12 + 全職務共通 3021〜3028 の8
```

**イマジンは `39xx` 帯の95件だけではない。** `2350` `3000` `1002xxx` `2900xxx` 等も
同じ枠判定に入るので、落とすと枠に名無しで出る。

---

## gen_scenes.py

`SceneTable.Name` が本体。`SceneTable` に無いシーンIDだけ `DungeonsTable` で補う
（実測で該当は `1721 弥妄·流月之野` の1件）。

cn/en の `SceneTable` は704件、アジア版は612件。アジア版に無い行は空文字で入り、
表示時に zh-CN へ落ちる。

---

## gen_monsters.py

エンティティリストやメーターで `AttrId` から表示名を引くのに使う。

鍵は **`MonsterTable` ∪ `DummyTable` ∪ `NpcTable`** の和集合（4言語ぶんの和で8770件）。
名前は**この順**で「本物の名前」を持つ最初の表から取る。同じIDが複数の表にあって
名前が食い違うことがあるので順序が要る（実測148件）。

```
52   DummyTable=共鸣技能卷心菜法师弱  NpcTable=西尔维   → NpcTable
108  MonsterTable=寒霜食人魔         DummyTable=雷     → MonsterTable
```

> **`MonsterTable` を丸ごと差し替えてはいけない。新しい unpack のほうが名前を失う。**
> 3表で埋まるのは zh-CN で6938件だが、テーブルにはそれより472件多い名前が入っている
> （`变身专用-虚蚀蒂娜` など、いまの unpack では空になっているもの）。
> 「既存の名前は消さない」規則がこれを守っている。**既存の値を捨てて作り直さないこと。**

en-US は776件、ja-JP / ko-KR は85件が同じく既存値の維持で残る。

---

## 生成しないもの

`Data/` 直下の生テーブル（`SkillTable.json` `BuffTable.json` 等）は
**`Star-Unpack/Ztable/cn/ZTable/` の生ファイルをそのまま置く**決まりで、加工しない。
