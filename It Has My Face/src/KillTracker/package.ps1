<#
    Packages KillTracker for distribution.

    Produces three zips in dist/:

      KillTracker.zip       the mod alone, for people who already run BepInEx.
                            Extract into the game folder.

      KillTracker-Pack.zip  BepInEx 5.4.23.5 plus the mod, self-contained.
                            Extract into the game folder and it is ready to go.

      KillTracker-Source.zip src/, package.ps1 and README.md.

    The -Pack exists because a wrong BepInEx install is the most common way
    these mods fail. It also makes the mod-only zip self-serve: install any
    -Pack, then drop further mod zips into its BepInEx\plugins.
#>
[CmdletBinding()]
param(
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'

$root     = $PSScriptRoot
$modName  = 'KillTracker'
$project  = Join-Path $root 'src\KillTracker\KillTracker.csproj'
$builtDll = Join-Path $root 'src\KillTracker\bin\Release\KillTracker.dll'
$bepinex  = Join-Path $root 'vendor\BepInEx-5.4.23.5'
$dist     = Join-Path $root 'dist'
$staging  = Join-Path $root 'build'

if (-not $SkipBuild) {
    Write-Host 'Building...' -ForegroundColor Cyan
    dotnet build $project -c Release -v minimal
    if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE." }
}

if (-not (Test-Path $builtDll)) { throw "Built plugin not found at $builtDll" }

# Staging is rebuilt from scratch each run; dist keeps only the zips.
if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
New-Item -ItemType Directory -Path $staging -Force | Out-Null
New-Item -ItemType Directory -Path $dist -Force | Out-Null

function New-Zip($sourceGlob, $name) {
    $zip = Join-Path $dist $name
    if (Test-Path $zip) { Remove-Item $zip -Force }
    Compress-Archive -Path $sourceGlob -DestinationPath $zip
    Write-Host "  -> $zip" -ForegroundColor Green
}

# ---------------------------------------------------------------- mod only --
$modOnly = Join-Path $staging 'mod'
$modPlugins = Join-Path $modOnly 'BepInEx\plugins'
New-Item -ItemType Directory -Path $modPlugins -Force | Out-Null
Copy-Item $builtDll $modPlugins
Copy-Item (Join-Path $root 'README.md') $modPlugins
New-Zip (Join-Path $modOnly '*') "$modName.zip"

# -------------------------------------------------------------------- pack --
$pack = Join-Path $staging 'pack'
New-Item -ItemType Directory -Path $pack -Force | Out-Null
Copy-Item (Join-Path $bepinex '*') $pack -Recurse -Force

$packPlugins = Join-Path $pack 'BepInEx\plugins'
New-Item -ItemType Directory -Path $packPlugins -Force | Out-Null
Copy-Item $builtDll $packPlugins
Copy-Item (Join-Path $root 'README.md') $pack

# Each -Pack ships its own INSTALL.txt covering only the mods it contains.
$install = @"
KillTracker - complete install
==============================

This archive contains BepInEx 5.4.23.5 and one mod: KillTracker.

Install
-------
1. Extract everything in this archive into your game folder - the folder
   containing It Has My Face.exe.
   When you are done, that folder contains winhttp.dll, doorstop_config.ini
   and a BepInEx folder sitting next to the .exe.
2. Start the game normally and start a level.
3. Kill something: a line under the crosshair shows the count. Hold F4 to see
   every kill for this level, this run and your lifetime.

The first launch takes a few seconds longer than usual while BepInEx sets
itself up. It creates BepInEx\config\com.ithasmyface.killtracker.cfg, where
you can change the key. Your counts are kept in
BepInEx\config\KillTracker\kills.json.

Uninstall
---------
Delete winhttp.dll, doorstop_config.ini, .doorstop_version, changelog.txt and
the BepInEx folder from the game folder. Nothing else in the game is modified.

Adding more mods later
----------------------
BepInEx is already installed by this archive. Drop any other mod's .dll into
BepInEx\plugins.
"@
Set-Content -Path (Join-Path $pack 'INSTALL.txt') -Value $install -Encoding utf8
New-Zip (Join-Path $pack '*') "$modName-Pack.zip"

# ------------------------------------------------------------------ source --
$src = Join-Path $staging 'source'
New-Item -ItemType Directory -Path $src -Force | Out-Null
Copy-Item (Join-Path $root 'README.md'), (Join-Path $root 'package.ps1') $src
$srcProject = Join-Path $src 'src\KillTracker'
New-Item -ItemType Directory -Path $srcProject -Force | Out-Null
Get-ChildItem (Join-Path $root 'src\KillTracker') -File | Copy-Item -Destination $srcProject
New-Zip (Join-Path $src '*') "$modName-Source.zip"

Remove-Item $staging -Recurse -Force

Write-Host ''
Write-Host 'Done.' -ForegroundColor Cyan
Get-ChildItem $dist | Select-Object Name, @{n='KB';e={[math]::Round($_.Length/1KB)}} | Format-Table -AutoSize
