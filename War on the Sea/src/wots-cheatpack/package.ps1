# War on the Sea Cheat Pack - builds every mod in the set and packages them together.
#
# Each mod keeps its own workspace, build, harnesses and zips; this does not duplicate any of
# that. It runs each mod's own package.ps1 (so every harness and signature check still gates
# the build), then collects the four DLLs into one distributable set:
#
#   release\WoTSCheatPack.zip         all four plugins, for a game that already has BepInEx
#   release\WoTSCheatPack-Pack.zip    BepInEx 5.4.23.2 x64 + all four, self-contained
#   release\WoTSCheatPack-Source.zip  every mod's full source, harnesses and build scripts
#
# -SkipMods reuses whatever each mod last built, for a docs-only repackage.
[CmdletBinding()]
param(
    [switch] $SkipMods
)
$ErrorActionPreference = "Stop"

$root     = Split-Path -Parent $MyInvocation.MyCommand.Path
$gameRoot = Join-Path $root "..\game"
$bepinex  = Join-Path $root "..\wots-trainer\tools\bepinex"
$release  = Join-Path $root "release"
$stage    = Join-Path $root "_stage"

# Every mod in the set, in the order they should read on screen and in the docs.
$mods = @(
    @{ Name = "WoTSTrainer";      Dir = "wots-trainer";      Title = "Trainer" }
    @{ Name = "WoTSAceAviators";  Dir = "wots-aceaviators";  Title = "Ace Aviators" }
    @{ Name = "WoTSMoreTargets";  Dir = "wots-moretargets";  Title = "More Targets" }
    @{ Name = "WoTSFireControl";  Dir = "wots-firecontrol";  Title = "Fire Control" }
)

if (-not $SkipMods) {
    foreach ($mod in $mods) {
        Write-Host "`n########## $($mod.Title) ##########" -ForegroundColor Magenta
        $modRoot = Join-Path $root "..\$($mod.Dir)"
        $script  = Join-Path $modRoot "package.ps1"
        if (-not (Test-Path $script)) { throw "Missing $script" }
        Push-Location $modRoot
        try {
            & $script | Out-Host
            if ($LASTEXITCODE -ne 0) { throw "$($mod.Title) packaging failed ($LASTEXITCODE)" }
        } finally { Pop-Location }
    }
}

Write-Host "`n########## Cheat Pack ##########" -ForegroundColor Magenta

# Collect the freshly built DLLs. Reading each mod's own build output rather than the game
# folder means the pack can never quietly ship a stale DLL that happened to be installed.
$dlls = @{}
foreach ($mod in $mods) {
    $dll = Join-Path $root "..\$($mod.Dir)\out\$($mod.Name).dll"
    if (-not (Test-Path $dll)) { throw "Build output not found: $dll (run without -SkipMods)" }
    $dlls[$mod.Name] = (Resolve-Path $dll).Path
}

Write-Host "`n== Stage ==" -ForegroundColor Cyan
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
$stageMod    = Join-Path $stage "mod"
$stagePack   = Join-Path $stage "pack"
$stageSource = Join-Path $stage "source"
New-Item -ItemType Directory -Path $stageMod, $stagePack, $stageSource -Force | Out-Null

# --- mod-only: the four plugins plus every mod's own README ---
$modPlugins = Join-Path $stageMod "BepInEx\plugins"
$modDocs    = Join-Path $stageMod "docs"
New-Item -ItemType Directory -Path $modPlugins, $modDocs -Force | Out-Null
foreach ($mod in $mods) {
    Copy-Item $dlls[$mod.Name] (Join-Path $modPlugins "$($mod.Name).dll") -Force
    $readme = Join-Path $root "..\$($mod.Dir)\README.txt"
    if (Test-Path $readme) { Copy-Item $readme (Join-Path $modDocs "$($mod.Name)-README.txt") -Force }
}
Copy-Item (Join-Path $root "README.txt") (Join-Path $stageMod "README.txt") -Force

# --- pack: the same, on top of a known-good BepInEx ---
Copy-Item (Join-Path $bepinex "*") $stagePack -Recurse -Force
$packPlugins = Join-Path $stagePack "BepInEx\plugins"
if (-not (Test-Path $packPlugins)) { New-Item -ItemType Directory -Path $packPlugins -Force | Out-Null }
foreach ($mod in $mods) {
    Copy-Item $dlls[$mod.Name] (Join-Path $packPlugins "$($mod.Name).dll") -Force
}
Copy-Item (Join-Path $stageMod "docs") $stagePack -Recurse -Force
Copy-Item (Join-Path $root "README.txt")  (Join-Path $stagePack "README.txt")  -Force
Copy-Item (Join-Path $root "INSTALL.txt") (Join-Path $stagePack "INSTALL.txt") -Force

# --- source: every workspace, minus build output and the vendored loader ---
$skipDirs = @("obj", "bin", "out", "release", "tools", "_stage", "_stage_pack", "_stage_mod")
foreach ($mod in $mods) {
    $src  = Join-Path $root "..\$($mod.Dir)"
    $dest = Join-Path $stageSource $mod.Dir
    New-Item -ItemType Directory -Path $dest -Force | Out-Null
    # /XD prunes build output and tools\bepinex: the loader is not ours to redistribute here,
    # and obj/bin would multiply the zip size for nothing.
    & robocopy $src $dest /E /R:2 /W:2 /NFL /NDL /NJH /NJS /XD $skipDirs | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "robocopy source copy failed for $($mod.Dir) (exit $LASTEXITCODE)" }
    $global:LASTEXITCODE = 0
}
Copy-Item (Join-Path $root "package.ps1") (Join-Path $stageSource "package.ps1") -Force
Copy-Item (Join-Path $root "README.txt")  (Join-Path $stageSource "README.txt")  -Force
Copy-Item (Join-Path $root "BUILDING.txt") (Join-Path $stageSource "BUILDING.txt") -Force

Write-Host "`n== Install into game folder ==" -ForegroundColor Cyan
# A running game holds winhttp.dll and the loaded plugin DLLs open. Skip rather than fight it:
# killing the game to swap a DLL would throw away whatever the player is in the middle of.
$running = @(Get-Process -Name "WarOnTheSea" -ErrorAction SilentlyContinue)
if ($running.Count -gt 0) {
    Write-Host "War on the Sea is running (PID $($running[0].Id)) - skipping install." -ForegroundColor Yellow
    Write-Host "The zips below are still built. Close the game and re-run to install." -ForegroundColor Yellow
} else {
    & robocopy $bepinex $gameRoot /E /IS /R:2 /W:2 /NFL /NDL /NJH /NJS
    if ($LASTEXITCODE -ge 8) { throw "robocopy install failed (exit $LASTEXITCODE)" }
    $global:LASTEXITCODE = 0
    $gamePlugins = Join-Path $gameRoot "BepInEx\plugins"
    if (-not (Test-Path $gamePlugins)) { New-Item -ItemType Directory -Path $gamePlugins -Force | Out-Null }
    foreach ($mod in $mods) {
        Copy-Item $dlls[$mod.Name] (Join-Path $gamePlugins "$($mod.Name).dll") -Force
    }
    Write-Host "Installed BepInEx + all $($mods.Count) plugins into $gameRoot"
}

Write-Host "`n== Package zips ==" -ForegroundColor Cyan
if (Test-Path $release) { Remove-Item $release -Recurse -Force }
New-Item -ItemType Directory -Path $release -Force | Out-Null
Compress-Archive -Path (Join-Path $stageMod "*")    -DestinationPath (Join-Path $release "WoTSCheatPack.zip")        -Force
Compress-Archive -Path (Join-Path $stagePack "*")   -DestinationPath (Join-Path $release "WoTSCheatPack-Pack.zip")   -Force
Compress-Archive -Path (Join-Path $stageSource "*") -DestinationPath (Join-Path $release "WoTSCheatPack-Source.zip") -Force
Remove-Item $stage -Recurse -Force

Write-Host "`nDone. Artifacts:" -ForegroundColor Green
Get-ChildItem $release | ForEach-Object { "  $($_.FullName)  ($([math]::Round($_.Length/1KB)) KB)" }

exit 0
