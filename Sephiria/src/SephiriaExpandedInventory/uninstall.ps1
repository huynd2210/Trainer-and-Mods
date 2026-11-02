$ErrorActionPreference = 'Stop'
$gameDirectory = Split-Path -Parent $PSScriptRoot
if (Get-Process Sephiria -ErrorAction SilentlyContinue) { throw 'Close Sephiria before uninstalling expanded inventory.' }
dotnet (Join-Path $PSScriptRoot 'Patcher/bin/Release/net8.0/Patcher.dll') uninstall $gameDirectory
if ($LASTEXITCODE -ne 0) { throw 'Expanded inventory uninstall failed.' }
