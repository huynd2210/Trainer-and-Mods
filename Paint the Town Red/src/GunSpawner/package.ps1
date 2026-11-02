# Builds dist\GunSpawner.zip (mod only), dist\GunSpawner-Pack.zip (BepInEx + mod) and dist\GunSpawner-Source.zip.
# BepInEx is taken from the game folder's own install (BepInEx 6 IL2CPP be.781), minus generated/per-user data.
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$game = Join-Path $root '..\..\game' | Resolve-Path
$dist = Join-Path $root 'dist'
$stage = Join-Path $env:TEMP "gunspawner-stage-$PID"

dotnet build (Join-Path $root 'GunSpawner.csproj') -c Release -nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'build failed' }
$dll = Join-Path $root 'bin\Release\GunSpawner.dll'

New-Item -ItemType Directory -Force $dist | Out-Null
Remove-Item (Join-Path $dist '*.zip') -ErrorAction SilentlyContinue

try {
    # Mod only
    $mod = Join-Path $stage 'mod'
    New-Item -ItemType Directory -Force "$mod\BepInEx\plugins" | Out-Null
    Copy-Item $dll "$mod\BepInEx\plugins\"
    Copy-Item (Join-Path $root 'README.md') "$mod\GunSpawner-README.md"
    Compress-Archive -Path "$mod\*" -DestinationPath (Join-Path $dist 'GunSpawner.zip')

    # Pack: BepInEx loader + mod
    $pack = Join-Path $stage 'pack'
    New-Item -ItemType Directory -Force "$pack\BepInEx\plugins" | Out-Null
    Copy-Item (Join-Path $game 'BepInEx\core') "$pack\BepInEx\core" -Recurse
    Copy-Item (Join-Path $game 'dotnet') "$pack\dotnet" -Recurse
    Copy-Item (Join-Path $game 'winhttp.dll'), (Join-Path $game 'doorstop_config.ini'), (Join-Path $game '.doorstop_version') $pack
    Copy-Item $dll "$pack\BepInEx\plugins\"
    Copy-Item (Join-Path $root 'README.md') "$pack\GunSpawner-README.md"
    @"
Gun Spawner for Paint the Town Red - install
=============================================

1. Extract everything in this zip into the game folder, the one that contains PaintTheTownRed.exe.
   You should end up with BepInEx\, dotnet\, winhttp.dll and doorstop_config.ini next to the exe.
2. Start the game. The first launch takes a minute or two longer while BepInEx generates its files.
3. In any level press F7 to drop a gun in front of you (Shift+F7 = previous gun). Pick it up with E.

To uninstall, delete winhttp.dll (that disables BepInEx), or delete BepInEx\, dotnet\, winhttp.dll,
doorstop_config.ini and .doorstop_version.

Contains BepInEx 6.0.0-be.781 (IL2CPP), https://github.com/BepInEx/BepInEx
"@ | Set-Content "$pack\INSTALL.txt" -Encoding utf8
    Compress-Archive -Path "$pack\*" -DestinationPath (Join-Path $dist 'GunSpawner-Pack.zip')

    # Source
    $src = Join-Path $stage 'src'
    New-Item -ItemType Directory -Force $src | Out-Null
    Copy-Item (Join-Path $root '*.cs'), (Join-Path $root '*.csproj'), (Join-Path $root 'README.md'), (Join-Path $root 'package.ps1') $src
    Compress-Archive -Path "$src\*" -DestinationPath (Join-Path $dist 'GunSpawner-Source.zip')
}
finally {
    Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
}

Get-ChildItem $dist | Select-Object Name, @{n = 'MB'; e = { [math]::Round($_.Length / 1MB, 2) } }
