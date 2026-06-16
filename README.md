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
