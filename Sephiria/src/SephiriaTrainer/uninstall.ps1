$ErrorActionPreference = 'Stop'
$gameDirectory = Split-Path -Parent $PSScriptRoot
if (Get-Process Sephiria -ErrorAction SilentlyContinue) { throw 'Close Sephiria before removing the trainer.' }
dotnet (Join-Path $PSScriptRoot 'Patcher/bin/Release/net8.0/Patcher.dll') uninstall $gameDirectory
if ($LASTEXITCODE -ne 0) { throw 'Trainer removal failed.' }
