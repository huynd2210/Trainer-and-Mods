# Builds and packages the CULTIC Lighter Brighter BepInEx plugin.
# Produces:
#   LighterBrighter.zip       - the mod alone (BepInEx\plugins\LighterBrighterPlugin.dll + README)
#   LighterBrighter-Pack.zip  - BepInEx + the mod, self-contained (extract into game folder)
#
# Run from PowerShell in this folder:  .\package.ps1
# Rebuild is optional (set -SkipBuild). Requires the .NET Framework csc.exe.

param([switch]$SkipBuild)

$ErrorActionPreference = "Stop"
$game      = Resolve-Path ".."      # CULTIC game folder (this script lives in <game>\LighterBrighterSource)
$src       = $PSScriptRoot
$managed   = Join-Path $game "CULTIC_Data\Managed"
$core      = Join-Path $game "BepInEx\core"
$plugins   = Join-Path $game "BepInEx\plugins"
$release   = Join-Path $src "release"

function New-Zip([string]$FromDir, [string]$ZipPath) {
    if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }
    New-Item -ItemType Directory -Path (Split-Path $ZipPath) -Force | Out-Null
    Compress-Archive -Path "$FromDir\*" -DestinationPath $ZipPath -Force
    Write-Host "Created $ZipPath"
}

if (-not $SkipBuild) {
    Write-Host "Compiling LighterBrighterPlugin.dll..."
    $csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
    $refs = @(
        "/reference:$core\BepInEx.dll",
        "/reference:$core\0Harmony.dll",
        "/reference:$managed\Assembly-CSharp.dll",
        "/reference:$managed\netstandard.dll",
        "/reference:$managed\Unity.Netcode.Runtime.dll",
        "/reference:$managed\UnityEngine.dll",
        "/reference:$managed\UnityEngine.CoreModule.dll",
        "/reference:$managed\UnityEngine.IMGUIModule.dll",
        "/reference:$managed\UnityEngine.InputLegacyModule.dll"
    )
    & $csc /nologo /target:library "/out:$src\LighterBrighterPlugin.dll" @refs "$src\LighterBrighterPlugin.cs"
    if ($LASTEXITCODE -ne 0) { throw "Compile failed (exit $LASTEXITCODE)" }
    Copy-Item "$src\LighterBrighterPlugin.dll" $plugins -Force
    Write-Host "Installed to BepInEx\plugins\LighterBrighterPlugin.dll"
}

# ---- LighterBrighter.zip : mod only -----------------------------------------
$stageMod = Join-Path $env:TEMP "cultic_lighterbrighter_mod"
if (Test-Path $stageMod) { Remove-Item $stageMod -Recurse -Force }
New-Item -ItemType Directory -Path "$stageMod\BepInEx\plugins" -Force | Out-Null
Copy-Item "$src\LighterBrighterPlugin.dll" "$stageMod\BepInEx\plugins\" -Force
Copy-Item "$src\README.txt" "$stageMod\" -Force
New-Zip $stageMod (Join-Path $release "LighterBrighter.zip")

# ---- LighterBrighter-Pack.zip : BepInEx + mod, self-contained ----------------
$stagePack = Join-Path $env:TEMP "cultic_lighterbrighter_pack"
if (Test-Path $stagePack) { Remove-Item $stagePack -Recurse -Force }
New-Item -ItemType Directory -Path "$stagePack\BepInEx\core" -Force | Out-Null
New-Item -ItemType Directory -Path "$stagePack\BepInEx\config" -Force | Out-Null
New-Item -ItemType Directory -Path "$stagePack\BepInEx\plugins" -Force | Out-Null

Copy-Item "$core\*" "$stagePack\BepInEx\core\" -Force
Copy-Item "$game\BepInEx\config\BepInEx.cfg" "$stagePack\BepInEx\config\" -Force
Copy-Item "$src\LighterBrighterPlugin.dll" "$stagePack\BepInEx\plugins\" -Force
Copy-Item "$src\README.txt" "$stagePack\" -Force

# Doorstop bootstrap (required for BepInEx to load at all)
Copy-Item "$game\winhttp.dll"          "$stagePack\" -Force
Copy-Item "$game\doorstop_config.ini"  "$stagePack\" -Force
Copy-Item "$game\.doorstop_version"    "$stagePack\" -Force

# INSTALL.txt that covers only what this pack contains
@"
LighterBrighter-Pack - CULTIC Lighter Brighter, self-contained (BepInEx + the mod)
===========================================================================
Extract the CONTENTS of this zip into your CULTIC game folder so that
BepInEx\, winhttp.dll, doorstop_config.ini and .doorstop_version land next
to CULTIC.exe. Launch CULTIC.exe - done, no separate BepInEx install needed.

The lighter burns ~3.5x brighter with double the radius; F6 toggles the
boost, and the pocket-lamp beam is brighter and reaches farther too. Tune it in
BepInEx\config\local.codex.culticlighterbrighter.cfg (created on first run).
See README.txt for details.
"@ | Set-Content "$stagePack\INSTALL.txt" -Encoding utf8
New-Zip $stagePack (Join-Path $release "LighterBrighter-Pack.zip")

Write-Host ""
Write-Host "Done. Packages in $release"
Get-ChildItem $release | Select-Object Name, Length
