$ErrorActionPreference = 'Stop'
$gameDirectory = Split-Path -Parent $PSScriptRoot
if (Get-Process Sephiria -ErrorAction SilentlyContinue) { throw 'Close Sephiria before installing the trainer.' }
dotnet build (Join-Path $PSScriptRoot 'TrainerHooks/TrainerHooks.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'TrainerHooks build failed.' }
dotnet build (Join-Path $PSScriptRoot 'Patcher/Patcher.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Patcher build failed.' }
dotnet (Join-Path $PSScriptRoot 'Patcher/bin/Release/net8.0/Patcher.dll') install $gameDirectory
if ($LASTEXITCODE -ne 0) { throw 'Trainer installation failed.' }
