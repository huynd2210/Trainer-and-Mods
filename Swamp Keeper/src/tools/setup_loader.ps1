$ErrorActionPreference = 'Stop'
$gameRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$vendorFolder = Join-Path $gameRoot 'ModsSource\vendor'
New-Item -ItemType Directory -Force -Path $vendorFolder | Out-Null
$archive = Join-Path $vendorFolder 'BepInEx-IL2CPP-x86.zip'
$source = 'https://builds.bepinex.dev/projects/bepinex_be/789/BepInEx-Unity.IL2CPP-win-x86-6.0.0-be.789%2B6cea286.zip'
if (-not (Test-Path -LiteralPath $archive)) { Invoke-WebRequest -Uri $source -OutFile $archive }
Expand-Archive -LiteralPath $archive -DestinationPath $gameRoot -Force
Write-Output 'Installed BepInEx IL2CPP x86 build 789. Launch the game once to generate its interop assemblies before building the plugin.'
