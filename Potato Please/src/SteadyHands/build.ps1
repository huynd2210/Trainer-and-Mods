$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$game = (Resolve-Path (Join-Path $here '..\..\Potato Please')).Path
dotnet build (Join-Path $here 'SteadyHands.csproj') --configuration Release
if ($LASTEXITCODE -ne 0) { throw 'Steady Hands failed to build.' }
Copy-Item (Join-Path $here 'bin\SteadyHands.dll') (Join-Path $game 'BepInEx\plugins\SteadyHands.dll') -Force
Write-Host "Installed SteadyHands.dll to $game\BepInEx\plugins"
