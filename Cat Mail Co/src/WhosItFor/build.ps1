$ErrorActionPreference = 'Stop'

$projectDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$gameDirectory = (Resolve-Path (Join-Path $projectDirectory '..\..')).Path
$pluginDirectory = Join-Path $gameDirectory 'BepInEx\plugins\WhosItFor'
$projectFile = Join-Path $projectDirectory 'WhosItFor.csproj'
$builtPlugin = Join-Path $projectDirectory 'bin\WhosItFor.dll'

dotnet build $projectFile --configuration Release
if ($LASTEXITCODE -ne 0) {
    throw "Who's It For? failed to build."
}

New-Item -ItemType Directory -Force -Path $pluginDirectory | Out-Null
Copy-Item -LiteralPath $builtPlugin -Destination (Join-Path $pluginDirectory 'WhosItFor.dll') -Force

Write-Host "Installed Who's It For? to $pluginDirectory"



