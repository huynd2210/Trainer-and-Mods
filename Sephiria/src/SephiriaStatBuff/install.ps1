$ErrorActionPreference = 'Stop'
$gameDirectory = Split-Path -Parent $PSScriptRoot
if (Get-Process Sephiria -ErrorAction SilentlyContinue) { throw 'Close Sephiria before installing the stat buff.' }
dotnet build (Join-Path $PSScriptRoot 'StatBuff/StatBuff.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'StatBuff build failed.' }
dotnet build (Join-Path $PSScriptRoot 'Patcher/Patcher.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Patcher build failed.' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'SephiriaStatBuff.cfg') -Destination (Join-Path $PSScriptRoot 'Patcher/bin/Release/net8.0/SephiriaStatBuff.cfg') -Force
dotnet (Join-Path $PSScriptRoot 'Patcher/bin/Release/net8.0/Patcher.dll') install $gameDirectory
if ($LASTEXITCODE -ne 0) { throw 'Stat buff installation failed.' }
