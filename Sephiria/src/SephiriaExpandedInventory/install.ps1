$ErrorActionPreference = 'Stop'
$gameDirectory = Split-Path -Parent $PSScriptRoot
if (Get-Process Sephiria -ErrorAction SilentlyContinue) { throw 'Close Sephiria before installing expanded inventory.' }
dotnet build (Join-Path $PSScriptRoot 'ExpandedInventory/ExpandedInventory.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'ExpandedInventory build failed.' }
dotnet build (Join-Path $PSScriptRoot 'Patcher/Patcher.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Patcher build failed.' }
dotnet (Join-Path $PSScriptRoot 'Patcher/bin/Release/net8.0/Patcher.dll') install $gameDirectory
if ($LASTEXITCODE -ne 0) { throw 'Expanded inventory installation failed.' }
$configPath = Join-Path $gameDirectory 'SephiriaExpandedInventory.cfg'
if (!(Test-Path -LiteralPath $configPath)) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'SephiriaExpandedInventory.cfg') -Destination $configPath }
