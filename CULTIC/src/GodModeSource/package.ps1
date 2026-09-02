# Builds and packages the CULTIC God Mode BepInEx plugin.
# Produces:
#   GodMode.zip       - the mod alone (BepInEx\plugins\GodModePlugin.dll + README)
#   GodMode-Pack.zip  - BepInEx + the mod, self-contained (extract into game folder)

param([switch]$SkipBuild)

$ErrorActionPreference = "Stop"
$game = Resolve-Path ".."
$source = $PSScriptRoot
$managed = Join-Path $game "CULTIC_Data\Managed"
$core = Join-Path $game "BepInEx\core"
$plugins = Join-Path $game "BepInEx\plugins"
$release = Join-Path $source "release"
$compiler = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

function New-Zip([string]$FromDir, [string]$ZipPath) {
    if (Test-Path -LiteralPath $ZipPath) {
        Remove-Item -LiteralPath $ZipPath -Force
    }
    New-Item -ItemType Directory -Path (Split-Path $ZipPath) -Force | Out-Null
    Compress-Archive -Path "$FromDir\*" -DestinationPath $ZipPath -Force
    Write-Host "Created $ZipPath"
}

if (-not $SkipBuild) {
    Write-Host "Compiling GodModePlugin.dll..."
    $references = @(
        "/reference:$core\BepInEx.dll",
        "/reference:$core\0Harmony.dll",
        "/reference:$managed\Assembly-CSharp.dll",
        "/reference:$managed\netstandard.dll",
        "/reference:$managed\Unity.Netcode.Runtime.dll",
        "/reference:$managed\UnityEngine.dll",
        "/reference:$managed\UnityEngine.CoreModule.dll",
        "/reference:$managed\UnityEngine.IMGUIModule.dll",
        "/reference:$managed\UnityEngine.InputLegacyModule.dll",
        "/reference:$managed\UnityEngine.PhysicsModule.dll"
    )

    & $compiler /nologo /target:library "/out:$source\GodModePlugin.dll" `
        @references "$source\GodModePlugin.cs"
    if ($LASTEXITCODE -ne 0) {
        throw "Compile failed (exit $LASTEXITCODE)"
    }
    Copy-Item "$source\GodModePlugin.dll" "$plugins\GodModePlugin.dll" -Force
    Write-Host "Installed to BepInEx\plugins\GodModePlugin.dll"
}

$stageRoot = Join-Path ([IO.Path]::GetTempPath()) ("cultic_godmode_" + [Guid]::NewGuid().ToString("N"))
try {
    $stageMod = Join-Path $stageRoot "mod"
    New-Item -ItemType Directory -Path "$stageMod\BepInEx\plugins" -Force | Out-Null
    Copy-Item "$source\GodModePlugin.dll" "$stageMod\BepInEx\plugins\" -Force
    Copy-Item "$source\README.txt" "$stageMod\" -Force
    New-Zip $stageMod (Join-Path $release "GodMode.zip")

    $stagePack = Join-Path $stageRoot "pack"
    New-Item -ItemType Directory -Path "$stagePack\BepInEx\core" -Force | Out-Null
    New-Item -ItemType Directory -Path "$stagePack\BepInEx\config" -Force | Out-Null
    New-Item -ItemType Directory -Path "$stagePack\BepInEx\plugins" -Force | Out-Null
    Copy-Item "$core\*" "$stagePack\BepInEx\core\" -Force
    Copy-Item "$game\BepInEx\config\BepInEx.cfg" "$stagePack\BepInEx\config\" -Force
    Copy-Item "$source\GodModePlugin.dll" "$stagePack\BepInEx\plugins\" -Force
    Copy-Item "$source\README.txt" "$stagePack\" -Force
    Copy-Item "$game\winhttp.dll" "$stagePack\" -Force
    Copy-Item "$game\doorstop_config.ini" "$stagePack\" -Force
    Copy-Item "$game\.doorstop_version" "$stagePack\" -Force
    @"
GodMode-Pack - CULTIC God Mode, self-contained (BepInEx + the mod)
=================================================================
Extract the CONTENTS of this zip into your CULTIC game folder so that
BepInEx\, winhttp.dll, doorstop_config.ini and .doorstop_version land next
to CULTIC.exe. Launch CULTIC.exe - done, no separate BepInEx install needed.

Insert toggles God Mode, F10 toggles flight, F11 freezes the world, F12 toggles
super speed, and Home restocks the player. See README.txt for full details.
"@ | Set-Content "$stagePack\INSTALL.txt" -Encoding utf8
    New-Zip $stagePack (Join-Path $release "GodMode-Pack.zip")
}
finally {
    if (Test-Path -LiteralPath $stageRoot) {
        $resolvedStage = (Resolve-Path -LiteralPath $stageRoot).Path
        $resolvedTemp = (Resolve-Path -LiteralPath ([IO.Path]::GetTempPath())).Path
        if (-not $resolvedStage.StartsWith($resolvedTemp, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to remove staging directory outside the system temp directory: $resolvedStage"
        }
        Remove-Item -LiteralPath $resolvedStage -Recurse -Force
    }
}

Write-Host "Done. Packages in $release"
Get-ChildItem -LiteralPath $release -File -Filter "*.zip" | Select-Object Name, Length
