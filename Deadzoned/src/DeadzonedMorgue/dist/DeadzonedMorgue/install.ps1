<#
    Deadzoned Morgue - installer

    Patches Deadzoned's data.win in place. Mods stack: this patches whatever
    data.win is there now, so other Deadzoned mods (e.g. the Dice Mod) stay.
    The file is backed up first and uninstall.ps1 puts it back.

    Usage:
        .\install.ps1                     # auto-detect the game
        .\install.ps1 -GameDir "D:\...\Deadzoned"
#>
[CmdletBinding()]
param(
    [string] $GameDir,
    [switch] $KeepTools
)

# ---- this mod ---------------------------------------------------------------
$ModName = 'Deadzoned Morgue'
$Marker  = 'morgue/runs.txt'        # a string only this mod adds to data.win
$SrcEnv  = 'MRG_SRC'
$Done    = 'When a run ends, a morgue file appears in %LOCALAPPDATA%\Deadzoned\morgue'
# -----------------------------------------------------------------------------

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

$UTMT_URL = 'https://github.com/UnderminersTeam/UndertaleModTool/releases/download/0.9.2.0/UTMT_CLI_v0.9.2.0-Windows.zip'
$UTMT_SHA = 'E7573E45D107BE34F81F955C6E4AFC3C7C8F2628E5A6F307A871E3825B3DFB40'

function Fail($m) { Write-Host "ERROR: $m" -ForegroundColor Red; exit 1 }
function Step($m) { Write-Host "==> $m" -ForegroundColor Cyan }
function Has-Marker([string] $path) { [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes($path)).Contains($Marker) }

function Find-GameDir {
    $candidates = @()
    try {
        $steam = (Get-ItemProperty 'HKCU:\Software\Valve\Steam' -ErrorAction Stop).SteamPath
        if ($steam) {
            $steam = $steam -replace '/', '\'
            $candidates += Join-Path $steam 'steamapps\common\Deadzoned'
            $vdf = Join-Path $steam 'steamapps\libraryfolders.vdf'
            if (Test-Path $vdf) {
                foreach ($m in [regex]::Matches((Get-Content $vdf -Raw), '"path"\s*"([^"]+)"')) {
                    $candidates += Join-Path ($m.Groups[1].Value -replace '\\\\', '\') 'steamapps\common\Deadzoned'
                }
            }
        }
    } catch { }
    $candidates += 'C:\Program Files (x86)\Steam\steamapps\common\Deadzoned'
    foreach ($c in $candidates) {
        if ($c -and (Test-Path (Join-Path $c 'data.win'))) { return $c }
    }
    return $null
}

if (-not $GameDir) { $GameDir = Find-GameDir }
if (-not $GameDir) { Fail "could not find the game. Re-run with:`n    .\install.ps1 -GameDir ""<path to Deadzoned>""" }
$dataWin = Join-Path $GameDir 'data.win'
if (-not (Test-Path $dataWin)) { Fail "no data.win in $GameDir" }
Step "Game: $GameDir"
if (Get-Process Deadzoned -ErrorAction SilentlyContinue) { Fail 'Deadzoned is running. Close it and re-run.' }

if (Has-Marker $dataWin) {
    Write-Host "$ModName is already installed. To reinstall or update, run uninstall.ps1 first." -ForegroundColor Yellow
    exit 0
}

# ------------------------------------------------------------------- back up
$backupDir = Join-Path $root 'backup'
$before    = Join-Path $backupDir 'data.win.before'
New-Item -ItemType Directory -Force -Path $backupDir | Out-Null
Step 'Backing up your current data.win'
Copy-Item $dataWin $before -Force
if ((Get-FileHash $dataWin).Hash -ne (Get-FileHash $before).Hash) { Fail 'backup did not verify - nothing was changed' }

# --------------------------------------------------------------- get the tool
$toolDir = Join-Path $root 'tools\utmt'
$cli     = Join-Path $toolDir 'UndertaleModCli.exe'
if (-not (Test-Path $cli)) {
    Step 'Fetching UndertaleModTool CLI (63 MB, one time)'
    $zip = Join-Path $root 'tools\utmt_cli.zip'
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri $UTMT_URL -OutFile $zip -UseBasicParsing
    $got = (Get-FileHash $zip -Algorithm SHA256).Hash
    if ($got -ne $UTMT_SHA) { Remove-Item $zip -Force; Fail "downloaded tool failed its checksum (got $got) - nothing was changed" }
    Write-Host '    checksum OK'
    Expand-Archive -Path $zip -DestinationPath $toolDir -Force
    if (-not $KeepTools) { Remove-Item $zip -Force }
}
if (-not (Test-Path $cli)) { Fail 'UndertaleModCli.exe missing after extraction' }

# ------------------------------------------------------------------- patch it
Step 'Patching'
$out = Join-Path $root 'data.win.patched'
if (Test-Path $out) { Remove-Item $out -Force }
Set-Item "env:$SrcEnv" (Join-Path $root 'src')
$log = & $cli load $before -s (Join-Path $root 'tools\patch.csx') -o $out -f 2>&1 | Out-String
if ($log -notmatch 'PATCH OK') {
    Write-Host $log
    if (Test-Path $out) { Remove-Item $out -Force }
    Fail 'patching failed - your game is untouched'
}

Step 'Installing'
Copy-Item $out $dataWin -Force
Remove-Item $out -Force
(Get-FileHash $dataWin).Hash | Set-Content (Join-Path $backupDir 'installed.sha256')

Write-Host ''
Write-Host "Done. $Done" -ForegroundColor Green
Write-Host 'To revert:  .\uninstall.ps1'
Write-Host 'A game update, or Steam''s "Verify integrity of game files", removes the mod; run install.ps1 again after one.'
