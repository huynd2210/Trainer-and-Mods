param([switch]$Install)
$ErrorActionPreference = 'Stop'
dotnet build (Join-Path $PSScriptRoot 'MissileControl.csproj') -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
dotnet run --project (Join-Path $PSScriptRoot 'tests/AimStateTests.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Aim tests failed' }
if ($Install) {
    if (Get-Process NuclearOption -ErrorAction SilentlyContinue) { throw 'Close Nuclear Option before replacing the plugin.' }
    $gameRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
    $destination = Join-Path $gameRoot 'BepInEx/plugins/MissileControl'
    New-Item -ItemType Directory -Force -Path $destination | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'bin/Release/NuclearOption.MissileControl.dll') -Destination $destination
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Destination $destination
    Write-Output "Installed to $destination"
}
