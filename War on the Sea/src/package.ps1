# WoTSTrainer packaging: builds the plugin, verifies signatures, installs into the
# game folder, and produces the two distributable zips per the BepInEx convention:
#   release\WoTSTrainer.zip       (plugin only, for users who already have BepInEx)
#   release\WoTSTrainer-Pack.zip  (BepInEx 5.4.23.2 x64 + plugin, self-contained)
$ErrorActionPreference = "Stop"

$root       = Split-Path -Parent $MyInvocation.MyCommand.Path
$gameRoot   = Join-Path $root "..\game"
$src        = Join-Path $root "src"
$out        = Join-Path $root "out"
$bepinex    = Join-Path $root "tools\bepinex"
$release    = Join-Path $root "release"
$managedDir = Join-Path $gameRoot "WarOnTheSea_Data\Managed"
$asmCSharp  = Join-Path $managedDir "Assembly-CSharp.dll"
$pluginDll  = Join-Path $out "WoTSTrainer.dll"
$pluginsDir = Join-Path $gameRoot "BepInEx\plugins"

Write-Host "== Build plugin ==" -ForegroundColor Cyan
Push-Location $src
try {
    & dotnet build -c Release -v minimal | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "dotnet build failed ($LASTEXITCODE)" }
} finally { Pop-Location }

if (-not (Test-Path $pluginDll)) { throw "Build output not found: $pluginDll" }

Write-Host "`n== Verify signatures against game assembly ==" -ForegroundColor Cyan
Push-Location (Join-Path $root "verify")
try {
    & dotnet run --project Verify.csproj -- $managedDir $asmCSharp $out | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Signature verification failed ($LASTEXITCODE)" }
} finally { Pop-Location }

Write-Host "`n== Install into game folder ==" -ForegroundColor Cyan
if (-not (Test-Path $pluginsDir)) { New-Item -ItemType Directory -Path $pluginsDir -Force | Out-Null }
# robocopy merges directory trees correctly (Copy-Item -Recurse nests an existing
# destination folder inside itself). Exit codes 0-7 are success.
& robocopy $bepinex $gameRoot /E /IS /NFL /NDL /NJH /NJS
if ($LASTEXITCODE -ge 8) { throw "robocopy install failed (exit $LASTEXITCODE)" }
Copy-Item $pluginDll (Join-Path $pluginsDir "WoTSTrainer.dll") -Force
Write-Host "Installed BepInEx + plugin into $gameRoot"

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
Copy-Item $pluginDll (Join-Path $pluginInPack "WoTSTrainer.dll") -Force
Copy-Item (Join-Path $root "INSTALL.txt") (Join-Path $stagePack "INSTALL.txt") -Force

# Mod: plugin only + README.txt
$modPlugins = Join-Path $stageMod "BepInEx\plugins"
New-Item -ItemType Directory -Path $modPlugins -Force | Out-Null
Copy-Item $pluginDll (Join-Path $modPlugins "WoTSTrainer.dll") -Force
Copy-Item (Join-Path $root "README.txt") (Join-Path $stageMod "README.txt") -Force

New-Item -ItemType Directory -Path $release -Force | Out-Null
Compress-Archive -Path (Join-Path $stagePack "*") -DestinationPath (Join-Path $release "WoTSTrainer-Pack.zip") -Force
Compress-Archive -Path (Join-Path $stageMod "*")  -DestinationPath (Join-Path $release "WoTSTrainer.zip") -Force
Remove-Item $stagePack, $stageMod -Recurse -Force

Write-Host "`nDone. Artifacts:" -ForegroundColor Green
Get-ChildItem $release | ForEach-Object { "  $($_.FullName)  ($([math]::Round($_.Length/1KB)) KB)" }
