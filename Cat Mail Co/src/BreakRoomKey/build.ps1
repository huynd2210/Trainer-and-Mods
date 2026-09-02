$ErrorActionPreference = 'Stop'

$projectDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$gameDirectory = (Resolve-Path (Join-Path $projectDirectory '..\..')).Path
$pluginDirectory = Join-Path $gameDirectory 'BepInEx\plugins\BreakRoomKey'
$projectFile = Join-Path $projectDirectory 'BreakRoomKey.csproj'
$builtPlugin = Join-Path $projectDirectory 'bin\BreakRoomKey.dll'

dotnet build $projectFile --configuration Release
if ($LASTEXITCODE -ne 0) {
    throw "Break Room Key failed to build."
}

New-Item -ItemType Directory -Force -Path $pluginDirectory | Out-Null
Copy-Item -LiteralPath $builtPlugin -Destination (Join-Path $pluginDirectory 'BreakRoomKey.dll') -Force

Write-Host "Installed Break Room Key to $pluginDirectory"
