<#
    Builds both mods and produces distributable zips in ModSource\dist:

      <Name>.zip        mod only  - for people who already have BepInEx installed
      <Name>-Pack.zip   BepInEx + that one mod, self-contained
      QuasimorphMods-Bundle.zip   BepInEx + both mods

    Every zip extracts directly into the Quasimorph game folder.
#>

$ErrorActionPreference = 'Stop'

$Root     = $PSScriptRoot
$Dist     = Join-Path $Root 'dist'
$Staging  = Join-Path $Root 'obj\staging'
$BepZip   = Join-Path $Root 'lib\BepInEx_win_x64.zip'
$BepStage = Join-Path $Root 'obj\bepinex'

$Mods = @(
    @{ Name = 'QM.WarpDrive';   Title = 'Quasimorph Warp Drive' },
    @{ Name = 'QM.Spawner';     Title = 'Quasimorph Item Spawner' },
    @{ Name = 'QM.KillTracker'; Title = 'Quasimorph Kill Tracker' },
    @{ Name = 'QM.OperatorBoost'; Title = 'Quasimorph Operator Boost' },
    @{ Name = 'QM.MapReveal';   Title = 'Quasimorph Map Reveal' }
)

# ---------------------------------------------------------------- build

foreach ($mod in $Mods) {
    $proj = Join-Path $Root "$($mod.Name)\$($mod.Name).csproj"
    Write-Host "Building $($mod.Name)..."
    dotnet build $proj -c Release --nologo -v quiet
    if ($LASTEXITCODE -ne 0) { throw "Build failed for $($mod.Name)" }
}

# ---------------------------------------------------------------- prepare

if (Test-Path $Dist)    { Remove-Item $Dist -Recurse -Force }
if (Test-Path $Staging) { Remove-Item $Staging -Recurse -Force }
New-Item -ItemType Directory -Path $Dist -Force | Out-Null

if (-not (Test-Path $BepZip)) {
    throw "Missing $BepZip. Download BepInEx 5.4.23.2 win x64 and save it there."
}
if (Test-Path $BepStage) { Remove-Item $BepStage -Recurse -Force }
Expand-Archive -Path $BepZip -DestinationPath $BepStage -Force

function New-Stage {
    param([string]$Label)
    $dir = Join-Path $Staging $Label
    if (Test-Path $dir) { Remove-Item $dir -Recurse -Force }
    New-Item -ItemType Directory -Path (Join-Path $dir 'BepInEx\plugins') -Force | Out-Null
    return $dir
}

function Add-Mod {
    param([string]$StageDir, [hashtable]$Mod)
    $dll = Join-Path $Root "$($Mod.Name)\bin\Release\$($Mod.Name).dll"
    if (-not (Test-Path $dll)) { throw "Missing build output: $dll" }
    Copy-Item $dll (Join-Path $StageDir 'BepInEx\plugins') -Force
    Copy-Item (Join-Path $Root "$($Mod.Name)\README.md") `
              (Join-Path $StageDir "BepInEx\plugins\$($Mod.Name).README.md") -Force
}

function Add-BepInEx {
    param([string]$StageDir)
    # Merge, don't replace: the staging dir already contains BepInEx\plugins.
    Copy-Item (Join-Path $BepStage '*') $StageDir -Recurse -Force
}

function Write-Install {
    param([string]$StageDir, [string[]]$Titles)
    $list = ($Titles | ForEach-Object { "  - $_" }) -join "`r`n"
    @"
Quasimorph mods
===============

Included in this package:
$list

Install
-------
1. Extract everything in this zip directly into your Quasimorph game folder -- the one
   containing Quasimorph.exe. Say yes if it asks to merge folders.
2. Launch the game once and let it reach the main menu, then quit. This lets BepInEx
   generate its config files.
3. Play. Hotkeys and settings are described in the README files placed in
   BepInEx\plugins\.

Settings live in BepInEx\config\ after that first launch.

Uninstall
---------
Delete the BepInEx folder, winhttp.dll, doorstop_config.ini, changelog.txt and
.doorstop_version from the game folder. To remove just one mod, delete its .dll from
BepInEx\plugins\ instead.
"@ | Set-Content -Path (Join-Path $StageDir 'INSTALL.txt') -Encoding utf8
}

function New-Zip {
    param([string]$StageDir, [string]$ZipName)
    $out = Join-Path $Dist $ZipName
    Compress-Archive -Path (Join-Path $StageDir '*') -DestinationPath $out -Force
    $kb = [math]::Round((Get-Item $out).Length / 1KB, 1)
    Write-Host "  -> $ZipName ($kb KB)"
}

# ---------------------------------------------------------------- package

foreach ($mod in $Mods) {
    $plain = New-Stage "$($mod.Name)-plain"
    Add-Mod $plain $mod
    New-Zip $plain "$($mod.Name).zip"

    $pack = New-Stage "$($mod.Name)-pack"
    Add-Mod $pack $mod
    Add-BepInEx $pack
    Write-Install $pack @($mod.Title)
    New-Zip $pack "$($mod.Name)-Pack.zip"
}

$bundle = New-Stage 'bundle'
foreach ($mod in $Mods) { Add-Mod $bundle $mod }
Add-BepInEx $bundle
Write-Install $bundle ($Mods | ForEach-Object { $_.Title })
New-Zip $bundle 'QuasimorphMods-Bundle.zip'

Write-Host ''
Write-Host "Done. Packages are in $Dist"
