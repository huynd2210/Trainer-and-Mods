$ErrorActionPreference = 'Stop'
$here   = Split-Path -Parent $MyInvocation.MyCommand.Path
$vendor = Join-Path $here '..\vendor\BepInEx-be781-IL2CPP-x64'
$dist   = Join-Path $here 'dist'
$stage  = Join-Path $env:TEMP 'SteadyHands-stage'
$dll    = Join-Path $here 'bin\SteadyHands.dll'
if (-not (Test-Path $dll)) { throw 'Run build.ps1 first.' }
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force $dist, "$stage\mod\BepInEx\plugins", "$stage\pack\BepInEx\plugins", "$stage\src\SteadyHands" | Out-Null

Copy-Item $dll "$stage\mod\BepInEx\plugins\"
Copy-Item (Join-Path $here 'README.md') "$stage\mod\SteadyHands-README.md"

Get-ChildItem $vendor -Force | Copy-Item -Destination "$stage\pack" -Recurse -Force
Copy-Item $dll "$stage\pack\BepInEx\plugins\"
Copy-Item (Join-Path $here 'INSTALL.txt') "$stage\pack\"
Copy-Item (Join-Path $here 'README.md') "$stage\pack\SteadyHands-README.md"

foreach ($f in 'Plugin.cs','SteadyHands.csproj','build.ps1','package.ps1','README.md','INSTALL.txt') { Copy-Item (Join-Path $here $f) "$stage\src\SteadyHands\" }

foreach ($z in @(@('mod','SteadyHands.zip'), @('pack','SteadyHands-Pack.zip'), @('src','SteadyHands-Source.zip'))) {
    $out = Join-Path $dist $z[1]
    if (Test-Path $out) { Remove-Item $out -Force }
    Compress-Archive -Path "$stage\$($z[0])\*" -DestinationPath $out
}
Remove-Item $stage -Recurse -Force
Get-ChildItem $dist | Select-Object Name, Length
