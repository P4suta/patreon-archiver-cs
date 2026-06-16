# Patreon Archiver (WinUI 3)

[P4suta/patreon-archiver](https://github.com/P4suta/patreon-archiver)（Python + Docker の CLI）を、**self-contained な C# WinUI 3 デスクトップアプリ**として作り直したもの。Docker 不要・GUI 付き・インストール不要のポータブル配布。

- アプリ内蔵の **WebView2** で Patreon にログイン → クリエイターページを取得（MHTML 手保存が不要）
- **yt-dlp + ffmpeg を同梱**（インストール直後から動作）
- 状態は **SQLite**、差分同期・coverage アンカー・ギャップ検出を原典から忠実移植
- **Fluent Design 全乗っかり**（Mica / dark-first / NavigationView / SettingsCard）

## アーキテクチャ

```
PatreonArchiver.sln
├─ src/PatreonArchiver.Core   … headless ドメイン/サービス（hexagonal、UI 非依存、69 単体テスト）
│   Domain / Abstractions(ポート) / Parsing / Resolving / Downloading / Publishing / Persistence / Sync
└─ src/PatreonArchiver.App    … WinUI 3 head（MVVM + Generic Host DI、Mica + NavigationView）
    Views(Browse/Library/Downloads/Sync/Settings) / ViewModels / Services(SyncCoordinator/AppSettings) / tools(同梱 exe)
tests/PatreonArchiver.Core.Tests … xUnit
```

依存は常に内向き（App → Core → 外部ライブラリ）。バージョンは Central Package Management（`Directory.Packages.props`）で一元管理。

## ツールチェーン（mise）

このリポジトリは **mise** でツールを管理します（`mise.toml` で .NET SDK を固定）。dotnet は mise 経由で実行してください:

```sh
mise install                       # mise.toml のツールを用意
mise exec -- dotnet --version      # 例: 10.0.300
```

## ビルド & 実行

```sh
# 1. 同梱する DL エンジン（yt-dlp / ffmpeg / ffprobe）を取得（初回のみ）
pwsh ./src/PatreonArchiver.App/tools/fetch-tools.ps1

# 2. 単体テスト
mise exec -- dotnet test

# 3. 開発実行
mise exec -- dotnet run --project src/PatreonArchiver.App

# 4. ポータブル配布物を発行（unpackaged self-contained）
mise exec -- dotnet publish src/PatreonArchiver.App -c Release -r win-x64
#   → .../bin/Release/.../win-x64/publish/ の PatreonArchiver.App.exe を直接起動。
#     インストール不要・Developer Mode 不要・.NET/WinAppSDK の別途インストール不要。
```

> 配布は **unpackaged self-contained**（`WindowsPackageType=None` + `WindowsAppSDKSelfContained` + `SelfContained`）。
> `Package.appxmanifest` は温存しており、将来 packaged MSIX 配布へ切り替え可能。

## 使い方

1. **Browse** — Patreon にログインし、クリエイターの投稿ページを開いて「このページを取得」。Cookie 書き出しも可能。
2. **Sync** — クリエイターごとに「保留を取得」で未取得の投稿をダウンロード。coverage/ギャップ状態を表示。
3. **Downloads** — 進行中の同期の進捗と履歴。
4. **Library** — アーカイブ済み動画のグリッド。再生・フォルダを開く。
5. **Settings** — polite/fast プリセット、テーマ、エンジンのバージョン、保存先。

## 動作要件

- Windows 10 1809+ / 11（WebView2 ランタイム: Win11 はプリインストール、Win10 は要 Evergreen ランタイム）
- 開発時のみ .NET 10 SDK（mise が供給）

## 免責事項（Disclaimer）

- **非提携・商標**: 本プロジェクトは Patreon, Inc. および Cloudflare, Inc. とは一切提携・関連しておらず、これらの企業による承認・出資・支持を受けたものではありません。「Patreon」「Cloudflare Stream」その他の名称・ロゴ・商標は、それぞれの権利者に帰属します。
- **利用目的**: 本ソフトウェアは、個人的かつ教育的な目的でのみ提供されます。利用者が正当にアクセスする権限を持つコンテンツ（例: 自身が支援しているクリエイターのコンテンツ）を、利用者個人がアーカイブする用途を想定しています。
- **利用者の責任**: 本ソフトウェアの利用にあたっては、利用者が単独で全責任を負います。Patreon の利用規約、各クリエイターが定める条件、および利用者に適用される著作権法その他すべての法令を遵守する責任は、利用者自身にあります。ダウンロードしたコンテンツの再配布・共有・商用利用は行わないでください。
- **無保証・免責**: 本ソフトウェアは MIT ライセンスのもと「現状有姿（"AS IS"）」で提供され、明示黙示を問わずいかなる保証もありません。作者および貢献者は、本ソフトウェアの利用または利用不能から生じるいかなる損害、データの損失、アカウントの凍結・停止、法的責任についても、一切の責任を負いません。
- **自己責任**: 本ソフトウェアの利用は、すべて利用者自身の責任（at your own risk）において行ってください。
