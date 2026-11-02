# Builds, then writes Mod (plugin only), Dist (plugin + BepInEx 5 + Doorstop) and Source zips
# to release/independent/MissileControl, in the same layout as ModSource/package-independent.ps1.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$name = 'MissileControl'
$version = '1.0.0'
$root = $PSScriptRoot
$gameRoot = (Resolve-Path (Join-Path $root '../..')).Path
$packaging = Join-Path $gameRoot 'ModSource/Packaging'
$release = Join-Path $gameRoot "release/independent/$name"
New-Item -ItemType Directory -Path $release, $packaging -Force | Out-Null

function Write-IndependentZip([string]$path, [System.Collections.IDictionary]$entries) {
    $lines = foreach ($entry in $entries.GetEnumerator()) {
        '{0}  {1}' -f (Get-FileHash -LiteralPath $entry.Value).Hash.ToLowerInvariant(), $entry.Key
    }
    $manifest = Join-Path $packaging ([IO.Path]::GetFileNameWithoutExtension($path) + '.MANIFEST.sha256')
    [IO.File]::WriteAllLines($manifest, [string[]]$lines, [Text.UTF8Encoding]::new($false))
    $entries['MANIFEST.sha256'] = $manifest
    if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path }
    $zip = [IO.Compression.ZipFile]::Open($path, 'Create')
    try {
        foreach ($entry in $entries.GetEnumerator()) {
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $entry.Value, $entry.Key, 'Optimal') | Out-Null
        }
    } finally { $zip.Dispose() }
    Write-Output ("{0}: {1:N2} MB, {2} entries" -f $path, ((Get-Item $path).Length / 1MB), $entries.Count)
}

& (Join-Path $root 'build.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }

$instructions = Join-Path $packaging "INSTALL-$name.txt"
$text = @"
Manual Missile Control $version for Nuclear Option 0.34.1
========================================================
Runs on its own; no other custom mod is required. Needs the BepInEx 5 Mono loader.

Mod.zip: this mod only, for an existing BepInEx 5 install.
Dist.zip: this mod plus Windows x64 BepInEx 5.4.23.5 and Doorstop.
Source.zip: this mod's code, tests and build/package scripts. No game assemblies.

Install:
1. Close Nuclear Option. In Steam: Manage > Browse local files.
2. Extract the ZIP beside NuclearOption.exe. The plugin goes into
   BepInEx/plugins/$name; Dist also adds winhttp.dll beside the executable.
3. Start the game. Fire a missile, press F6 and steer it.
   BepInEx/plugins/$name/README.md has the controls and settings.
4. Uninstall: with the game closed, delete BepInEx/plugins/$name.

Build from Source: extract into the game folder so ModSource/$name sits beside
NuclearOption_Data and BepInEx, then run
  powershell -ExecutionPolicy Bypass -File .\ModSource\$name\build.ps1 -Install

Checked: compiles against the 0.34.1 assemblies; steering-math tests pass;
every patched game method and field was confirmed in the 0.34.1 code.
Not yet checked in a running game: loading, camera, steering feel, impacts.
Single player / host only. Future game versions may break it.
"@
[IO.File]::WriteAllText($instructions, $text, [Text.UTF8Encoding]::new($false))

$modFiles = [ordered]@{ 'INSTALL.txt' = $instructions }
$modFiles["BepInEx/plugins/$name/NuclearOption.$name.dll"] = Join-Path $root "bin/Release/NuclearOption.$name.dll"
$modFiles["BepInEx/plugins/$name/README.md"] = Join-Path $root 'README.md'

$dist = [ordered]@{}
foreach ($entry in $modFiles.GetEnumerator()) { $dist[$entry.Key] = $entry.Value }
foreach ($file in @('winhttp.dll', 'doorstop_config.ini')) { $dist[$file] = Join-Path $gameRoot $file }
Get-ChildItem (Join-Path $gameRoot 'BepInEx/core') -File | Sort-Object Name | ForEach-Object { $dist["BepInEx/core/$($_.Name)"] = $_.FullName }
$licenses = Join-Path $packaging 'licenses' # workspace only; absent when built from the Source zip
if (Test-Path -LiteralPath $licenses) { Get-ChildItem $licenses -File | Sort-Object Name | ForEach-Object { $dist["licenses/$($_.Name)"] = $_.FullName } }

$source = [ordered]@{ 'INSTALL.txt' = $instructions }
Get-ChildItem $root -Recurse -File | Where-Object {
    $relative = $_.FullName.Substring($root.Length + 1)
    $relative -notmatch '(^|\\)(bin|obj)\\' -and $_.Extension -in '.cs', '.csproj', '.ps1', '.md'
} | Sort-Object FullName | ForEach-Object { $source["ModSource/$name/" + $_.FullName.Substring($root.Length + 1).Replace('\', '/')] = $_.FullName }

$prefix = "$name-$version-NO0.34.1"
Write-IndependentZip (Join-Path $release "$prefix-Mod.zip") $modFiles
Write-IndependentZip (Join-Path $release "$prefix-Dist.zip") $dist
Write-IndependentZip (Join-Path $release "$prefix-Source.zip") $source
$hashes = Get-ChildItem $release -Filter '*.zip' -File | Sort-Object Name | ForEach-Object { '{0}  {1}' -f (Get-FileHash $_.FullName).Hash.ToLowerInvariant(), $_.Name }
[IO.File]::WriteAllLines((Join-Path $release 'SHA256SUMS.txt'), [string[]]$hashes, [Text.UTF8Encoding]::new($false))
