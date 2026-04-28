# Downloads + extracts a portable Windows Terminal release into a per-version
# cache under the user profile. Used by wintty-bench so the harness can launch
# `wt.exe` as a fully isolated process+window -- the Microsoft Store install is
# tied to a single AppX identity per user, which causes new spawns to open as
# tabs in the user's daily-driver WT (Monarch behavior). A portable / unpackaged
# build has its own identity (or none), so each spawn becomes its own process.
#
# Usage:
#   powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/setup-wt-portable.ps1
#   powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/setup-wt-portable.ps1 -Version 1.20.11781.0
#
# On success prints the resolved wt.exe path on stdout (last line). Idempotent:
# if the cache already contains the requested version, the download is skipped.

[CmdletBinding()]
param(
    [string]$Version = "1.21.3231.0"
)

# PowerShell 5.1 defaults to TLS 1.0/1.1 which GitHub disabled in 2018;
# pwsh 7+ defaults are fine but be explicit so the script also works
# under stock Windows PowerShell.
[Net.ServicePointManager]::SecurityProtocol =
    [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12

$ErrorActionPreference = "Stop"

$cacheRoot = Join-Path $env:USERPROFILE ".cache\wintty-bench\wt"
$cacheDir = Join-Path $cacheRoot $Version
$wtExe = Join-Path $cacheDir "wt.exe"

if (Test-Path $wtExe) {
    Write-Host "Portable WT $Version already present at $wtExe"
    Write-Output $wtExe
    return
}

$url = "https://github.com/microsoft/terminal/releases/download/v$Version/Microsoft.WindowsTerminal_${Version}_x64.zip"
$zipPath = Join-Path $env:TEMP "wintty-bench-wt-$Version.zip"

Write-Host "Downloading portable WT $Version from $url"
try {
    Invoke-WebRequest -Uri $url -OutFile $zipPath -UseBasicParsing
} catch {
    Write-Error "Failed to download $url. Verify the version exists at https://github.com/microsoft/terminal/releases"
    throw
}

try {
    Write-Host "Extracting to $cacheDir"
    if (Test-Path $cacheDir) {
        Remove-Item -Recurse -Force $cacheDir
    }
    New-Item -ItemType Directory -Force -Path $cacheDir | Out-Null

    Expand-Archive -Path $zipPath -DestinationPath $cacheDir -Force

    # Some WT release ZIPs extract into a nested `terminal-<version>\` folder; if
    # wt.exe ended up nested, flatten so $cacheDir\wt.exe is the canonical path.
    if (-not (Test-Path $wtExe)) {
        $nested = Get-ChildItem -Path $cacheDir -Recurse -Filter "wt.exe" | Select-Object -First 1
        if ($nested) {
            $nestedDir = Split-Path $nested.FullName -Parent
            Get-ChildItem -Path $nestedDir | ForEach-Object {
                Move-Item -Path $_.FullName -Destination $cacheDir -Force
            }
        }
    }

    if (-not (Test-Path $wtExe)) {
        throw "wt.exe not found in extracted ZIP. Verify the release ZIP layout."
    }
} catch {
    Remove-Item -Recurse -Force $cacheDir -ErrorAction SilentlyContinue
    throw
} finally {
    Remove-Item -Path $zipPath -Force -ErrorAction SilentlyContinue
}

Write-Host "Portable WT $Version installed at $wtExe"
Write-Output $wtExe
