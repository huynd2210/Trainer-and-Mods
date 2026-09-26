<#
    CSD2 Autoplay - installer

    Patches Cook, Serve, Delicious! 2's data.win in place. Your original is
    backed up first and verified byte-for-byte, so uninstall.ps1 can always put
    it back.

    Usage:
        .\install.ps1                     # auto-detect the game
        .\install.ps1 -GameDir "D:\...\CookServeDelicious2"

    The patch is always applied to the pristine backup, never to an already
    patched file, so re-running this is safe.
#>
[CmdletBinding()]
param(
    [string] $GameDir,
    [switch] $KeepTools
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

$UTMT_URL  = 'https://github.com/UnderminersTeam/UndertaleModTool/releases/download/0.9.2.0/UTMT_CLI_v0.9.2.0-Windows.zip'
$UTMT_SHA  = 'E7573E45D107BE34F81F955C6E4AFC3C7C8F2628E5A6F307A871E3825B3DFB40'

function Fail($m) { Write-Host "ERROR: $m" -ForegroundColor Red; exit 1 }
# --------------------------------------------------- already-patched detector
# The patch writes its own strings into the data file, so a raw byte scan tells
# us whether a data.win is vanilla without needing the modding tool. This
# matters: backing up an ALREADY PATCHED data.win would make "uninstall" restore
# a modded game and make the next install refuse to run.
function Test-Patched([string] $path) {
    $needle = 'AUTOPLAY  -  F8 to stop'
    $fs = [IO.File]::OpenRead($path)
    try {
        $size = 4MB
        $buf  = New-Object byte[] $size
        $tail = ''
        while (($read = $fs.Read($buf, 0, $size)) -gt 0) {
            $chunk = $tail + [Text.Encoding]::ASCII.GetString($buf, 0, $read)
            if ($chunk.Contains($needle)) { return $true }
            $keep = $needle.Length - 1
            if ($chunk.Length -gt $keep) { $tail = $chunk.Substring($chunk.Length - $keep) } else { $tail = $chunk }
        }
    } finally { $fs.Close() }
    return $false
}

function Step($m) { Write-Host "==> $m" -ForegroundColor Cyan }

# ---------------------------------------------------------------- locate game
function Find-GameDir {
    $candidates = @()
    try {
        $steam = (Get-ItemProperty 'HKCU:\Software\Valve\Steam' -ErrorAction Stop).SteamPath
        if ($steam) {
            $steam = $steam -replace '/', '\'
            $candidates += Join-Path $steam 'steamapps\common\CookServeDelicious2'
            $vdf = Join-Path $steam 'steamapps\libraryfolders.vdf'
            if (Test-Path $vdf) {
                foreach ($m in [regex]::Matches((Get-Content $vdf -Raw), '"path"\s*"([^"]+)"')) {
                    $candidates += Join-Path ($m.Groups[1].Value -replace '\\\\', '\') 'steamapps\common\CookServeDelicious2'
                }
            }
        }
    } catch { }
    $candidates += 'C:\Program Files (x86)\Steam\steamapps\common\CookServeDelicious2'
    $candidates += 'C:\Program Files (x86)\GOG Galaxy\Games\Cook Serve Delicious 2'
    foreach ($c in $candidates) {
        if ($c -and (Test-Path (Join-Path $c 'data.win'))) { return $c }
    }
    return $null
}

if (-not $GameDir) { $GameDir = Find-GameDir }
if (-not $GameDir) {
    Fail "could not find the game. Re-run with:`n    .\install.ps1 -GameDir ""<path to CookServeDelicious2>"""
}
$dataWin = Join-Path $GameDir 'data.win'
if (-not (Test-Path $dataWin)) { Fail "no data.win in $GameDir" }
Step "Game: $GameDir"

if (Get-Process CSD2 -ErrorAction SilentlyContinue) {
    Fail 'Cook, Serve, Delicious! 2 is running. Close it and re-run.'
}

# ------------------------------------------------------------------- back up
$backupDir = Join-Path $root 'backup'
$vanilla   = Join-Path $backupDir 'data.win.vanilla'
New-Item -ItemType Directory -Force -Path $backupDir | Out-Null

if (-not (Test-Path $vanilla)) {
    Step 'Checking your data.win is unmodified'
    if (Test-Patched $dataWin) {
        Fail @"
your data.win is ALREADY PATCHED and there is no backup to fall back on.
Backing it up now would capture the mod, not your original game.

Restore the original first - in Steam, right-click the game ->
Properties -> Installed Files -> Verify integrity of game files - then
re-run this installer.
"@
    }
    Step 'Backing up your original data.win (834 MB, takes a moment)'
    Copy-Item $dataWin $vanilla
    $a = (Get-FileHash $dataWin -Algorithm SHA256).Hash
    $b = (Get-FileHash $vanilla -Algorithm SHA256).Hash
    if ($a -ne $b) { Remove-Item $vanilla -Force; Fail 'backup did not verify - nothing was changed' }
    Write-Host "    backup verified: $b"
} else {
    Step 'Checking the existing backup is a clean copy'
    if (Test-Patched $vanilla) {
        Fail @"
backup\data.win.vanilla is itself patched, so it cannot be used as the base.
Delete it, restore the original game files (Steam -> Properties ->
Installed Files -> Verify integrity of game files), then re-run this installer.
"@
    }
    Step 'Using the existing verified backup (so this cannot double-patch)'
}

# --------------------------------------------------------------- get the tool
$toolDir = Join-Path $root 'tools\utmt'
$cli     = Join-Path $toolDir 'UndertaleModCli.exe'
if (-not (Test-Path $cli)) {
    Step 'Fetching UndertaleModTool CLI (63 MB, one time)'
    $zip = Join-Path $root 'tools\utmt_cli.zip'
    New-Item -ItemType Directory -Force -Path (Join-Path $root 'tools') | Out-Null
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri $UTMT_URL -OutFile $zip -UseBasicParsing
    $got = (Get-FileHash $zip -Algorithm SHA256).Hash
    if ($got -ne $UTMT_SHA) {
        Remove-Item $zip -Force
        Fail "downloaded tool failed its checksum (got $got) - nothing was changed"
    }
    Write-Host '    checksum OK'
    Expand-Archive -Path $zip -DestinationPath $toolDir -Force
    if (-not $KeepTools) { Remove-Item $zip -Force }
}
if (-not (Test-Path $cli)) { Fail 'UndertaleModCli.exe missing after extraction' }

# ------------------------------------------------------------------- patch it
Step 'Patching'
$out = Join-Path $root 'data.win.patched'
if (Test-Path $out) { Remove-Item $out -Force }

$env:CSD2AP_SRC = Join-Path $root 'src'
$log = & $cli load $vanilla -s (Join-Path $root 'tools\patch.csx') -o $out -f 2>&1 | Out-String
if ($log -notmatch 'PATCH OK') {
    Write-Host $log
    if (Test-Path $out) { Remove-Item $out -Force }
    Fail 'patching failed - your game is untouched'
}

Step 'Installing'
Copy-Item $out $dataWin -Force
Remove-Item $out -Force

Write-Host ''
Write-Host 'Done. Start the game, begin a shift, and press F8.' -ForegroundColor Green
Write-Host 'To revert:  .\uninstall.ps1'
Write-Host ''
Write-Host 'Note: Steam''s "Verify integrity of game files" also restores the'
Write-Host 'original, and a game update will silently undo the patch - just run'
Write-Host 'install.ps1 again if F8 stops responding.'
