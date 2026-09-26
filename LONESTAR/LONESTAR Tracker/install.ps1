<#
  Installs LONESTAR Tracker into the game's mod folder.
  The game must be closed: it holds an open handle on the mod DLL while running.

  Your logs are NOT touched by this script. They live in
  AppData\LocalLow\Shuxi\LONESTAR\Tracker, outside the mod folder, exactly so that
  reinstalling or updating the mod cannot take your history with it.
#>
[CmdletBinding()]
param(
    [string]$ModsRoot = "$env:USERPROFILE\AppData\LocalLow\Shuxi\LONESTAR\Mods"
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

if (Get-Process -Name LONESTAR -ErrorAction SilentlyContinue) {
    Write-Error "LONESTAR is running. Close the game first - it keeps the mod DLL open."
}

$dll = Join-Path $root 'src\LonestarTracker\bin\Release\LonestarTracker.dll'
if (-not (Test-Path $dll)) {
    Write-Error "Build output not found at $dll. Run: dotnet build -c Release src\LonestarTracker\LonestarTracker.csproj"
}

$target = Join-Path $ModsRoot 'Local\LonestarTracker'
New-Item -ItemType Directory -Force -Path $target | Out-Null

Copy-Item $dll (Join-Path $target 'LonestarTracker.dll') -Force
Copy-Item (Join-Path $root 'mod\mod.json') (Join-Path $target 'mod.json') -Force
Copy-Item (Join-Path $root 'mod\README.txt') (Join-Path $target 'README.txt') -Force

Write-Host "Installed to $target"
Write-Host "Start the game, open Mods, enable 'LONESTAR Tracker', then restart the game."
