# Rebuild build\data.win from backup\data.win.vanilla + src\dzm.gml
$env:DZM_SRC = "$PSScriptRoot\src"
$utmt = "$PSScriptRoot\tools\utmt\UndertaleModCli.exe"
& $utmt load "$PSScriptRoot\backup\data.win.vanilla" -s "$PSScriptRoot\tools\patch.csx" -o "$PSScriptRoot\build\data.win" -f
if ($LASTEXITCODE -ne 0) { throw "Mod build failed with exit code $LASTEXITCODE" }
