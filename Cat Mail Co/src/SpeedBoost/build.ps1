$ErrorActionPreference = 'Stop'

$projectDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$gameDirectory = (Resolve-Path (Join-Path $projectDirectory '..\..')).Path
$pluginDirectory = Join-Path $gameDirectory 'BepInEx\plugins\SpeedBoost'
$projectFile = Join-Path $projectDirectory 'SpeedBoost.csproj'
$builtPlugin = Join-Path $projectDirectory 'bin\SpeedBoost.dll'

dotnet build $projectFile --configuration Release
if ($LASTEXITCODE -ne 0) {
    throw "Speed Boost failed to build."
}

New-Item -ItemType Directory -Force -Path $pluginDirectory | Out-Null
Copy-Item -LiteralPath $builtPlugin -Destination (Join-Path $pluginDirectory 'SpeedBoost.dll') -Force

Write-Host "Installed Speed Boost to $pluginDirectory"
