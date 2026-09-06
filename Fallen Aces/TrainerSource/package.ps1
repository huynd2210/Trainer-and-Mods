param([switch]$Rebuild)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$gameRoot = Split-Path $PSScriptRoot -Parent
$docs = Join-Path $PSScriptRoot 'Distribution'
$release = Join-Path $PSScriptRoot 'release'
New-Item -ItemType Directory -Force $release | Out-Null
if ($Rebuild) { & (Join-Path $PSScriptRoot 'build.ps1') -NoInstall }

function Make-Package([string]$Name, [System.Collections.IDictionary]$Entries) {
    $archivePath = Join-Path $release $Name
    foreach ($source in $Entries.Values) {
        if (!(Test-Path -LiteralPath $source -PathType Leaf)) { throw "Missing package input: $source" }
    }
    # Explicit entries include .doorstop_version and cannot accidentally include
    # game assemblies, logs, decompiler output or personal configurations.
    $stream = [IO.File]::Open($archivePath, [IO.FileMode]::Create)
    $zip = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($entry in $Entries.GetEnumerator()) {
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $entry.Value, $entry.Key, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    } finally { $zip.Dispose(); $stream.Dispose() }

    $zip = [IO.Compression.ZipFile]::OpenRead($archivePath)
    try {
        if ($zip.Entries.Count -ne $Entries.Count) { throw "Entry count mismatch: $Name" }
        foreach ($entry in $zip.Entries) {
            if (!$Entries.Contains($entry.FullName)) { throw "Unexpected entry: $($entry.FullName)" }
            $sha = [Security.Cryptography.SHA256]::Create()
            $entryStream = $entry.Open()
            try { $actual = [BitConverter]::ToString($sha.ComputeHash($entryStream)).Replace('-', '') }
            finally { $entryStream.Dispose(); $sha.Dispose() }
            $expected = (Get-FileHash -LiteralPath $Entries[$entry.FullName] -Algorithm SHA256).Hash
            if ($actual -ne $expected) { throw "Content mismatch: $($entry.FullName)" }
        }
    } finally { $zip.Dispose() }
    Write-Host "Verified $Name ($($Entries.Count) files)"
    return $archivePath
}

# FallenAces.Trainer.zip - the mod alone, for a game folder that already has BepInEx.
$mod = [ordered]@{
    'BepInEx/plugins/FallenAces.Trainer.dll' = Join-Path $PSScriptRoot 'FallenAces.Trainer.dll'
    'README.txt' = Join-Path $docs 'README.txt'
    'INSTALL.txt' = Join-Path $docs 'INSTALL-ModOnly.txt'
}
$paths = @()
$paths += Make-Package 'FallenAces.Trainer.zip' $mod

# FallenAces.Trainer-Pack.zip - BepInEx plus this one mod, self-contained. Installing
# any Pack is what makes the mod-only ZIPs self-serve afterwards.
$pack = [ordered]@{}
foreach ($entry in $mod.GetEnumerator()) { $pack[$entry.Key] = $entry.Value }
$pack['INSTALL.txt'] = Join-Path $docs 'INSTALL-Pack.txt'
foreach ($name in @('winhttp.dll', 'doorstop_config.ini', '.doorstop_version')) { $pack[$name] = Join-Path $gameRoot $name }
$coreNames = @('0Harmony.dll','0Harmony.xml','0Harmony20.dll','BepInEx.dll','BepInEx.Harmony.dll','BepInEx.Harmony.xml',
    'BepInEx.Preloader.dll','BepInEx.Preloader.xml','BepInEx.xml','HarmonyXInterop.dll','Mono.Cecil.dll',
    'Mono.Cecil.Mdb.dll','Mono.Cecil.Pdb.dll','Mono.Cecil.Rocks.dll','MonoMod.RuntimeDetour.dll',
    'MonoMod.RuntimeDetour.xml','MonoMod.Utils.dll','MonoMod.Utils.xml')
foreach ($name in $coreNames) { $pack["BepInEx/core/$name"] = Join-Path $gameRoot "BepInEx/core/$name" }
$pack['BepInEx/config/BepInEx.cfg'] = Join-Path $docs 'BepInEx.cfg'
$pack['THIRD-PARTY-NOTICES.txt'] = Join-Path $docs 'THIRD-PARTY-NOTICES.txt'
foreach ($file in (Get-ChildItem -LiteralPath (Join-Path $docs 'licenses') -File)) { $pack["licenses/$($file.Name)"] = $file.FullName }
$paths += Make-Package 'FallenAces.Trainer-Pack.zip' $pack

# Source ZIP, matching the other mods in this folder.
$source = [ordered]@{}
foreach ($name in @('TrainerModel.cs','TrainerCheats.cs','TrainerPlugin.cs','build.ps1','package.ps1','README.md')) {
    $source["TrainerSource/$name"] = Join-Path $PSScriptRoot $name
}
foreach ($file in (Get-ChildItem -LiteralPath $docs -Recurse -File)) {
    $relative = $file.FullName.Substring($docs.Length + 1).Replace('\', '/')
    $source["TrainerSource/Distribution/$relative"] = $file.FullName
}
$paths += Make-Package 'FallenAces.Trainer-Source.zip' $source

$hashes = foreach ($path in $paths) { "{0}  {1}" -f (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant(), (Split-Path $path -Leaf) }
$hashes | Set-Content -LiteralPath (Join-Path $release 'SHA256SUMS.txt') -Encoding ascii
Write-Host "Wrote $($paths.Count) archives to $release"
