$ErrorActionPreference = 'Stop'

$projectDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$gameDirectory = (Resolve-Path (Join-Path $projectDirectory '..\..')).Path
$pluginDirectory = Join-Path $gameDirectory 'BepInEx\plugins\PackageCounter'
$projectFile = Join-Path $projectDirectory 'PackageCounter.csproj'
$builtPlugin = Join-Path $projectDirectory 'bin\PackageCounter.dll'

dotnet build $projectFile --configuration Release
if ($LASTEXITCODE -ne 0) {
    throw "Package Counter failed to build."
}

New-Item -ItemType Directory -Force -Path $pluginDirectory | Out-Null
Copy-Item -LiteralPath $builtPlugin -Destination (Join-Path $pluginDirectory 'PackageCounter.dll') -Force

Write-Host "Installed Package Counter to $pluginDirectory"
