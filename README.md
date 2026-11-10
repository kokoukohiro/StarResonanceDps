# StarResonanceDps

『ブループロトコル：スターレゾナンス』の戦闘情報(DPSなど)を表示する非公式のツールです(Windows用)。

[English](#english)

![DPSメーター](docs/wiki/images/DpsMeter1.png)

戦闘の状況をリアルタイムで表示し、過去の戦闘の結果も振り返ることができます。戦闘中のできごとの通知や、機能の拡張にも対応しています。

ゲームのクライアントを改変せず、戦闘のデータを外部へ送信することもありません。表示される数値を、他のプレイヤーへの誹謗中傷など、コミュニティの環境を損なう目的で使用しないでください。

## 必要なもの

- Windows(64ビット)
- [.NET 10デスクトップランタイム](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Npcap](https://npcap.com/)

## 入れ方

1. [リリース](https://github.com/kokoukohiro/StarResonanceDps/releases)から`StarResonanceDps-<バージョン>.zip`を取得します
2. zipを、書き込みのできるフォルダへ展開します
3. フォルダの中の`StarResonanceDps.exe`を起動します

- 設定と戦闘の履歴は、このフォルダの中に保存します
- 最初の起動で、Windowsの警告(SmartScreen)が出ることがあります。「詳細情報」を押すと「実行」を選べます
- ホットキーを使うための補助プログラムを起動するときに、ユーザーアカウント制御の確認が出ます。使わない場合は、全体設定の起動設定で「アプリ起動時にホットキー補助プログラムを起動」をオフにしてください
- 起動した後にすることは、[最初の設定](https://github.com/kokoukohiro/StarResonanceDps/wiki/最初の設定)にあります

## 更新

アプリの中から、新しいバージョンへ更新できます。新しいバージョンがあると、起動時に確認の窓が出ます。「ヘルプ」の「更新を確認」を押しても確かめられます。詳しくは[入れ方と更新](https://github.com/kokoukohiro/StarResonanceDps/wiki/入れ方と更新)にあります。

## 使い方

画面と設定の説明は、[Wiki](https://github.com/kokoukohiro/StarResonanceDps/wiki)にあります。

## プラグイン

プラグインで機能を足せます。プラグインはアプリの中で動き、アプリと同じことができます。入れるのは信頼できる配布元のものだけにしてください。

作り方は[プラグイン開発](https://github.com/kokoukohiro/StarResonanceDps/wiki/プラグイン開発)にあります。SDKのパッケージ(`.nupkg`)は、リリースにアプリのzipと並んでいます。

## 不具合の報告と要望

アプリの「フィードバック」から、GitHubのIssueへ投稿できます。投稿にはGitHubのアカウントが必要です。

## 通信

入れたプラグインを除くと、アプリが外部へ接続するのは次のときだけです。戦闘のデータは送りません。

- 更新の確認とダウンロード(GitHub)
- 「フィードバック」で報告済みかを確かめるとき(GitHub)
- 「プレイヤー情報」で顔と名刺の写真を表示するとき(ゲームから届いた写真のURL)

## ライセンス

- 本体はGNU GPL v3(追加の許可あり)、SDK・解析器・サンプル・文書はMITです。詳しくは[LICENSING.md](LICENSING.md)にあります
- 使っているライブラリのライセンスは、[THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt)にあります
- このツールは非公式で、ゲームの開発・運営とは関係ありません。ゲームの名前・データ・画像の権利は、それぞれの権利者にあります

## English

An unofficial tool that shows combat information (such as DPS) for Blue Protocol: Star Resonance (Windows).
It shows combat in real time and lets you look back on past battles. It also supports notifications for in-combat events and extensions.
It does not modify the game client or send combat data anywhere. Do not use the displayed numbers to defame other players or otherwise harm the community.

- Requirements: 64-bit Windows, [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) and [Npcap](https://npcap.com/)
- Install: download `StarResonanceDps-<version>.zip` from [Releases](https://github.com/kokoukohiro/StarResonanceDps/releases), extract it to a folder you can write to, and run `StarResonanceDps.exe`. Settings and history are saved in that folder.
- Update: the app updates itself ("Help" > "Check for updates").
- Network: apart from installed plugins, the app connects to the internet only to check for and download updates (GitHub), to check whether items in "Feedback" have been reported (GitHub), and to show profile and card photos in "Player Info" (photo URLs sent by the game). It does not send combat data.
- Languages: the app is available in Japanese, Korean, Chinese and English. The documentation ([Wiki](https://github.com/kokoukohiro/StarResonanceDps/wiki)) is in Japanese only.
- Feedback: report bugs and requests from "Feedback" in the app (a GitHub account is required).
- Plugins: plugins run inside the app and can do anything the app can. Install only plugins from sources you trust.
- License: the app is under the GNU GPL version 3 with additional permissions; the plugin SDK, the analyzers, the samples and the documentation are under the MIT License. See [LICENSING.md](LICENSING.md) and [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt).
- This is an unofficial tool and is not affiliated with the developers or operators of the game. The names, data and images of the game belong to their respective owners.
