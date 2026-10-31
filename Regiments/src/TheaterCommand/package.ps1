# Theater Command packaging: builds the plugin, installs it into the game folder, and produces
# the distributable zips per the BepInEx convention:
#   release\TheaterCommand.zip         (plugin only, for users who already have BepInEx)
#   release\TheaterCommand-Pack.zip    (BepInEx 5.4.23.2 x64 + plugin, self-contained)
#   release\TheaterCommand-Source.zip  (source, including the balance simulator)
$ErrorActionPreference = "Stop"

$root       = Split-Path -Parent $MyInvocation.MyCommand.Path
$gameRoot   = Join-Path $root "..\..\game"
$out        = Join-Path $root "out"
$bepinex    = Join-Path $root "..\tools\bepinex"
$release    = Join-Path $root "release"
$pluginDll  = Join-Path $out "TheaterCommand.dll"
$pluginsDir = Join-Path $gameRoot "BepInEx\plugins"

Write-Host "== Build plugin ==" -ForegroundColor Cyan
& dotnet build (Join-Path $root "src\TheaterCommand.csproj") -c Release -v minimal -nologo | Out-Host
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed ($LASTEXITCODE)" }
if (-not (Test-Path $pluginDll)) { throw "Build output not found: $pluginDll" }

Write-Host "`n== Install into game folder ==" -ForegroundColor Cyan
# A running game holds the loaded plugin DLLs open. Skip rather than kill it: that would throw
# away whatever the player is in the middle of.
$running = @(Get-Process -Name "Regiments" -ErrorAction SilentlyContinue)
if ($running.Count -gt 0) {
    Write-Host "Regiments is running (PID $($running[0].Id)) - skipping install." -ForegroundColor Yellow
    Write-Host "The zips below are still built. Close the game and re-run to install." -ForegroundColor Yellow
} else {
    if (-not (Test-Path $pluginsDir)) { New-Item -ItemType Directory -Path $pluginsDir -Force | Out-Null }
    # robocopy merges trees; 0-7 are success. /R /W bound retries so a lock fails fast.
    & robocopy $bepinex $gameRoot /E /IS /R:2 /W:2 /NFL /NDL /NJH /NJS
    if ($LASTEXITCODE -ge 8) { throw "robocopy install failed (exit $LASTEXITCODE)" }
    $global:LASTEXITCODE = 0
    Copy-Item $pluginDll (Join-Path $pluginsDir "TheaterCommand.dll") -Force
    Write-Host "Installed BepInEx + plugin into $gameRoot"
}

Write-Host "`n== Package zips ==" -ForegroundColor Cyan
if (Test-Path $release) { Remove-Item $release -Recurse -Force }
$stagePack = Join-Path $root "_stage_pack"
$stageMod  = Join-Path $root "_stage_mod"
$stageSrc  = Join-Path $root "_stage_src"
foreach ($d in $stagePack, $stageMod, $stageSrc) { if (Test-Path $d) { Remove-Item $d -Recurse -Force } }
New-Item -ItemType Directory -Path $stagePack, $stageMod, $stageSrc -Force | Out-Null

# Pack: full BepInEx tree + plugin + INSTALL.txt at zip root
Copy-Item (Join-Path $bepinex "*") $stagePack -Recurse -Force
$pluginInPack = Join-Path $stagePack "BepInEx\plugins"
if (-not (Test-Path $pluginInPack)) { New-Item -ItemType Directory -Path $pluginInPack -Force | Out-Null }
Copy-Item $pluginDll (Join-Path $pluginInPack "TheaterCommand.dll") -Force
Copy-Item (Join-Path $root "INSTALL.txt") (Join-Path $stagePack "INSTALL.txt") -Force

# Mod: plugin only + README.txt
$modPlugins = Join-Path $stageMod "BepInEx\plugins"
New-Item -ItemType Directory -Path $modPlugins -Force | Out-Null
Copy-Item $pluginDll (Join-Path $modPlugins "TheaterCommand.dll") -Force
Copy-Item (Join-Path $root "README.txt") (Join-Path $stageMod "README.txt") -Force

# Source: project without build output
foreach ($item in "src", "sim", "package.ps1", "README.txt", "INSTALL.txt") {
    Copy-Item (Join-Path $root $item) $stageSrc -Recurse -Force
}
Get-ChildItem $stageSrc -Recurse -Directory | Where-Object { $_.Name -in "bin", "obj" } | Remove-Item -Recurse -Force
Get-ChildItem $stageSrc -Recurse -File -Filter *.png | Remove-Item -Force

New-Item -ItemType Directory -Path $release -Force | Out-Null
Compress-Archive -Path (Join-Path $stagePack "*") -DestinationPath (Join-Path $release "TheaterCommand-Pack.zip") -Force
Compress-Archive -Path (Join-Path $stageMod "*")  -DestinationPath (Join-Path $release "TheaterCommand.zip") -Force
Compress-Archive -Path (Join-Path $stageSrc "*")  -DestinationPath (Join-Path $release "TheaterCommand-Source.zip") -Force
Remove-Item $stagePack, $stageMod, $stageSrc -Recurse -Force

Write-Host "`nDone. Artifacts:" -ForegroundColor Green
Get-ChildItem $release | ForEach-Object { "  $($_.FullName)  ($([math]::Round($_.Length/1KB)) KB)" }
exit 0
