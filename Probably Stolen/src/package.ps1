# Builds every mod and writes dist\<Mod>.zip (extract into the game folder) and
# dist\<Mod>-Source.zip. The game has its own mod loader, so there is no -Pack.
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Force $dist | Out-Null

$mods = Get-ChildItem $root -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'manifest.xml') }
foreach ($mod in $mods.Name) {
    $proj = Join-Path $root $mod
    dotnet build (Join-Path $proj "$mod.csproj") -c Release -nologo -v q -p:SkipInstall=true
    if ($LASTEXITCODE -ne 0) { throw "build failed: $mod" }

    $stage = Join-Path $env:TEMP "ps-mod-stage\$mod"
    if (Test-Path $stage) { Remove-Item -Recurse -Force $stage }
    $modDir = Join-Path $stage "Mods\$mod"
    New-Item -ItemType Directory -Force $modDir | Out-Null
    Copy-Item (Join-Path $proj "bin\Release\$mod.dll"), (Join-Path $proj 'manifest.xml'), (Join-Path $proj 'README.md') $modDir
    $zip = Join-Path $dist "$mod.zip"
    if (Test-Path $zip) { Remove-Item $zip }
    Compress-Archive -Path (Join-Path $stage 'Mods') -DestinationPath $zip

    $srcStage = Join-Path $env:TEMP "ps-mod-stage\$mod-Source"
    if (Test-Path $srcStage) { Remove-Item -Recurse -Force $srcStage }
    New-Item -ItemType Directory -Force (Join-Path $srcStage $mod) | Out-Null
    Get-ChildItem $proj -File | Copy-Item -Destination (Join-Path $srcStage $mod)
    Copy-Item (Join-Path $root 'Directory.Build.props'), (Join-Path $root 'package.ps1') $srcStage
    $srcZip = Join-Path $dist "$mod-Source.zip"
    if (Test-Path $srcZip) { Remove-Item $srcZip }
    Compress-Archive -Path (Join-Path $srcStage '*') -DestinationPath $srcZip
}
Get-ChildItem $dist | Select-Object Name, Length
