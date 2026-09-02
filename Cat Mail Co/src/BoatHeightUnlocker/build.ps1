$ErrorActionPreference = 'Stop'

$projectDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$gameDirectory = (Resolve-Path (Join-Path $projectDirectory '..\..')).Path
$pluginDirectory = Join-Path $gameDirectory 'BepInEx\plugins\BoatHeightUnlocker'
$projectFile = Join-Path $projectDirectory 'BoatHeightUnlocker.csproj'
$builtPlugin = Join-Path $projectDirectory 'bin\BoatHeightUnlocker.dll'

dotnet build $projectFile --configuration Release
if ($LASTEXITCODE -ne 0) {
    throw "Boat Height Unlocker failed to build."
}

New-Item -ItemType Directory -Force -Path $pluginDirectory | Out-Null
Copy-Item -LiteralPath $builtPlugin -Destination (Join-Path $pluginDirectory 'BoatHeightUnlocker.dll') -Force

Write-Host "Installed Boat Height Unlocker to $pluginDirectory"
