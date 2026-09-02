# Builds, installs, packages, and audits every local CULTIC mod.
# Each mod must produce a mod-only zip and a self-contained BepInEx bundle.

param([switch]$SkipBuild)

$ErrorActionPreference = "Stop"
$game = $PSScriptRoot
$mods = @(
    @{ Name = "CULTIC God Mode";         Source = "GodModeSource";         Dll = "GodModePlugin.dll";         Zip = "GodMode.zip";         Pack = "GodMode-Pack.zip" },
    @{ Name = "CULTIC Kill Tracker";     Source = "KillTrackerSource";     Dll = "KillTrackerPlugin.dll";     Zip = "KillTracker.zip";     Pack = "KillTracker-Pack.zip" },
    @{ Name = "CULTIC Lighter Brighter"; Source = "LighterBrighterSource"; Dll = "LighterBrighterPlugin.dll"; Zip = "LighterBrighter.zip"; Pack = "LighterBrighter-Pack.zip" },
    @{ Name = "CULTIC Minimap";          Source = "MinimapSource";         Dll = "MinimapPlugin.dll";         Zip = "Minimap.zip";         Pack = "Minimap-Pack.zip" },
    @{ Name = "CULTIC Replay";           Source = "ReplaySource";          Dll = "CulticReplayPlugin.dll";    Zip = "CulticReplay.zip";    Pack = "CulticReplay-Pack.zip" },
    @{ Name = "CULTIC SuperHot";         Source = "SuperHotSource";        Dll = "SuperHotPlugin.dll";        Zip = "SuperHot.zip";        Pack = "SuperHot-Pack.zip" },
    @{ Name = "CULTIC Hotkey Trainer";   Source = "TrainerSource";         Dll = "CulticTrainerPlugin.dll";   Zip = "CulticTrainer.zip";   Pack = "CulticTrainer-Pack.zip" }
)

Add-Type -AssemblyName System.IO.Compression.FileSystem

function Get-ArchiveEntryHash([string]$ZipPath, [string]$EntryPath) {
    $archive = [IO.Compression.ZipFile]::OpenRead($ZipPath)
    try {
        $entry = $archive.Entries | Where-Object {
            $_.FullName.Replace('\', '/') -eq $EntryPath.Replace('\', '/')
        } | Select-Object -First 1
        if ($null -eq $entry) {
            throw "Missing archive entry '$EntryPath' in $ZipPath"
        }
        $stream = $entry.Open()
        $sha = [Security.Cryptography.SHA256]::Create()
        try {
            $bytes = $sha.ComputeHash($stream)
            return ([BitConverter]::ToString($bytes)).Replace('-', '')
        }
        finally {
            $sha.Dispose()
            $stream.Dispose()
        }
    }
    finally {
        $archive.Dispose()
    }
}

function Get-ArchiveEntries([string]$ZipPath) {
    $archive = [IO.Compression.ZipFile]::OpenRead($ZipPath)
    try {
        return @($archive.Entries | ForEach-Object { $_.FullName.Replace('\', '/') })
    }
    finally {
        $archive.Dispose()
    }
}

foreach ($mod in $mods) {
    $sourceDir = Join-Path $game $mod.Source
    $packageScript = Join-Path $sourceDir "package.ps1"
    if (-not (Test-Path -LiteralPath $packageScript -PathType Leaf)) {
        throw "Missing package script: $packageScript"
    }

    Write-Host ""
    Write-Host "=== Building $($mod.Name) ==="
    Push-Location $sourceDir
    try {
        if ($SkipBuild) {
            & $packageScript -SkipBuild
        }
        else {
            & $packageScript
        }
    }
    finally {
        Pop-Location
    }
}

$manifest = New-Object System.Collections.Generic.List[string]
$manifest.Add("CULTIC mod release manifest")
$manifest.Add("Built: " + [DateTime]::Now.ToString("yyyy-MM-dd HH:mm:ss zzz"))
$manifest.Add("Game build: v2026.01.10 / Unity " + (Get-Item -LiteralPath (Join-Path $game "CULTIC.exe")).VersionInfo.ProductVersion)
$manifest.Add("BepInEx: " + [Reflection.AssemblyName]::GetAssemblyName((Join-Path $game "BepInEx\core\BepInEx.dll")).Version.ToString())
$manifest.Add("")

foreach ($mod in $mods) {
    $sourceDir = Join-Path $game $mod.Source
    $sourceDll = Join-Path $sourceDir $mod.Dll
    $installedDll = Join-Path $game ("BepInEx\plugins\" + $mod.Dll)
    $releaseDir = Join-Path $sourceDir "release"
    $modZip = Join-Path $releaseDir $mod.Zip
    $packZip = Join-Path $releaseDir $mod.Pack
    foreach ($requiredFile in @($sourceDll, $installedDll, $modZip, $packZip)) {
        if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
            throw "Missing required build output: $requiredFile"
        }
    }
    $releaseEntries = @(Get-ChildItem -LiteralPath $releaseDir -Force)
    $expectedReleaseNames = @($mod.Zip, $mod.Pack) | Sort-Object
    $actualReleaseNames = @($releaseEntries | ForEach-Object { $_.Name } | Sort-Object)
    if ($releaseEntries.Count -ne 2 -or (Compare-Object $expectedReleaseNames $actualReleaseNames)) {
        throw "Release folder must contain exactly $($mod.Zip) and $($mod.Pack): $releaseDir"
    }

    $sourceHash = (Get-FileHash -LiteralPath $sourceDll -Algorithm SHA256).Hash
    $installedHash = (Get-FileHash -LiteralPath $installedDll -Algorithm SHA256).Hash
    $entryPath = "BepInEx/plugins/" + $mod.Dll
    $modHash = Get-ArchiveEntryHash $modZip $entryPath
    $packHash = Get-ArchiveEntryHash $packZip $entryPath
    $uniqueHashes = @($sourceHash, $installedHash, $modHash, $packHash) | Select-Object -Unique
    if ($uniqueHashes.Count -ne 1) {
        throw "DLL hash mismatch for $($mod.Name): source=$sourceHash installed=$installedHash mod=$modHash pack=$packHash"
    }
    $readmePath = Join-Path $sourceDir "README.txt"
    $readmeHash = (Get-FileHash -LiteralPath $readmePath -Algorithm SHA256).Hash
    $modReadmeHash = Get-ArchiveEntryHash $modZip "README.txt"
    $packReadmeHash = Get-ArchiveEntryHash $packZip "README.txt"
    if ((@($readmeHash, $modReadmeHash, $packReadmeHash) | Select-Object -Unique).Count -ne 1) {
        throw "README hash mismatch for $($mod.Name)"
    }
    $sourceBepInExHash = (Get-FileHash -LiteralPath (Join-Path $game "BepInEx\core\BepInEx.dll") -Algorithm SHA256).Hash
    $packBepInExHash = Get-ArchiveEntryHash $packZip "BepInEx/core/BepInEx.dll"
    if ($sourceBepInExHash -ne $packBepInExHash) {
        throw "Bundled BepInEx.dll is stale in $packZip"
    }

    $modEntries = @(Get-ArchiveEntries $modZip)
    $packEntries = @(Get-ArchiveEntries $packZip)
    $forbiddenModEntries = @($modEntries | Where-Object {
        $_ -eq "winhttp.dll" -or $_ -eq "doorstop_config.ini" -or $_ -eq ".doorstop_version" -or $_ -like "BepInEx/core/*" -or $_ -eq "BepInEx/config/BepInEx.cfg"
    })
    if ($forbiddenModEntries.Count -gt 0) {
        throw "Mod-only archive $modZip unexpectedly bundles BepInEx: $($forbiddenModEntries -join ', ')"
    }
    foreach ($requiredEntry in @($entryPath, "README.txt")) {
        if ($modEntries -notcontains $requiredEntry) {
            throw "Mod-only archive $modZip is missing $requiredEntry"
        }
    }
    foreach ($requiredEntry in @($entryPath, "README.txt", "INSTALL.txt", "BepInEx/core/BepInEx.dll", "BepInEx/config/BepInEx.cfg", "winhttp.dll", "doorstop_config.ini", ".doorstop_version")) {
        if ($packEntries -notcontains $requiredEntry) {
            throw "Bundled archive $packZip is missing $requiredEntry"
        }
    }
    $packPluginDlls = @($packEntries | Where-Object { $_ -like "BepInEx/plugins/*.dll" })
    if ($packPluginDlls.Count -ne 1 -or $packPluginDlls[0] -ne $entryPath) {
        throw "Bundled archive $packZip contains unexpected plugin DLLs: $($packPluginDlls -join ', ')"
    }

    $sourcePath = Join-Path $sourceDir ([IO.Path]::GetFileNameWithoutExtension($mod.Dll) + ".cs")
    $sourceText = Get-Content -LiteralPath $sourcePath -Raw
    $versionMatch = [regex]::Match($sourceText, 'BepInPlugin\([^\r\n]*,\s*"([0-9]+\.[0-9]+\.[0-9]+)"\)')
    $version = if ($versionMatch.Success) { $versionMatch.Groups[1].Value } else { "unknown" }
    $readmeFirstLine = Get-Content -LiteralPath $readmePath -TotalCount 1
    if ($version -eq "unknown" -or $readmeFirstLine -notmatch ("v" + [regex]::Escape($version))) {
        throw "README version does not match plugin metadata for $($mod.Name): plugin=$version heading='$readmeFirstLine'"
    }
    $manifest.Add(("{0} v{1}" -f $mod.Name, $version))
    $manifest.Add("  DLL SHA-256: " + $sourceHash)
    $manifest.Add(("  Mod only: {0} ({1:N0} bytes)" -f $modZip, (Get-Item -LiteralPath $modZip).Length))
    $manifest.Add(("  BepInEx bundled: {0} ({1:N0} bytes)" -f $packZip, (Get-Item -LiteralPath $packZip).Length))
}

$manifestPath = Join-Path $game "MOD-RELEASE-MANIFEST.txt"
[IO.File]::WriteAllLines($manifestPath, $manifest, (New-Object Text.UTF8Encoding($false)))
Write-Host ""
Write-Host "All 7 mods built and all 14 archives passed package verification."
Write-Host "Manifest: $manifestPath"
