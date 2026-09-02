# package.ps1 - builds the distribution zips for He is Coming - Equipment Draft.
# Convention: <Mod>.zip (mod alone) + <Mod>-Pack.zip (BepInEx + mod).

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$dev     = "C:\Games\He.is.Coming.v0.9.22\moddev"
$game    = "C:\Games\He.is.Coming.v0.9.22\game"
$dist    = "$dev\dist"
$plugin  = "$dev\HicDraft\bin\HicDraft.dll"
$readme  = "$dev\HicDraft\README.md"
$install = "$dev\HicDraft\INSTALL.txt"

if (-not (Test-Path $plugin))  { throw "Plugin not built: $plugin" }
if (-not (Test-Path "$game\BepInEx\core\BepInEx.Unity.IL2CPP.dll")) { throw "BepInEx not found in $game" }

New-Item -ItemType Directory -Force $dist | Out-Null

function New-Zip([string]$zipPath, $entries) {
    $fs  = [System.IO.File]::Create($zipPath)          # truncates an existing zip
    $zip = New-Object System.IO.Compression.ZipArchive($fs, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($e in $entries) {
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $zip, $e.src, $e.dest, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    } finally { $zip.Dispose(); $fs.Dispose() }
}

# 1) Mod only - for a game that already has BepInEx 6 IL2CPP.
$modEntries = @(
    @{ src = $plugin; dest = "BepInEx/plugins/HicDraft.dll" },
    @{ src = $readme; dest = "README.md" }
)
New-Zip "$dist\HicDraft.zip" $modEntries

# 2) BepInEx + mod - self-contained, extract into the game folder and play.
$packEntries = @()
foreach ($f in Get-ChildItem "$game\BepInEx\core\*" -File) {
    $packEntries += @{ src = $f.FullName; dest = "BepInEx/core/$($f.Name)" }
}
foreach ($f in Get-ChildItem "$game\dotnet\*" -File) {
    $packEntries += @{ src = $f.FullName; dest = "dotnet/$($f.Name)" }
}
$packEntries += @{ src = $plugin;                      dest = "BepInEx/plugins/HicDraft.dll" }
$packEntries += @{ src = "$game\winhttp.dll";          dest = "winhttp.dll" }
$packEntries += @{ src = "$game\doorstop_config.ini";  dest = "doorstop_config.ini" }
$packEntries += @{ src = "$game\.doorstop_version";    dest = ".doorstop_version" }
$packEntries += @{ src = $readme;                      dest = "README.md" }
$packEntries += @{ src = $install;                     dest = "INSTALL.txt" }
New-Zip "$dist\HicDraft-Pack.zip" $packEntries

Write-Host "Packaged:"
Get-ChildItem $dist | Select-Object Name, Length
