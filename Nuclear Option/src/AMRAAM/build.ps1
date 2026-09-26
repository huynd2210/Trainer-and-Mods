param([switch]$Install)
$ErrorActionPreference = 'Stop'
dotnet build (Join-Path $PSScriptRoot 'AMRAAM.csproj') -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'AMRAAM build failed' }
dotnet run --project (Join-Path $PSScriptRoot 'tests/TrajectoryTests.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Trajectory checks failed' }
if ($Install) {
    $gameRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
    $runningGame = Get-Process NuclearOption -ErrorAction SilentlyContinue
    if ($runningGame) { throw 'Close Nuclear Option before installing the plugin.' }
    $destination = Join-Path $gameRoot 'BepInEx/plugins/AMRAAM'
    New-Item -ItemType Directory -Force -Path $destination | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'bin/Release/NuclearOption.AMRAAM.dll') -Destination $destination
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'assets/amraam.meshbin') -Destination $destination
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'assets/amraam-paint.png') -Destination $destination
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Destination $destination
    Write-Output "Installed to $destination"
}
