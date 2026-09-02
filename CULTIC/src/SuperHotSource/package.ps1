# Builds and packages the CULTIC SuperHot BepInEx plugin.
# Produces:
#   SuperHot.zip         - the mod alone (BepInEx\plugins\SuperHotPlugin.dll + README)
#   SuperHot-Pack.zip    - BepInEx + the mod, self-contained (extract into game folder)
#
# Run from PowerShell in this folder:  .\package.ps1
# Rebuild is optional (set -SkipBuild). Requires the .NET Framework csc.exe.

param([switch]$SkipBuild)

$ErrorActionPreference = "Stop"
$game      = Resolve-Path ".."      # CULTIC game folder (this script lives in <game>\SuperHotSource)
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
    Write-Host "Compiling SuperHotPlugin.dll..."
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
        "/reference:$managed\UnityEngine.PhysicsModule.dll",
        "/reference:$managed\Unity.InputSystem.dll"
    )
    & $csc /nologo /target:library "/out:$src\SuperHotPlugin.dll" @refs "$src\SuperHotPlugin.cs"
    if ($LASTEXITCODE -ne 0) { throw "Compile failed (exit $LASTEXITCODE)" }
    Copy-Item "$src\SuperHotPlugin.dll" $plugins -Force
    Write-Host "Installed to BepInEx\plugins\SuperHotPlugin.dll"
}

# ---- SuperHot.zip : mod only -------------------------------------------------
$stageMod = Join-Path $env:TEMP "cultic_superhot_mod"
if (Test-Path $stageMod) { Remove-Item $stageMod -Recurse -Force }
New-Item -ItemType Directory -Path "$stageMod\BepInEx\plugins" -Force | Out-Null
Copy-Item "$src\SuperHotPlugin.dll" "$stageMod\BepInEx\plugins\" -Force
Copy-Item "$src\README.txt" "$stageMod\" -Force
New-Zip $stageMod (Join-Path $release "SuperHot.zip")

# ---- SuperHot-Pack.zip : BepInEx + mod, self-contained -----------------------
$stagePack = Join-Path $env:TEMP "cultic_superhot_pack"
if (Test-Path $stagePack) { Remove-Item $stagePack -Recurse -Force }
New-Item -ItemType Directory -Path "$stagePack\BepInEx\core" -Force | Out-Null
New-Item -ItemType Directory -Path "$stagePack\BepInEx\config" -Force | Out-Null
New-Item -ItemType Directory -Path "$stagePack\BepInEx\plugins" -Force | Out-Null

Copy-Item "$core\*" "$stagePack\BepInEx\core\" -Force
Copy-Item "$game\BepInEx\config\BepInEx.cfg" "$stagePack\BepInEx\config\" -Force
Copy-Item "$src\SuperHotPlugin.dll" "$stagePack\BepInEx\plugins\" -Force
Copy-Item "$src\README.txt" "$stagePack\" -Force

# Doorstop bootstrap (required for BepInEx to load at all)
Copy-Item "$game\winhttp.dll"          "$stagePack\" -Force
Copy-Item "$game\doorstop_config.ini"  "$stagePack\" -Force
Copy-Item "$game\.doorstop_version"    "$stagePack\" -Force

# INSTALL.txt that covers only what this pack contains
@"
SuperHot-Pack - CULTIC SuperHot, self-contained (BepInEx + the mod)
=====================================================================
Extract the CONTENTS of this zip into your CULTIC game folder so that
BepInEx\, winhttp.dll, doorstop_config.ini and .doorstop_version land next
to CULTIC.exe. Launch CULTIC.exe - done, no separate BepInEx install needed.

] (RightBracket) toggles the time-freeze mechanic on/off. See README.txt for details and
the config file (BepInEx\config\local.codex.culticsuperhot.cfg).
"@ | Set-Content "$stagePack\INSTALL.txt" -Encoding utf8
New-Zip $stagePack (Join-Path $release "SuperHot-Pack.zip")

Write-Host ""
Write-Host "Done. Packages in $release"
Get-ChildItem $release | Select-Object Name, Length
