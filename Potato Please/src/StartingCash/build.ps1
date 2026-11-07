$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$game = (Resolve-Path (Join-Path $here '..\..\Potato Please')).Path
dotnet build (Join-Path $here 'StartingCash.csproj') --configuration Release
if ($LASTEXITCODE -ne 0) { throw 'Starting Cash failed to build.' }
Copy-Item (Join-Path $here 'bin\StartingCash.dll') (Join-Path $game 'BepInEx\plugins\StartingCash.dll') -Force
Write-Host "Installed StartingCash.dll to $game\BepInEx\plugins"
