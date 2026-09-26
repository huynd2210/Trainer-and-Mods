# Builds, tests and packages the AMRAAM mod into release\:
#   AMRAAM.zip         the mod alone (extract into the game folder over an existing BepInEx)
#   AMRAAM-Pack.zip    BepInEx from this install + the mod, self-contained
#   AMRAAM-Source.zip  source, tests, exporter and non-decompiled QA evidence
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem

& (Join-Path $PSScriptRoot 'build.ps1')

$gameRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$release = Join-Path $PSScriptRoot 'release'
New-Item -ItemType Directory -Force -Path $release | Out-Null

$pluginFiles = [ordered]@{
    'BepInEx/plugins/AMRAAM/NuclearOption.AMRAAM.dll' = 'bin/Release/NuclearOption.AMRAAM.dll'
    'BepInEx/plugins/AMRAAM/amraam.meshbin'           = 'assets/amraam.meshbin'
    'BepInEx/plugins/AMRAAM/amraam-paint.png'         = 'assets/amraam-paint.png'
    'BepInEx/plugins/AMRAAM/README.md'                = 'README.md'
}

function Write-Zip([string]$name, [System.Collections.IDictionary]$entries) {
    $path = Join-Path $release $name
    if (Test-Path $path) { Remove-Item -LiteralPath $path }
    $zip = [IO.Compression.ZipFile]::Open($path, 'Create')
    try {
        foreach ($entry in $entries.GetEnumerator()) {
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $entry.Value, $entry.Key, 'Optimal') | Out-Null
        }
    } finally { $zip.Dispose() }
    Write-Output ("{0}  {1:N0} KB  ({2} files)" -f $name, ((Get-Item $path).Length / 1KB), $entries.Count)
}

function Resolve-Entries([System.Collections.IDictionary]$map, [string]$root) {
    $resolved = [ordered]@{}
    foreach ($entry in $map.GetEnumerator()) { $resolved[$entry.Key] = Join-Path $root $entry.Value }
    $resolved
}

# 1. Mod alone.
$mod = Resolve-Entries $pluginFiles $PSScriptRoot
Write-Zip 'AMRAAM.zip' $mod

# 2. Pack: the BepInEx loader this install runs (core, doorstop proxy and config) + the mod.
$pack = [ordered]@{}
foreach ($file in 'winhttp.dll', 'doorstop_config.ini') { $pack[$file] = Join-Path $gameRoot $file }
Get-ChildItem (Join-Path $gameRoot 'BepInEx/core') -File | ForEach-Object { $pack["BepInEx/core/$($_.Name)"] = $_.FullName }
foreach ($entry in $mod.GetEnumerator()) { $pack[$entry.Key] = $entry.Value }
$pack['INSTALL.txt'] = Join-Path $PSScriptRoot 'INSTALL.txt'
Write-Zip 'AMRAAM-Pack.zip' $pack

# 3. Source. evidence\ keeps QA output but drops decompiled game/library code and scratch files.
$decompiled = 'Missile.decompiled.txt', 'Unit.txt', 'FactionHQ.txt', 'Dropdown.txt',
              'ClientObjectManager.txt', 'NetworkIdentity.txt', 'qa.pid'
$source = [ordered]@{}
Get-ChildItem $PSScriptRoot -Recurse -File | Where-Object {
    $relative = $_.FullName.Substring($PSScriptRoot.Length + 1)
    $relative -notmatch '^(bin|obj|release|assets)\\' -and
    $relative -notmatch '\\(bin|obj)\\' -and
    $_.Extension -notin '.zip', '.bak' -and
    -not ($relative -like 'evidence\*' -and $_.Name -in $decompiled)
} | ForEach-Object {
    $source['AMRAAM/' + $_.FullName.Substring($PSScriptRoot.Length + 1).Replace('\', '/')] = $_.FullName
}
Write-Zip 'AMRAAM-Source.zip' $source
