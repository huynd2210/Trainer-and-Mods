<#
    Builds Swamp Keeper Online Co-op and produces three zips in .\dist :

      SwampKeeperCoop.zip         the co-op plugin + its required Swamp Keeper Mods 1.2.0 + docs
                                  (for players who already have BepInEx 6 be.789 IL2CPP x86)
      SwampKeeperCoop-Pack.zip    BepInEx 6.0.0-be.789 IL2CPP x86 + the above + INSTALL.txt (self-contained)
      SwampKeeperCoop-Source.zip  co-op and Swamp Keeper Mods source, tests and test launchers, without bin/obj/dist

    Every zip extracts directly into the Swamp Keeper v1.05 game folder (the one with "Swamp Keeper.exe").
    Builds deploy only into ModsSource\coop-investigation\sandbox; nothing here writes into the normal game install.

    Usage:  powershell -File package.ps1 [-SkipTests]
#>
param([switch]$SkipTests)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$Root     = $PSScriptRoot                                   # ModsSource\SwampKeeperCoop
$Sources  = Split-Path $Root                                # ModsSource
$Game     = Split-Path $Sources                             # game folder
$Sandbox  = Join-Path $Sources 'coop-investigation\sandbox'
$Dist     = Join-Path $Root 'dist'
$Staging  = Join-Path $Root 'obj\staging'
$BepZip   = Join-Path $Sources 'vendor\BepInEx-IL2CPP-x86.zip'
$UnityLib = Join-Path $Game 'BepInEx\unity-libs\2022.3.62.zip'
$Name     = 'SwampKeeperCoop'
$CoopOut  = Join-Path $Root 'Runtime\bin\Release\net6.0'
$ModsDll  = Join-Path $Sources 'SwampKeeperMods\bin\Release\net6.0\SwampKeeperMods.dll'

function Invoke-Dotnet([string[]]$Arguments, [string]$What) {
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$What failed ($LASTEXITCODE)." }
}

# ---------------------------------------------------------------- build (and test)

# GameDir points both builds at the sandbox copy: their Deploy step must never touch the normal game install.
$gameDirArg = '-p:GameDir=' + $Sandbox + '\\'
Write-Host 'Building Swamp Keeper Mods 1.2.0...'
Invoke-Dotnet @('build', (Join-Path $Sources 'SwampKeeperMods'), '-c', 'Release', '--nologo', '-v', 'quiet', $gameDirArg) 'Swamp Keeper Mods build'
Write-Host "Building $Name..."
Invoke-Dotnet @('build', (Join-Path $Root 'Runtime'), '-c', 'Release', '--nologo', '-v', 'quiet', $gameDirArg) 'Co-op build'

if (-not $SkipTests) {
    Write-Host 'Running storage and protocol tests...'
    Invoke-Dotnet @('run', '--project', (Join-Path $Sources 'tests'), '-c', 'Release', '--nologo', '-v', 'quiet') 'Storage tests'
    Invoke-Dotnet @('run', '--project', (Join-Path $Root 'ProtocolTests'), '-c', 'Release', '--nologo', '-v', 'quiet') 'Protocol tests'
}

# The co-op plugin declares [BepInDependency("local.swampkeeper.mods","1.2.0")]; refuse to ship any other version.
$modsBytes = [IO.File]::ReadAllBytes($ModsDll)
$modsText  = [Text.Encoding]::UTF8.GetString($modsBytes)
if (-not ($modsText.Contains('local.swampkeeper.mods') -and $modsText.Contains('1.2.0'))) { throw "$ModsDll is not Swamp Keeper Mods 1.2.0." }
foreach ($f in 'SwampKeeperCoop.dll', 'SwampKeeperCoop.Protocol.dll') {
    if (-not (Test-Path (Join-Path $CoopOut $f))) { throw "Missing build output: $f" }
}

# ---------------------------------------------------------------- helpers

function New-Stage([string]$Label) {
    $dir = Join-Path $Staging $Label
    if (Test-Path $dir) { Remove-Item $dir -Recurse -Force }
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    return $dir
}

function Add-Mod([string]$Stage) {
    $coop = Join-Path $Stage 'BepInEx\plugins\SwampKeeperCoop'
    $mods = Join-Path $Stage 'BepInEx\plugins\SwampKeeperMods'
    New-Item -ItemType Directory -Path $coop, $mods -Force | Out-Null
    Copy-Item (Join-Path $CoopOut 'SwampKeeperCoop.dll'), (Join-Path $CoopOut 'SwampKeeperCoop.Protocol.dll') $coop -Force
    Copy-Item $ModsDll $mods -Force
    Copy-Item (Join-Path $Root 'PLAYER-README.md') (Join-Path $Stage 'SwampKeeperCoop-README.md') -Force
    Copy-Item (Join-Path $Game 'SwampKeeper-Radmin-Guide.html') $Stage -Force
}

# Zip entries use forward slashes (Compress-Archive in Windows PowerShell 5.1 writes backslashes).
function New-Zip([string]$Stage, [string]$ZipPath) {
    if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }
    $stageFull = (Resolve-Path $Stage).Path.TrimEnd('\')
    $zip = [System.IO.Compression.ZipFile]::Open($ZipPath, 'Create')
    try {
        foreach ($f in Get-ChildItem $stageFull -Recurse -File -Force) {
            $rel = $f.FullName.Substring($stageFull.Length + 1).Replace('\', '/')
            [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $f.FullName, $rel, 'Optimal')
        }
    } finally { $zip.Dispose() }
    '{0,-30} {1,12:N0} bytes' -f (Split-Path $ZipPath -Leaf), (Get-Item $ZipPath).Length | Write-Host
}

# ---------------------------------------------------------------- package

if (Test-Path $Dist) { Remove-Item $Dist -Recurse -Force }
New-Item -ItemType Directory -Path $Dist -Force | Out-Null

# 1. the mod alone (with the Swamp Keeper Mods version it requires)
$plain = New-Stage 'plain'
Add-Mod $plain
New-Zip $plain (Join-Path $Dist "$Name.zip")

# 2. BepInEx + the mod
$pack = New-Stage 'pack'
[System.IO.Compression.ZipFile]::ExtractToDirectory($BepZip, $pack)
# Offline first launch: BepInEx would otherwise download these Unity base libraries.
New-Item -ItemType Directory -Path (Join-Path $pack 'BepInEx\unity-libs') -Force | Out-Null
Copy-Item $UnityLib (Join-Path $pack 'BepInEx\unity-libs') -Force
# Same loader settings as the verified install (notably no console window).
New-Item -ItemType Directory -Path (Join-Path $pack 'BepInEx\config') -Force | Out-Null
Copy-Item (Join-Path $Game 'BepInEx\config\BepInEx.cfg') (Join-Path $pack 'BepInEx\config') -Force
Add-Mod $pack
@"
Swamp Keeper Online Co-op
=========================

Contents: BepInEx 6.0.0-be.789 (Windows, IL2CPP, x86), Swamp Keeper Online Co-op, and
Swamp Keeper Mods 1.2.0 (Kill Tracker + Item Prepare), which co-op requires.
For Swamp Keeper v1.05 only. Both players need this same package.

Install
-------
1. Extract everything in this zip into your Swamp Keeper game folder, the one that contains
   "Swamp Keeper.exe". Say yes if asked to merge or replace files.
2. Launch the game normally. The first launch takes a few minutes while BepInEx prepares the game;
   later launches are quick.
3. On the title screen press F7 to open ONLINE CO-OP. SwampKeeperCoop-README.md explains hosting
   and joining; SwampKeeper-Radmin-Guide.html walks through playing over Radmin VPN.

If Windows Firewall asks, allow Swamp Keeper network access (the host needs TCP port 27856 reachable).

Already have BepInEx 6 be.789 for Swamp Keeper? Use SwampKeeperCoop.zip instead; it contains only the mods.

Your game save is never modified by the mods. Lifetime kill history is kept in
BepInEx\config\SwampKeeperMods\progress.json, and an existing Swamp Keeper Mods 1.1.0 history is kept as is.

Uninstall
---------
Delete BepInEx, dotnet, winhttp.dll, doorstop_config.ini, .doorstop_version, changelog.txt,
SwampKeeperCoop-README.md and SwampKeeper-Radmin-Guide.html from the game folder.
To remove only co-op, delete BepInEx\plugins\SwampKeeperCoop.
"@ | Set-Content -Path (Join-Path $pack 'INSTALL.txt') -Encoding utf8
New-Zip $pack (Join-Path $Dist "$Name-Pack.zip")

# 3. source (no bin/obj/dist, no sandbox game copies, no screenshots)
$src = New-Stage 'source'
$exclude = '\\(bin|obj|dist)(\\|$)|\.png$|last-run\.txt$'
foreach ($item in @(
        @{ From = $Root;                              To = 'SwampKeeperCoop' },
        @{ From = (Join-Path $Sources 'SwampKeeperMods'); To = 'SwampKeeperMods' },
        @{ From = (Join-Path $Sources 'tests');           To = 'tests' },
        @{ From = (Join-Path $Sources 'tools');           To = 'tools' })) {
    foreach ($f in Get-ChildItem $item.From -Recurse -File -Force) {
        $rel = $f.FullName.Substring($item.From.Length)
        if ($rel -match $exclude) { continue }
        $to = Join-Path (Join-Path $src $item.To) $rel.TrimStart('\')
        New-Item -ItemType Directory -Path (Split-Path $to) -Force | Out-Null
        Copy-Item $f.FullName $to -Force
    }
}
New-Zip $src (Join-Path $Dist "$Name-Source.zip")

Write-Host ''
Write-Host "Done. Packages are in $Dist"
