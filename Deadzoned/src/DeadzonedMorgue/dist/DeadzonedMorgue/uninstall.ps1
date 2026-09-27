<#
    Deadzoned Morgue - uninstaller

    Puts back the data.win this mod's installer backed up. That is only safe
    if nothing has changed data.win since - if another mod was installed on
    top, restoring would silently remove it too, so this refuses instead.
#>
[CmdletBinding()]
param([string] $GameDir)

# ---- this mod ---------------------------------------------------------------
$ModName = 'Deadzoned Morgue'
$Marker  = 'morgue/runs.txt'
# -----------------------------------------------------------------------------

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

function Fail($m) { Write-Host "ERROR: $m" -ForegroundColor Red; exit 1 }
function Step($m) { Write-Host "==> $m" -ForegroundColor Cyan }

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
if (-not $GameDir) { Fail 'could not find the game. Re-run with -GameDir "<path>"' }
$dataWin   = Join-Path $GameDir 'data.win'
$before    = Join-Path $root 'backup\data.win.before'
$installed = Join-Path $root 'backup\installed.sha256'

if (Get-Process Deadzoned -ErrorAction SilentlyContinue) { Fail 'Deadzoned is running. Close it and re-run.' }
if (-not [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes($dataWin)).Contains($Marker)) {
    Write-Host "$ModName is not installed - nothing to do." -ForegroundColor Yellow
    exit 0
}
if (-not (Test-Path $before) -or -not (Test-Path $installed)) {
    Fail "no backup from this installer. Steam's ""Verify integrity of game files"" restores the original game (removing all mods)."
}
if ((Get-FileHash $dataWin).Hash -ne (Get-Content $installed).Trim()) {
    Fail @"
data.win has changed since $ModName was installed (another mod was installed
after it, or the game updated), so restoring the backup would undo that too.
To remove it: Steam -> Deadzoned -> Properties -> Installed Files -> Verify
integrity of game files (back to vanilla), then reinstall the mods you want.
"@
}

Step "Restoring data.win to $GameDir"
Copy-Item $before $dataWin -Force
if ((Get-FileHash $before).Hash -ne (Get-FileHash $dataWin).Hash) { Fail 'restore did not verify' }
Remove-Item $installed -Force
Write-Host ''
Write-Host "Done. $ModName is removed." -ForegroundColor Green
