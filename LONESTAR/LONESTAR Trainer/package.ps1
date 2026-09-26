<#
  Builds the mod and writes the distributable zips to dist\.

  No loader is bundled: LONESTAR has its own C# mod loader with Harmony built in,
  so the usual "-Pack" zip (loader + mod) has nothing to carry and is not produced.
#>
[CmdletBinding()]
param(
    [string]$GameManaged = 'C:\Games\LONESTAR.v2026.03.05\game\LONESTAR_Data\Managed'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$dist = Join-Path $root 'dist'
$proj = Join-Path $root 'src\LonestarTrainer\LonestarTrainer.csproj'

New-Item -ItemType Directory -Force -Path $dist | Out-Null

Write-Host 'Building...'
& dotnet build -c Release $proj "-p:GameManaged=$GameManaged" -v minimal
if ($LASTEXITCODE -ne 0) { Write-Error 'Build failed.' }

$dll = Join-Path $root 'src\LonestarTrainer\bin\Release\LonestarTrainer.dll'

# --- mod zip: the folder the player drops into Mods\Local ---
$stage = Join-Path $env:TEMP ("lonestar-trainer-" + [guid]::NewGuid().ToString('N'))
$inner = Join-Path $stage 'LonestarTrainer'
New-Item -ItemType Directory -Force -Path $inner | Out-Null
Copy-Item $dll $inner
Copy-Item (Join-Path $root 'mod\mod.json') $inner
Copy-Item (Join-Path $root 'mod\README.txt') $inner

$modZip = Join-Path $dist 'LONESTAR-Trainer.zip'
if (Test-Path $modZip) { Remove-Item $modZip }
Compress-Archive -Path $inner -DestinationPath $modZip
Remove-Item $stage -Recurse -Force

# --- source zip ---
$srcStage = Join-Path $env:TEMP ("lonestar-trainer-src-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $srcStage | Out-Null
Copy-Item (Join-Path $root 'src') $srcStage -Recurse
Copy-Item (Join-Path $root 'mod') $srcStage -Recurse
Copy-Item (Join-Path $root 'README.md') $srcStage
Copy-Item (Join-Path $root 'package.ps1') $srcStage
Copy-Item (Join-Path $root 'install.ps1') $srcStage
Get-ChildItem $srcStage -Recurse -Directory |
    Where-Object { $_.Name -in @('bin', 'obj') } |
    Remove-Item -Recurse -Force

$srcZip = Join-Path $dist 'LONESTAR-Trainer-Source.zip'
if (Test-Path $srcZip) { Remove-Item $srcZip }
Compress-Archive -Path (Join-Path $srcStage '*') -DestinationPath $srcZip
Remove-Item $srcStage -Recurse -Force

Get-ChildItem $dist | Select-Object Name, Length
