# Build the morgue mod two ways:
#   build\data.win            vanilla + morgue
#   build\data.win.with-dice  the dice mod's build + morgue (both mods)
$ErrorActionPreference = 'Stop'
$env:MRG_SRC = "$PSScriptRoot\src"
$utmt = "$PSScriptRoot\tools\utmt\UndertaleModCli.exe"
$dice = "$PSScriptRoot\..\Deadzoned-DiceMod\build\data.win"
$log = & $utmt load "$PSScriptRoot\backup\data.win.vanilla" -s "$PSScriptRoot\tools\patch.csx" -o "$PSScriptRoot\build\data.win" -f 2>&1 | Out-String
if ($log -notmatch 'PATCH OK') { Write-Host $log; throw 'morgue build (vanilla) failed' }
Write-Host 'built build\data.win (vanilla + morgue)'
if (Test-Path $dice) {
    $log = & $utmt load $dice -s "$PSScriptRoot\tools\patch.csx" -o "$PSScriptRoot\build\data.win.with-dice" -f 2>&1 | Out-String
    if ($log -notmatch 'PATCH OK') { Write-Host $log; throw 'morgue build (on dice) failed' }
    Write-Host 'built build\data.win.with-dice (dice + morgue)'
}
