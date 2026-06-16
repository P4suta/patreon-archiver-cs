#Requires -Version 5
<#
.SYNOPSIS
  Fetches the bundled download engine (yt-dlp + ffmpeg/ffprobe) into this folder.
  These binaries are git-ignored and must be present for the app to build/run.
#>
$ErrorActionPreference = 'Stop'
$dir = $PSScriptRoot

Write-Host 'Downloading yt-dlp.exe...'
Invoke-WebRequest -Uri 'https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe' `
    -OutFile (Join-Path $dir 'yt-dlp.exe')

Write-Host 'Downloading ffmpeg (essentials)...'
$zip = Join-Path $env:TEMP 'pa-ffmpeg.zip'
$extract = Join-Path $env:TEMP 'pa-ffmpeg-extract'
Invoke-WebRequest -Uri 'https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip' -OutFile $zip
Expand-Archive -Force -Path $zip -DestinationPath $extract

Get-ChildItem -Path $extract -Recurse -Include 'ffmpeg.exe', 'ffprobe.exe' |
    ForEach-Object { Copy-Item -Force -Path $_.FullName -Destination (Join-Path $dir $_.Name) }

Remove-Item -Recurse -Force -Path $zip, $extract
Write-Host 'Done. Bundled tools are in' $dir
