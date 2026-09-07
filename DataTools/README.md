# DataTools

`StarResonanceDps.Core/Data/` に置く名前テーブル類を、言語別の JSON テーブルから作り直すツール。

素の Python 3 だけで動く。依存パッケージは無い。

```bash
cd StarResonanceDps/DataTools
python gen_recounts.py    # メーターの行名
python gen_buffs.py       # バフ/デバフウィジェットのバフ名
python gen_skills.py      # 装備中スキル枠のスキル名
python gen_scenes.py      # シーン名
python gen_monsters.py    # モンスター・NPC・訓練用ダミーの名前
```

作業ディレクトリはどこでもよい（`_common.py` が自身の位置からリポジトリを求める）。
`_common.py` はパスと入出力だけを持つ共有モジュールで、単体では実行しない。

## 入力

**言語別の JSON テーブル一式が要る。リポジトリには含まれない。** 各自で用意する。

期待する構成は「言語フォルダ → `ZTable` → `*.json`」。

```
JSONS/
  cn/ZTable/*.json     zh-CN
  en/ZTable/*.json     en-US
  jp/ZTable/*.json     ja-JP
  kr/ZTable/*.json     ko-KR
```

既定ではリポジトリの隣の `JSONS` を見る。別の場所に置くなら環境変数で指す。

```bash
set BPSR_TABLES=<置き場所>        # Windows
export BPSR_TABLES=<置き場所>     # bash
```

読むファイルは各ツールの節に挙げたものだけ。無ければどこを設定すればよいかを示して止まる。
**パスの決定は `_common.py` の1か所で、各ツールは触らない。**

**言語ごとにテーブルの版が違うことがある。** 行数も収録IDも一致しないので、
**言語をまたいで行を突き合わせない**。各ツールは言語ごとに自前のフォルダだけで完結させている。

**各ツールが読むのは入力側の JSON だけ。** 手動編集ファイルは読みも書きもしない。

## 出力

| ツール | 出力 |
|---|---|
| `gen_recounts.py` | `Data/Localization/recounts.{4言語}.json` |
| `gen_buffs.py` | `Data/Localization/buffs.{4言語}.json` |
| `gen_skills.py` | `Data/Localization/skills.{4言語}.json` |
| `gen_scenes.py` | `Data/Localization/scenes.{4言語}.json` |
| `gen_monsters.py` | `Data/Localization/monsters.{4言語}.json` |

## 全ツール共通の仕様

### 鍵は4言語で同じにする

**鍵は4言語の和集合。** 版差で収録IDが違っても、鍵集合は言語で変えない。
足りない側は空文字で埋める。

**鍵集合が言語で変わると、表示言語によって畳み込みの停止条件が変わってしまう。**
名前が無い側は実行時に zh-CN へ落ちる（`ResolveText` はキー欠落でも空欄でも zh-CN へ落ちる）。

**zh-CN 自身に名前が無い鍵はどの言語でも空になる。** 和集合なので、
他の言語にしか無いIDが入り得る。落ちる先が空なので、そこは埋まらない。
埋めたければ `RecountOverrides.json` に書くか、生成物を直接編集する。

### 完全な上書き

**出力は入力だけで決まる。前の出力は読まない。** 入力に名前が無ければ空になる。

生成物を直接編集しても次に流せば消える。残したい手修正は上書きファイルへ置く。

### プレースホルダは名前として扱わない

未翻訳の行に入っている埋め草文字列（`_common.PLACEHOLDERS`）は空として扱う。

## 手動編集ファイル

**ツールが読みも書きもしないファイル。** 生成物とは独立していて、再生成しても消えない。

| ファイル | 中身 |
|---|---|
| `Data/Mappings/RecountSourceMap.json` | 発生源ID → 畳み先ID |
| `Data/Mappings/BuffNameAlias.json` | 行名に接尾辞を付けるID |
| `Data/Mappings/NameSuffixes.json` | 接尾辞の4言語 |
| `Data/Overrides/RecountOverrides.json` | `recounts` の名前の差し替え |
| `Data/Overrides/BuffOverrides.json` | 同梱 `Data/BuffTable.json` の項目の差し替え |

### RecountSourceMap.json

同じダメージソースとして1行にまとめるIDを書く。

```json
{ "<発生源ID>": <畳み先ID> }
```

**畳み先をさらに畳まない。** 実行時は1ホップしか引かないので、
`X → Y` と `Y → Z` を両方書くと `X` は `Y` で止まる。

### BuffOverrides.json

同梱の `Data/BuffTable.json` を読むときに重ねる。名前ではなく**項目**の差し替え。

```json
{ "<バフID>": { "Id": "<バフID>", "BuffType": <値>, "Icon": "<アイコン名>" } }
```

`BuffType` と `Icon` を、必要なほうだけ書く。生の値をそのまま使うとバフとデバフの
振り分けが実行時と食い違うため、**振り分けは実質このファイルが決めている。**

**IDだけの項目を置かない。** 差し替える中身が無くても、そのIDが登録されている扱いになる。

### RecountOverrides.json

生成物は流すたびに書き直されるので、名前の手修正はこちらへ置く。

```json
{
  "<発生源ID>": { "<言語>": "<名前>" }
}
```

| | |
|---|---|
| キー | 発生源ID |
| 値 | 言語 → 名前。**言語と値だけ。他のキーは置かない** |
| 書いた言語だけ差し替わる | 書かない言語は生成値のまま |
| **空文字** | **生成値を消す。** 以後は通常どおり zh-CN へ落ちる |

**生成物に無いIDも書ける。** 鍵が無ければ新しく足される。

**畳み込み後のID（行キー）に書く。** 畳まれる側に書いても表示されないので、
読み込み時に畳み先を示す警告が出る。

**言語名の打ち間違いは黙って無視されない。** 静かに効かないのが一番困る失敗なので、
未知のキーはログにエラーが出て飛ばされる。入れたらログを確認する。

---

## gen_recounts.py

メーターの行の見出しを持つ表から、発生源ID → 行名を作る。

### 鍵の作り方

```
RecountTable.DamageId → DamageAttrTable.TypeEnum → そのまま鍵
```

**`TypeEnum` を生のまま使う。親スキルへ畳まない。**
弾IDの `TypeEnum` を親へ寄せると弾IDが鍵から消え、別途「弾ID規則」が必要になる。
生のまま入れれば弾IDも直接載るので規則が要らない。

### 総括行は名前を空にする

「その他」に当たる行は鍵の大半を占め、個別の名前を持たない。
抱えるIDが突出して多い行として特定し、**名前で検算している**（一致しなければ例外で止まる）。

畳み込みの対象にもしない。

---

## gen_buffs.py

**`BuffPriority` が 0 のバフは収録しない。** ゲーム内のバフ表示と対象をそろえるため。

言語によって収録数に差があるので、鍵は和集合。

> **表示条件そのものはこのファイルで判定しない。**
> 同梱 `Data/BuffTable.json` で判定する（`MeterSnapshotProvider.IsHiddenFromBuffBar`）。
> 言語別テーブルで判定すると、片方にしか無いバフが特定の表示言語でだけ出なくなる。

---

## gen_skills.py

**枠に置けるIDだけ**を収録する。

メーターの行名は見出し表が決めるので、このテーブルに要るのはスキル枠ウィジェットが
名前を引くIDだけ。枠ウィジェットはイマジンとロールしか表示しない
（`ResolvePlayerSkillLevels` も `CreateSelfActionBarLoadout` もクラススキルを通す前に落とす）。

```
イマジン  Skill.IsImagineSlot() = SlotPositionId に 7 か 8
ロール    Skill.IsRoleSlot()    = SlotPositionId に 21〜24
```

**判定はこの枠番号だけで行う。** ID帯で絞ると、同じ枠に置けるのに範囲から外れるIDが
名無しで出る。

枠の判定は言語ごとに行い、鍵はその和集合。版差でどれか1言語にしか無い枠でも落とさない。

---

## gen_scenes.py

`SceneTable.Name` が本体。`SceneTable` に無いシーンIDだけ `DungeonsTable` で補う。

言語によって収録数に差がある。無い行は空文字で入り、表示時に zh-CN へ落ちる。

---

## gen_monsters.py

エンティティリストやメーターで `AttrId` から表示名を引くのに使う。

鍵は **`MonsterTable` ∪ `DummyTable` ∪ `NpcTable`** の和集合（4言語ぶんの和）。
名前は**この順**で「本物の名前」を持つ最初の表から取る。
同じIDが複数の表にあって名前が食い違うことがあるので順序が要る。

`MonsterTable` は行があっても `Name` が空のことがあり、その数は言語で大きく違う。
埋まらないIDは入力側に名前が無いだけで、他の表にも代わりは無い。
