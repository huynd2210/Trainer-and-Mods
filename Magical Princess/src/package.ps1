<#
    Builds MagicalPrincessTrainer and produces the two distribution zips:

      dist\MagicalPrincessTrainer.zip       mod only - for people who already have BepInEx
      dist\MagicalPrincessTrainer-Pack.zip  BepInEx 5 (mono x64) + the mod, extract and play

    -Install also copies the pack straight into the game folder.
#>
[CmdletBinding()]
param(
    [string] $GameDir    = 'C:\Games\Magical Princess',
    [string] $BepInExSrc = 'C:\Games\PEAK.v1.62.a_LinkNeverDie.Com\BepInExPack_PEAK',
    [switch] $Install
)

$ErrorActionPreference = 'Stop'
$root    = $PSScriptRoot
$dist    = Join-Path $root 'dist'
$staging = Join-Path $dist 'staging'
$name    = 'MagicalPrincessTrainer'

Write-Host '== build ==' -ForegroundColor Cyan
dotnet build (Join-Path $root 'src\MagicalPrincessTrainer.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'build failed' }

$dll = Join-Path $root 'src\bin\Release\MagicalPrincessTrainer.dll'
if (-not (Test-Path $dll)) { throw "missing build output: $dll" }

Write-Host '== stage ==' -ForegroundColor Cyan
if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
$modDir  = Join-Path $staging 'mod'
$packDir = Join-Path $staging 'pack'
New-Item -ItemType Directory -Force -Path (Join-Path $modDir 'BepInEx\plugins') | Out-Null

Copy-Item $dll (Join-Path $modDir 'BepInEx\plugins')
Copy-Item (Join-Path $root 'README.md') (Join-Path $modDir "$name-README.md")

# Pack = a known-good BepInEx 5 mono x64 install with the mod already in place.
New-Item -ItemType Directory -Force -Path $packDir | Out-Null
Copy-Item (Join-Path $BepInExSrc '*') $packDir -Recurse -Force
Get-ChildItem (Join-Path $packDir 'BepInEx\plugins') -File -ErrorAction SilentlyContinue | Remove-Item -Force
New-Item -ItemType Directory -Force -Path (Join-Path $packDir 'BepInEx\plugins') | Out-Null
Copy-Item $dll (Join-Path $packDir 'BepInEx\plugins')
Copy-Item (Join-Path $root 'README.md') (Join-Path $packDir "$name-README.md")

# Keep the game's very chatty Debug.Log out of the BepInEx log file.
$cfg = Join-Path $packDir 'BepInEx\config\BepInEx.cfg'
(Get-Content $cfg) -replace '^UnityLogListening = true', 'UnityLogListening = false' |
    Set-Content $cfg -Encoding utf8

@"
Magical Princess Trainer - install
==================================

This zip contains BepInEx 5 (Mono, x64) plus the trainer. It contains no other mods.

1. Close the game.
2. Extract everything in this zip into your Magical Princess folder - the one holding
   MagicalPrincess.exe. When it is right, winhttp.dll sits next to MagicalPrincess.exe
   and there is a BepInEx\ folder beside it.
3. Start MagicalPrincess.exe. The trainer panel appears at the top left; F1 hides it.

To uninstall, delete winhttp.dll, doorstop_config.ini, .doorstop_version and the
BepInEx folder. No game file is modified.

Hotkeys and settings: see MagicalPrincessTrainer-README.md.
"@ | Set-Content (Join-Path $packDir 'INSTALL.txt') -Encoding utf8

Write-Host '== zip ==' -ForegroundColor Cyan
$modZip  = Join-Path $dist "$name.zip"
$packZip = Join-Path $dist "$name-Pack.zip"
Remove-Item $modZip, $packZip -Force -ErrorAction SilentlyContinue
Compress-Archive -Path (Join-Path $modDir  '*') -DestinationPath $modZip
Compress-Archive -Path (Join-Path $packDir '*') -DestinationPath $packZip

if ($Install) {
    Write-Host "== install -> $GameDir ==" -ForegroundColor Cyan
    if (-not (Test-Path (Join-Path $GameDir 'MagicalPrincess.exe'))) {
        throw "not a Magical Princess folder: $GameDir"
    }
    $existingCfg = Join-Path $GameDir 'BepInEx\config'
    if (Test-Path $existingCfg) {
        # Never clobber settings the player has already tuned.
        Write-Host '   keeping existing BepInEx\config' -ForegroundColor Yellow
        Get-ChildItem $packDir -Force | Where-Object { $_.Name -ne 'BepInEx' } |
            ForEach-Object { Copy-Item $_.FullName $GameDir -Recurse -Force }
        Copy-Item (Join-Path $packDir 'BepInEx\core')    (Join-Path $GameDir 'BepInEx') -Recurse -Force
        Copy-Item (Join-Path $packDir 'BepInEx\plugins') (Join-Path $GameDir 'BepInEx') -Recurse -Force
    }
    else {
        Copy-Item (Join-Path $packDir '*') $GameDir -Recurse -Force
    }
}

Write-Host "done:`n  $modZip`n  $packZip" -ForegroundColor Green
