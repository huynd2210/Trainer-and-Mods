# Ace Aviators packaging: builds the plugin, flies the guidance in the simulation harness,
# verifies every patched signature against the real game assembly, installs into the game
# folder, and produces the two distributable zips per the BepInEx convention:
#   release\WoTSAceAviators.zip       (plugin only, for users who already have BepInEx)
#   release\WoTSAceAviators-Pack.zip  (BepInEx 5.4.23.2 x64 + plugin, self-contained)
$ErrorActionPreference = "Stop"

$root       = Split-Path -Parent $MyInvocation.MyCommand.Path
$gameRoot   = Join-Path $root "..\game"
$src        = Join-Path $root "src"
$out        = Join-Path $root "out"
$bepinex    = Join-Path $root "tools\bepinex"
$release    = Join-Path $root "release"
$managedDir = Join-Path $gameRoot "WarOnTheSea_Data\Managed"
$asmCSharp  = Join-Path $managedDir "Assembly-CSharp.dll"
$pluginDll  = Join-Path $out "WoTSAceAviators.dll"
$pluginsDir = Join-Path $gameRoot "BepInEx\plugins"

Write-Host "== Build plugin ==" -ForegroundColor Cyan
Push-Location $src
try {
    & dotnet build -c Release -v minimal | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "dotnet build failed ($LASTEXITCODE)" }
} finally { Pop-Location }

if (-not (Test-Path $pluginDll)) { throw "Build output not found: $pluginDll" }

# The guidance is the whole mod, so it is flown before it ships. The harness compiles the
# real guidance source and fails the build if any case misses.
Write-Host "`n== Fly the guidance (simulation) ==" -ForegroundColor Cyan
Push-Location (Join-Path $root "sim")
try {
    & dotnet run --project Sim.csproj -v minimal | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Guidance simulation reported a miss ($LASTEXITCODE)" }
} finally { Pop-Location }

# Harmony resolves its targets by name at runtime, so a signature that has drifted would
# compile and only fail in game. Check it against the real assembly first.
Write-Host "`n== Verify signatures against game assembly ==" -ForegroundColor Cyan
Push-Location (Join-Path $root "verify")
try {
    & dotnet run --project Verify.csproj -- $managedDir $asmCSharp | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Signature verification failed ($LASTEXITCODE)" }
} finally { Pop-Location }

Write-Host "`n== Install into game folder ==" -ForegroundColor Cyan
# A running game holds winhttp.dll and the loaded plugin DLLs open, so installing over them
# cannot work. Skip rather than fight it: killing the game to swap a DLL would throw away
# whatever the player is in the middle of, and robocopy's default is to retry a locked file
# a million times at 30 s intervals, which hangs this script for days rather than failing.
$running = @(Get-Process -Name "WarOnTheSea" -ErrorAction SilentlyContinue)
if ($running.Count -gt 0) {
    Write-Host "War on the Sea is running (PID $($running[0].Id)) - skipping install." -ForegroundColor Yellow
    Write-Host "The zips below are still built. Close the game and re-run to install." -ForegroundColor Yellow
} else {
    if (-not (Test-Path $pluginsDir)) { New-Item -ItemType Directory -Path $pluginsDir -Force | Out-Null }
    # robocopy merges directory trees correctly (Copy-Item -Recurse nests an existing
    # destination folder inside itself). Exit codes 0-7 are success. /R and /W bound the
    # retries so an unexpected lock fails fast instead of hanging.
    & robocopy $bepinex $gameRoot /E /IS /R:2 /W:2 /NFL /NDL /NJH /NJS
    if ($LASTEXITCODE -ge 8) { throw "robocopy install failed (exit $LASTEXITCODE)" }
    # robocopy signals success with 0-7 (3 = "copied some, destination had extras"), which
    # would otherwise leak out as this script's own exit code and read as a failure.
    $global:LASTEXITCODE = 0
    Copy-Item $pluginDll (Join-Path $pluginsDir "WoTSAceAviators.dll") -Force
    Write-Host "Installed BepInEx + plugin into $gameRoot"
}

Write-Host "`n== Package zips ==" -ForegroundColor Cyan
if (Test-Path $release) { Remove-Item $release -Recurse -Force }
$stagePack = Join-Path $root "_stage_pack"
$stageMod  = Join-Path $root "_stage_mod"
if (Test-Path $stagePack) { Remove-Item $stagePack -Recurse -Force }
if (Test-Path $stageMod)  { Remove-Item $stageMod  -Recurse -Force }
New-Item -ItemType Directory -Path $stagePack, $stageMod -Force | Out-Null

# Pack: full BepInEx tree + plugin + INSTALL.txt at zip root
Copy-Item (Join-Path $bepinex "*") $stagePack -Recurse -Force
$pluginInPack = Join-Path $stagePack "BepInEx\plugins"
if (-not (Test-Path $pluginInPack)) { New-Item -ItemType Directory -Path $pluginInPack -Force | Out-Null }
Copy-Item $pluginDll (Join-Path $pluginInPack "WoTSAceAviators.dll") -Force
Copy-Item (Join-Path $root "INSTALL.txt") (Join-Path $stagePack "INSTALL.txt") -Force
Copy-Item (Join-Path $root "README.txt")  (Join-Path $stagePack "README.txt")  -Force

# Mod: plugin only + README.txt
$modPlugins = Join-Path $stageMod "BepInEx\plugins"
New-Item -ItemType Directory -Path $modPlugins -Force | Out-Null
Copy-Item $pluginDll (Join-Path $modPlugins "WoTSAceAviators.dll") -Force
Copy-Item (Join-Path $root "README.txt") (Join-Path $stageMod "README.txt") -Force

New-Item -ItemType Directory -Path $release -Force | Out-Null
Compress-Archive -Path (Join-Path $stagePack "*") -DestinationPath (Join-Path $release "WoTSAceAviators-Pack.zip") -Force
Compress-Archive -Path (Join-Path $stageMod "*")  -DestinationPath (Join-Path $release "WoTSAceAviators.zip") -Force
Remove-Item $stagePack, $stageMod -Recurse -Force

Write-Host "`nDone. Artifacts:" -ForegroundColor Green
Get-ChildItem $release | ForEach-Object { "  $($_.FullName)  ($([math]::Round($_.Length/1KB)) KB)" }

exit 0
