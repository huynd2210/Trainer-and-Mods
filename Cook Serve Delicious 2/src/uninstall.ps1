<#
    CSD2 Autoplay - uninstaller

    Restores the original data.win from the verified backup made at install
    time. The backup is left in place afterwards so you can reinstall without
    re-copying 834 MB.
#>
[CmdletBinding()]
param(
    [string] $GameDir,
    [switch] $RemoveBackup
)

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
    foreach ($c in $candidates) {
        if ($c -and (Test-Path (Join-Path $c 'data.win'))) { return $c }
    }
    return $null
}

if (-not $GameDir) { $GameDir = Find-GameDir }
if (-not $GameDir) { Fail 'could not find the game. Re-run with -GameDir "<path>"' }

$dataWin = Join-Path $GameDir 'data.win'
$vanilla = Join-Path $root 'backup\data.win.vanilla'

if (-not (Test-Path $vanilla)) {
    Fail "no backup at $vanilla.`nSteam's ""Verify integrity of game files"" will also restore the original."
}
if (Get-Process CSD2 -ErrorAction SilentlyContinue) {
    Fail 'Cook, Serve, Delicious! 2 is running. Close it and re-run.'
}

Step "Restoring original data.win to $GameDir"
Copy-Item $vanilla $dataWin -Force
$a = (Get-FileHash $vanilla -Algorithm SHA256).Hash
$b = (Get-FileHash $dataWin -Algorithm SHA256).Hash
if ($a -ne $b) { Fail 'restore did not verify' }
Write-Host "    verified: $b"

if ($RemoveBackup) {
    Remove-Item $vanilla -Force
    Step 'Backup removed'
}

Write-Host ''
Write-Host 'Done. The game is back to vanilla.' -ForegroundColor Green
