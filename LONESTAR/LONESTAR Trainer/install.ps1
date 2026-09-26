<#
  Installs LONESTAR Trainer into the game's mod folder.
  The game must be closed: it holds an open handle on the mod DLL while running.
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

$dll = Join-Path $root 'src\LonestarTrainer\bin\Release\LonestarTrainer.dll'
if (-not (Test-Path $dll)) {
    Write-Error "Build output not found at $dll. Run: dotnet build -c Release src\LonestarTrainer\LonestarTrainer.csproj"
}

$target = Join-Path $ModsRoot 'Local\LonestarTrainer'
New-Item -ItemType Directory -Force -Path $target | Out-Null

Copy-Item $dll (Join-Path $target 'LonestarTrainer.dll') -Force
Copy-Item (Join-Path $root 'mod\mod.json') (Join-Path $target 'mod.json') -Force
Copy-Item (Join-Path $root 'mod\README.txt') (Join-Path $target 'README.txt') -Force

Write-Host "Installed to $target"
Write-Host "Start the game, open Mods, enable 'LONESTAR Trainer', then restart the game."
