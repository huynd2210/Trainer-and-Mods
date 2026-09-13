<#
    Builds the distributable zips for Night Shippers Trainer.

        dist\NightShippersTrainer.zip        the mod alone (needs UE4SS already)
        dist\NightShippersTrainer-Pack.zip   UE4SS + the mod, self-contained
        dist\NightShippersTrainer-src.zip    this source tree

    Both game-facing zips extract into the Night Shippers folder - the one
    holding ProjectSH.exe - so every path inside them starts at ProjectSH\.

    The Pack copies UE4SS out of a real install rather than a checked-in binary;
    -GameDir points at that install, -SkipPack builds without one.
#>
[CmdletBinding()]
param(
    [string] $GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Night Shippers',
    [string] $OutDir  = (Join-Path $PSScriptRoot 'dist'),
    [switch] $SkipPack
)

$ErrorActionPreference = 'Stop'

# ZipArchive/ZipArchiveMode live in System.IO.Compression,
# ZipFile/ZipFileExtensions in System.IO.Compression.FileSystem.
Add-Type -AssemblyName System.IO.Compression | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem | Out-Null

$ModName  = 'NightShippersTrainer'
$SrcMod   = Join-Path $PSScriptRoot "src\$ModName"
$Docs     = Join-Path $PSScriptRoot 'docs'
$Readme   = Join-Path $SrcMod 'README.txt'
$Staging  = Join-Path $env:TEMP "nst-package-$PID"

# Where the mod lives relative to the game folder, and where UE4SS lives.
$ModRel   = "ProjectSH\Binaries\Win64\ue4ss\Mods\$ModName"
$Ue4ssRel = 'ProjectSH\Binaries\Win64\ue4ss'
$BinRel   = 'ProjectSH\Binaries\Win64'

# UE4SS pieces the Pack carries. Everything else in the install (logs, dumps,
# other people's mods) stays out.
$PackFiles = @(
    "$BinRel\dwmapi.dll"
    "$Ue4ssRel\UE4SS.dll"
    "$Ue4ssRel\UE4SS-settings.ini"
    "$Ue4ssRel\LICENSE"
)
$PackDirs = @(
    "$Ue4ssRel\UE4SS_SDK_Backends"
    "$Ue4ssRel\Mods\shared"
)
# The loader's own bundled mods, plus the load-order files that list them.
$PackStockMods = @(
    'BPML_GenericFunctions', 'BPModLoaderMod', 'CheatManagerEnablerMod',
    'ConsoleCommandsMod', 'ConsoleEnablerMod', 'Keybinds', 'LineTraceMod',
    'SplitScreenMod'
)
$PackModsFiles = @('mods.txt', 'mods.json')

function New-Staging([string] $Name) {
    $path = Join-Path $Staging $Name
    New-Item -ItemType Directory -Path $path -Force | Out-Null
    return $path
}

function Copy-Into([string] $Source, [string] $Destination) {
    $parent = Split-Path -Parent $Destination
    if ($parent -and -not (Test-Path $parent)) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }
    Copy-Item -Path $Source -Destination $Destination -Recurse -Force
}

# Compress-Archive writes entry names with backslashes, which the zip format does
# not allow; Explorer and 7-Zip cope, other extractors produce files with literal
# backslashes in the name. Write the entries by hand so the separators are '/'.
# Entries are sorted, so the same input tree always gives the same archive.
function Write-Zip([string] $Root, [string] $ZipName) {
    $zip = Join-Path $OutDir $ZipName
    if (Test-Path $zip) { Remove-Item $zip -Force }

    $rootFull = (Resolve-Path $Root).ProviderPath.TrimEnd('\')
    $files = Get-ChildItem -Path $Root -Recurse -File |
             Sort-Object { $_.FullName }

    $archive = [System.IO.Compression.ZipFile]::Open(
        $zip, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($file in $files) {
            $name = $file.FullName.Substring($rootFull.Length + 1).Replace('\', '/')
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $archive, $file.FullName, $name,
                [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    } finally {
        $archive.Dispose()
    }

    $size = '{0:N0}' -f (Get-Item $zip).Length
    Write-Host ("  {0,-32} {1,12} bytes   {2} files" -f $ZipName, $size, $files.Count)
    return $zip
}

# --- checks ---------------------------------------------------------------

if (-not (Test-Path $SrcMod))  { throw "mod source missing: $SrcMod" }
if (-not (Test-Path $Readme))  { throw "README.txt missing: $Readme" }

if (-not $SkipPack) {
    # Test-Path throws on a path whose drive does not exist, which would bury the
    # useful message below under a "cannot find drive" error.
    if (-not (Test-Path -LiteralPath $GameDir -ErrorAction Ignore)) {
        throw ("Game folder not found: '$GameDir'. Pass -GameDir <path to the " +
               "folder holding ProjectSH.exe>, or -SkipPack to build without the Pack.")
    }
    $missing = @()
    foreach ($rel in $PackFiles) {
        if (-not (Test-Path -LiteralPath (Join-Path $GameDir $rel) -ErrorAction Ignore)) {
            $missing += $rel
        }
    }
    if ($missing.Count -gt 0) {
        throw ("UE4SS is not installed under '$GameDir'. Missing: " +
               ($missing -join ', ') +
               ". Install UE4SS there first, or pass -SkipPack to build without the Pack.")
    }
}

if (Test-Path $Staging) { Remove-Item $Staging -Recurse -Force }
New-Item -ItemType Directory -Path $Staging -Force | Out-Null
New-Item -ItemType Directory -Path $OutDir  -Force | Out-Null

Write-Host "Packaging $ModName" -ForegroundColor Cyan
Write-Host ''

try {
    # --- mod only ---------------------------------------------------------

    $mod = New-Staging 'mod'
    Copy-Into $SrcMod (Join-Path $mod $ModRel)
    Copy-Into $Readme (Join-Path $mod 'README.txt')
    Copy-Into (Join-Path $Docs 'INSTALL-mod.txt') (Join-Path $mod 'INSTALL.txt')
    Write-Zip $mod "$ModName.zip" | Out-Null

    # --- pack: UE4SS + mod ------------------------------------------------

    if ($SkipPack) {
        Write-Host '  (skipped Pack)'
    } else {
        $pack = New-Staging 'pack'

        foreach ($rel in $PackFiles) {
            Copy-Into (Join-Path $GameDir $rel) (Join-Path $pack $rel)
        }
        foreach ($rel in $PackDirs) {
            Copy-Into (Join-Path $GameDir $rel) (Join-Path $pack $rel)
        }
        foreach ($name in $PackStockMods) {
            $rel = "$Ue4ssRel\Mods\$name"
            if (Test-Path (Join-Path $GameDir $rel)) {
                Copy-Into (Join-Path $GameDir $rel) (Join-Path $pack $rel)
            } else {
                Write-Warning "stock mod not in install, omitted from Pack: $name"
            }
        }
        foreach ($name in $PackModsFiles) {
            Copy-Into (Join-Path $GameDir "$Ue4ssRel\Mods\$name") `
                      (Join-Path $pack "$Ue4ssRel\Mods\$name")
        }

        # Ship the loader quiet: no console window on launch. Written back as
        # plain UTF-8 with no BOM - Set-Content -Encoding UTF8 adds one under
        # PowerShell 5.1, and a BOM at the top of the ini is not worth risking.
        $settings = Join-Path $pack "$Ue4ssRel\UE4SS-settings.ini"
        $lines = Get-Content $settings
        $quiet = $lines -replace '^(GuiConsoleEnabled|ConsoleEnabled)\s*=.*', '$1 = 0'
        if (Compare-Object $lines $quiet -SyncWindow 0) {
            $noBom = New-Object System.Text.UTF8Encoding($false)
            [System.IO.File]::WriteAllLines($settings, $quiet, $noBom)
        }

        Copy-Into $SrcMod (Join-Path $pack $ModRel)
        Copy-Into $Readme (Join-Path $pack 'README.txt')
        Copy-Into (Join-Path $Docs 'INSTALL-pack.txt') (Join-Path $pack 'INSTALL.txt')
        Write-Zip $pack "$ModName-Pack.zip" | Out-Null
    }

    # --- source -----------------------------------------------------------

    $src = New-Staging 'src'
    foreach ($item in @('src', 'docs', 'reference', 'package.ps1')) {
        $from = Join-Path $PSScriptRoot $item
        if (Test-Path $from) { Copy-Into $from (Join-Path $src $item) }
    }
    Copy-Into $Readme (Join-Path $src 'README.txt')
    Copy-Into (Join-Path $Docs 'BUILD.txt') (Join-Path $src 'BUILD.txt')
    Write-Zip $src "$ModName-src.zip" | Out-Null

} finally {
    if (Test-Path $Staging) { Remove-Item $Staging -Recurse -Force }
}

Write-Host ''
Write-Host "Done -> $OutDir" -ForegroundColor Green
