# Builds the standalone distribution packages for a Cat Mail Co. mod:
#
#   CatMailCo-<Mod>-v<version>.zip
#       Full package: BepInEx loader + .NET runtime + the plugin, so a
#       player can extract it into the game folder and run on a clean install.
#
#   CatMailCo-<Mod>-v<version>-PluginOnly.zip
#       Just the plugin folder for players who already run BepInEx.
#
# The BepInEx loader files are taken from the tested Friends Mod Pack zip so
# they stay byte-identical to the pack. The plugin DLL and README are taken
# from the live installation (run mods\<Mod>\build.ps1 first). The version is
# read from the mod's Plugin.cs unless -Version is passed.
#
# Distribution docs live in dist\docs\<Mod>\ and are copied in unchanged.
#
# Usage:  powershell -File build-standalone.ps1 -Mod BoatHeightUnlocker
#         powershell -File build-standalone.ps1 -Mod PackageCounter

param(
    [ValidateSet('BoatHeightUnlocker', 'PackageCounter', 'SpeedBoost', 'BreakRoomKey', 'WhosItFor')]
    [string]$Mod = 'BoatHeightUnlocker',
    [string]$Version = ''
)

$ErrorActionPreference = 'Stop'

$scriptDir   = Split-Path -Parent $MyInvocation.MyCommand.Path
$gameDir     = (Resolve-Path (Join-Path $scriptDir '..')).Path
$docsDir     = Join-Path $scriptDir 'docs'
$stagingRoot = Join-Path $scriptDir '.staging'
$fullStage   = Join-Path $stagingRoot 'full'
$onlyStage   = Join-Path $stagingRoot 'plugin-only'

$modpackZip   = Join-Path $scriptDir 'CatMailCo-Friends-ModPack-v1.0.0.zip'
$modSourceDir = Join-Path $gameDir "mods\$Mod"
$pluginDir    = Join-Path $gameDir "BepInEx\plugins\$Mod"
$pluginDll    = Join-Path $pluginDir "$Mod.dll"
$pluginReadme = Join-Path $modSourceDir 'README.md'
$pluginCs     = Join-Path $modSourceDir 'Plugin.cs'
$modDocsDir   = Join-Path $docsDir $Mod

if ([string]::IsNullOrEmpty($Version)) {
    $versionMatch = Select-String -Path $pluginCs -Pattern 'Version\s*=\s*"([^"]+)"'
    if ($null -eq $versionMatch) {
        throw "Could not determine the version from $pluginCs; pass -Version explicitly."
    }
    $Version = $versionMatch.Matches[0].Groups[1].Value
}

$fullZip = Join-Path $scriptDir "CatMailCo-$Mod-v$Version.zip"
$onlyZip = Join-Path $scriptDir "CatMailCo-$Mod-v$Version-PluginOnly.zip"

foreach ($required in @($modpackZip, $pluginDll, $pluginReadme,
                        (Join-Path $modDocsDir 'README-FIRST.txt'),
                        (Join-Path $modDocsDir 'README-FIRST-plugin.txt'),
                        (Join-Path $modDocsDir 'VERSIONS.txt'),
                        (Join-Path $modDocsDir 'THIRD-PARTY-NOTICES.txt'))) {
    if (-not (Test-Path $required)) {
        throw "Missing required file: $required"
    }
}

# Ships any extra .md reference material a mod keeps beside its README, so the
# README does not point at files that are missing from the zip. Mods without
# extra docs are unaffected.
function Copy-ExtraDocs {
    param([string]$Destination)

    Get-ChildItem -Path $modSourceDir -File |
        Where-Object { $_.Extension -in '.md', '.csv' -and $_.Name -ne 'README.md' } |
        ForEach-Object {
            Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $Destination $_.Name) -Force
        }
}

Write-Host "Building $Mod v$Version standalone packages..."

# ---- clean staging ----
Remove-Item -Recurse -Force $stagingRoot -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $fullStage, $onlyStage | Out-Null

# ---- full package: loader base from the tested mod pack zip ----
Expand-Archive -LiteralPath $modpackZip -DestinationPath $fullStage -Force

# drop every plugin from the pack except the target mod
Get-ChildItem (Join-Path $fullStage 'BepInEx\plugins') -Directory | ForEach-Object {
    if ($_.Name -ne $Mod) { Remove-Item -Recurse -Force $_.FullName }
}

# refresh the target plugin with the freshly built DLL + README
$fullPluginDir = Join-Path $fullStage "BepInEx\plugins\$Mod"
Remove-Item -Recurse -Force $fullPluginDir -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $fullPluginDir | Out-Null
Copy-Item -LiteralPath $pluginDll    -Destination (Join-Path $fullPluginDir "$Mod.dll")
Copy-Item -LiteralPath $pluginReadme -Destination (Join-Path $fullPluginDir 'README.md')
Copy-ExtraDocs $fullPluginDir

# mod-specific top-level docs
Copy-Item -LiteralPath (Join-Path $modDocsDir 'README-FIRST.txt')        -Destination (Join-Path $fullStage 'README-FIRST.txt')        -Force
Copy-Item -LiteralPath (Join-Path $modDocsDir 'VERSIONS.txt')            -Destination (Join-Path $fullStage 'VERSIONS.txt')            -Force
Copy-Item -LiteralPath (Join-Path $modDocsDir 'THIRD-PARTY-NOTICES.txt') -Destination (Join-Path $fullStage 'THIRD-PARTY-NOTICES.txt') -Force

# ---- plugin-only package ----
$onlyPluginDir = Join-Path $onlyStage "BepInEx\plugins\$Mod"
New-Item -ItemType Directory -Force -Path $onlyPluginDir | Out-Null
Copy-Item -LiteralPath $pluginDll    -Destination (Join-Path $onlyPluginDir "$Mod.dll")
Copy-Item -LiteralPath $pluginReadme -Destination (Join-Path $onlyPluginDir 'README.md')
Copy-ExtraDocs $onlyPluginDir
Copy-Item -LiteralPath (Join-Path $modDocsDir 'README-FIRST-plugin.txt') -Destination (Join-Path $onlyStage 'README-FIRST.txt')

# ---- zip ----
Compress-Archive -Path (Join-Path $fullStage '*') -DestinationPath $fullZip -Force -CompressionLevel Optimal
Compress-Archive -Path (Join-Path $onlyStage '*') -DestinationPath $onlyZip -Force -CompressionLevel Optimal

Remove-Item -Recurse -Force $stagingRoot -ErrorAction SilentlyContinue

Write-Host "Created:"
Write-Host "  $fullZip"
Write-Host "  $onlyZip"

