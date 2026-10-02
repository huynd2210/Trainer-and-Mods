# Builds dist\:
#   tome-trainer.teaa          the addon itself (drop into <ToME>\game\addons)
#   ToMETrainer.zip            game\addons\tome-trainer.teaa + README, extract into the ToME folder
#   ToMETrainer-Source.zip     addon source + this script + README
# Zip entries are written with forward slashes: PhysFS (which mounts .teaa files) needs them.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem

$root = $PSScriptRoot
$src  = Join-Path $root 'tome-trainer'
$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Force $dist | Out-Null

function New-Zip([string]$path, [object[]]$entries) {
    if (Test-Path $path) { Remove-Item $path }
    $zip = [System.IO.Compression.ZipFile]::Open($path, 'Create')
    try {
        foreach ($e in $entries) {
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $e.File, $e.Name, 'Optimal') | Out-Null
        }
    } finally { $zip.Dispose() }
}

function Get-TreeEntries([string]$dir, [string]$prefix) {
    Get-ChildItem $dir -Recurse -File | ForEach-Object {
        $rel = $_.FullName.Substring($dir.Length + 1) -replace '\\', '/'
        [pscustomobject]@{ File = $_.FullName; Name = "$prefix$rel" }
    }
}

$teaa = Join-Path $dist 'tome-trainer.teaa'
New-Zip $teaa (Get-TreeEntries $src '')

$readme = Join-Path $root 'README.md'
New-Zip (Join-Path $dist 'ToMETrainer.zip') @(
    [pscustomobject]@{ File = $teaa;   Name = 'game/addons/tome-trainer.teaa' },
    [pscustomobject]@{ File = $readme; Name = 'ToMETrainer-README.md' }
)

New-Zip (Join-Path $dist 'ToMETrainer-Source.zip') (@(Get-TreeEntries $src 'tome-trainer/') + @(
    [pscustomobject]@{ File = $readme; Name = 'README.md' },
    [pscustomobject]@{ File = (Join-Path $root 'package.ps1'); Name = 'package.ps1' }
))

Get-ChildItem $dist | Select-Object Name, Length
