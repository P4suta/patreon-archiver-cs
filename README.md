# Patreon Archiver (WinUI 3)

> [!IMPORTANT]
> **For personal and educational use only.** This project is not affiliated with Patreon or Cloudflare. You alone are responsible for complying with Patreon's Terms of Service, individual creators' terms, and applicable copyright law. Provided "as is", without warranty — **use at your own risk**. See the [Disclaimer](#disclaimer).
>
> **個人的・教育的な利用のみ。** 本プロジェクトは Patreon / Cloudflare とは無関係です。Patreon 利用規約・各クリエイターの条件・著作権法の遵守は利用者の責任であり、本ソフトウェアは無保証・**自己責任**で提供されます。詳細は[免責事項](#disclaimer)を参照。

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

## License

[MIT](LICENSE). The license covers **this software only** — it grants no rights over any content you download with it. The copyright of any video, image, or other media retrieved through this tool remains with its original creator and is governed by their terms.

本ソフトウェアは [MIT ライセンス](LICENSE)です。ライセンスが対象とするのは**本ソフトウェアのみ**であり、本ツールでダウンロードしたコンテンツに対するいかなる権利も付与しません。取得した動画・画像その他のメディアの著作権は原権利者に帰属し、その条件に従います。

## Disclaimer

**English**

- **No affiliation / trademarks.** This project is not affiliated with, endorsed by, sponsored by, or in any way officially connected to Patreon, Inc. or Cloudflare, Inc. "Patreon", "Cloudflare Stream", and all related names, logos, and trademarks are the property of their respective owners.
- **Intended use.** This software is provided for personal and educational purposes only. It is intended to let users create personal archives of content they are authorized to access (for example, content from creators they actively support). It does **not** facilitate piracy, large-scale scraping, or redistribution of paid content.
- **No DRM circumvention.** This tool does not implement or assist in circumventing any digital rights management (DRM) or technical protection measure. Protected streams (e.g. Widevine, PlayReady, FairPlay) are out of scope and are not handled.
- **Your responsibility.** You are solely responsible for how you use this software. Ensuring that your use complies with Patreon's Terms of Service, the terms set by individual creators, and all applicable laws — including copyright law in your jurisdiction — is entirely your own responsibility. Do not redistribute, share, or commercially exploit downloaded content.
- **No warranty / no liability.** This software is provided "AS IS", under the MIT License, without warranty of any kind, express or implied. The authors and contributors shall not be liable for any claim, damages, data loss, account suspension or termination, or other liability arising from the use of, or inability to use, this software.
- **Use at your own risk.**

**日本語**

- **非提携・商標**: 本プロジェクトは Patreon, Inc. および Cloudflare, Inc. とは一切提携・関連しておらず、これらの企業による承認・出資・支持を受けたものではありません。「Patreon」「Cloudflare Stream」その他の名称・ロゴ・商標は、それぞれの権利者に帰属します。
- **利用目的**: 本ソフトウェアは、個人的かつ教育的な目的でのみ提供されます。利用者が正当にアクセスする権限を持つコンテンツ（例: 自身が支援しているクリエイターのコンテンツ）を、利用者個人がアーカイブする用途を想定しています。海賊行為・大規模スクレイピング・有料コンテンツの再配布を補助するものではありません。
- **DRM 回避の不実施**: 本ツールは、いかなるデジタル著作権管理（DRM）・技術的保護手段の回避も実装・補助しません。保護されたストリーム（例: Widevine、PlayReady、FairPlay）は対象外であり、扱いません。
- **利用者の責任**: 本ソフトウェアの利用にあたっては、利用者が単独で全責任を負います。Patreon の利用規約、各クリエイターが定める条件、および利用者に適用される著作権法その他すべての法令を遵守する責任は、利用者自身にあります。ダウンロードしたコンテンツの再配布・共有・商用利用は行わないでください。
- **無保証・免責**: 本ソフトウェアは MIT ライセンスのもと「現状有姿（"AS IS"）」で提供され、明示黙示を問わずいかなる保証もありません。作者および貢献者は、本ソフトウェアの利用または利用不能から生じるいかなる損害、データの損失、アカウントの凍結・停止、法的責任についても、一切の責任を負いません。
- **自己責任**: 本ソフトウェアの利用は、すべて利用者自身の責任（at your own risk）において行ってください。
