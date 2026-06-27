<#
.SYNOPSIS
    Assemble the distributable bundle for a given runtime, then zip + checksum it.

.DESCRIPTION
    Produces the "open the folder, one obvious thing to run" layout under dist/:

        dist/PatreonArchiver/
        |- PatreonArchiver.exe     <- the single launcher a user double-clicks
        |- README.txt              <- bilingual "double-click PatreonArchiver.exe"
        \- app/                    <- the whole self-contained runtime, out of sight
           |- PatreonArchiver.App.exe   (the real app the launcher spawns)
           |- tools/ (yt-dlp / ffmpeg / ffprobe)
           \- *.dll, locales, ...

    Then packages it as dist/PatreonArchiver-<version>-<rid>.zip and records the
    SHA-256 in dist/SHA256SUMS.txt. Mirrors the find-my-files xtask publish/package
    flow (xtask/src/{publish,package,locale}.rs) in a single script so the bundle is
    reproducible locally and in release.yml.

.PARAMETER Rid
    Target runtime identifier (win-x64 or win-arm64).

.PARAMETER Version
    Release version for the zip name, e.g. v0.1.0. Defaults to v0.0.0-dev for
    workflow_dispatch / local runs that aren't a tag.

.EXAMPLE
    pwsh eng/pack.ps1 -Rid win-x64 -Version v0.1.0
#>
[CmdletBinding()]
param(
    [ValidateSet('win-x64', 'win-arm64')]
    [string]$Rid = 'win-x64',

    [ValidatePattern('^v\d+\.\d+\.\d+')]
    [string]$Version = 'v0.0.0-dev',

    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# --- paths ----------------------------------------------------------------
$repo = Split-Path -Parent $PSScriptRoot
$dist = Join-Path $repo 'dist'
$bundle = Join-Path $dist 'PatreonArchiver'      # zip contents
$app = Join-Path $bundle 'app'                   # the runtime payload
$appProj = Join-Path $repo 'src/PatreonArchiver.App/PatreonArchiver.App.csproj'
$launcherProj = Join-Path $repo 'src/PatreonArchiver.Launcher/PatreonArchiver.Launcher.csproj'

# Files whose presence means the bundle can actually launch (self-verify gate).
$required = @(
    'app/PatreonArchiver.App.exe',
    'app/tools/yt-dlp.exe',
    'PatreonArchiver.exe'
)
# WinAppSDK self-contained publish drops ~85 locale dirs; keep only what ships.
$keepLocales = @('en-us', 'ja-JP')

# NativeAOT links via MSVC; the ILCompiler finds the VC toolset with vswhere.exe.
# Locally that's only on PATH inside a VS Developer shell, so add the standard
# Installer dir ourselves — keeps `pwsh eng/pack.ps1` working from a plain shell.
# (CI's windows-latest already has it; the prepend is harmless there.)
if (-not (Get-Command vswhere.exe -ErrorAction SilentlyContinue)) {
    $installer = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer"
    if (Test-Path (Join-Path $installer 'vswhere.exe')) {
        $env:PATH = "$installer;$env:PATH"
    }
}

function Invoke-Dotnet {
    param([string[]]$Arguments)
    Write-Host "> dotnet $($Arguments -join ' ')" -ForegroundColor DarkGray
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet $($Arguments -join ' ') failed ($LASTEXITCODE)" }
}

# --- 1) clean the staging bundle (best-effort: a running app can lock it; the
#        publish below overwrites and the self-verify is the real gate) --------
if (Test-Path $bundle) {
    try { Remove-Item -Recurse -Force $bundle }
    catch { Write-Warning "could not fully clean $bundle ($_); publishing over leftovers" }
}
New-Item -ItemType Directory -Force -Path $app | Out-Null

# --- 2) publish the self-contained app INTO app/ ---------------------------
# Reserve the bundle root for the launcher + README so "which exe do I run" is
# obvious. Mirrors release.yml's previous publish invocation.
$appArgs = @('publish', $appProj, '-c', $Configuration, '-r', $Rid, '-o', $app)
# In CI, build the shipped bundle from the pinned dependency graph (reproducible
# supply chain) — only when a lock file exists, so a lockless repo still builds.
if ($env:GITHUB_ACTIONS -eq 'true' -and (Test-Path (Join-Path (Split-Path $appProj) 'packages.lock.json'))) {
    $appArgs += '-p:RestoreLockedMode=true'
}
Invoke-Dotnet $appArgs

# --- 3) publish the NativeAOT launcher, copy PatreonArchiver.exe to the root --
$launcherOut = Join-Path $repo "src/PatreonArchiver.Launcher/bin/$Configuration/pack-$Rid"
Invoke-Dotnet @('publish', $launcherProj, '-c', $Configuration, '-r', $Rid, '-o', $launcherOut)
Copy-Item (Join-Path $launcherOut 'PatreonArchiver.exe') (Join-Path $bundle 'PatreonArchiver.exe') -Force

# --- 4) prune unshipped WinAppSDK locale dirs ------------------------------
# A name shaped like a BCP-47 locale folder (e.g. fr-FR, zh-Hant) that isn't in
# the keep-list. Bare-language .NET satellite dirs (de, es) have no hyphen
# segment and so don't match — left untouched, matching find-my-files.
$localeRe = '(?i)^[a-z]{2,3}(-[A-Za-z0-9]+){1,3}$'
Get-ChildItem -Path $app -Directory | Where-Object {
    $_.Name -match $localeRe -and ($keepLocales -notcontains $_.Name)
} | ForEach-Object { Remove-Item -Recurse -Force $_.FullName }

# --- 5) README.txt at the bundle root (CRLF + UTF-8 BOM so Notepad renders the
#        Japanese half too) ---------------------------------------------------
$readme = @'
PatreonArchiver - archive the Patreon you support, on Windows
=============================================================

>> To start: double-click  PatreonArchiver.exe  (here, in this folder).

That's it. The app and all of its runtime files live in the  app\  subfolder,
so this folder root stays clean - PatreonArchiver.exe is the one thing to run.
The whole folder is portable: copy it anywhere, or delete it, freely.

Bundled, inside  app\tools\ :
  yt-dlp.exe / ffmpeg.exe / ffprobe.exe  - the download engine (used by the app)

Keep PatreonArchiver.exe next to the  app\  folder; don't move the .exe out on
its own. Windows 10 1809+ / Windows 11, x64 or ARM64.

-------------------------------------------------------------------------------

PatreonArchiver - 支援している Patreon を Windows でアーカイブ
=============================================================

>> 起動: このフォルダーの  PatreonArchiver.exe  をダブルクリック。

これだけです。アプリ本体と実行ファイル群は  app\  サブフォルダーにまとまって
いるので、フォルダー直下は散らからず、起動するのは PatreonArchiver.exe ひとつ
だけ。フォルダーごとどこへでもコピーでき、削除も自由なポータブル構成です。

同梱ツール( app\tools\ 内):
  yt-dlp.exe / ffmpeg.exe / ffprobe.exe  - ダウンロードエンジン(アプリが使用)

PatreonArchiver.exe は  app\  フォルダーと同じ場所に置いたまま、.exe だけを
移動しないでください。対応: Windows 10 1809 以降 / Windows 11、x64 または ARM64。
'@
$readmeCrlf = ([char]0xFEFF) + ($readme -replace "`r?`n", "`r`n")
[System.IO.File]::WriteAllText((Join-Path $bundle 'README.txt'), $readmeCrlf, (New-Object System.Text.UTF8Encoding($true)))

# --- 6) self-verify: the producer guarantees a runnable bundle -------------
$missing = $required | Where-Object { -not (Test-Path (Join-Path $bundle $_)) }
if ($missing) { throw "bundle is missing $($missing -join ', ') - it would not launch" }

# --- 7) package: zip the bundle contents + record the SHA-256 --------------
$zipName = "PatreonArchiver-$Version-$Rid.zip"
$zipPath = Join-Path $dist $zipName
if (Test-Path $zipPath) { Remove-Item -Force $zipPath }
Compress-Archive -Path (Join-Path $bundle '*') -DestinationPath $zipPath -Force

$hash = (Get-FileHash -Algorithm SHA256 $zipPath).Hash   # uppercase hex
$sumsPath = Join-Path $dist 'SHA256SUMS.txt'
# Accumulate across arches (each release.yml job uploads its own; the release
# job concatenates them): replace any stale line for this zip, then re-sort.
$lines = @()
if (Test-Path $sumsPath) {
    $lines = Get-Content $sumsPath | Where-Object { $_ -and ($_ -notmatch [regex]::Escape($zipName)) }
}
$lines += "$hash  $zipName"
($lines | Sort-Object) -join "`n" | Set-Content -NoNewline -Path $sumsPath -Encoding ascii

Write-Host ""
Write-Host "packaged $zipName" -ForegroundColor Green
Write-Host "  bundle : $bundle  (root = PatreonArchiver.exe + README.txt + app\)"
Write-Host "  zip    : $zipPath"
Write-Host "  sha256 : $hash"
