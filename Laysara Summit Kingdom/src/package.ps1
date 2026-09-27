<#
    Builds the distributable zips for Extra Income.

        dist\ExtraIncome.zip          the mod alone (needs UE4SS already)
        dist\ExtraIncome-Pack.zip     UE4SS + the mod, self-contained
        dist\ExtraIncome-Source.zip   this source tree

    Both game-facing zips extract into the Laysara folder - the one holding
    Laysara.exe - so every path inside them starts at AS\.

    The Pack copies UE4SS out of a real install rather than a checked-in binary;
    -GameDir points at that install, -SkipPack builds without one.
#>
[CmdletBinding()]
param(
    [string] $GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Laysara Summit Kingdom',
    [string] $OutDir  = (Join-Path $PSScriptRoot 'dist'),
    [switch] $SkipPack
)

$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.IO.Compression | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem | Out-Null

$ModName  = 'ExtraIncome'
# The workspace keeps the mod in src\<mod>; the published source tree keeps it
# next to this script.
$SrcMod   = Join-Path $PSScriptRoot "src\$ModName"
if (-not (Test-Path $SrcMod)) { $SrcMod = Join-Path $PSScriptRoot $ModName }
$Docs     = Join-Path $PSScriptRoot 'docs'
$Readme   = Join-Path $SrcMod 'README.txt'
$Staging  = Join-Path $env:TEMP "extraincome-package-$PID"

$ModRel   = "AS\Binaries\Win64\ue4ss\Mods\$ModName"
$Ue4ssRel = 'AS\Binaries\Win64\ue4ss'
$BinRel   = 'AS\Binaries\Win64'

# Runtime state that must never ship: the user's per-map amounts.
$ModExclude = @('amounts.txt')

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

function Copy-Mod([string] $Destination) {
    Copy-Into $SrcMod $Destination
    foreach ($name in $ModExclude) {
        $f = Join-Path $Destination $name
        if (Test-Path $f) { Remove-Item $f -Force }
    }
}

# Entries written by hand so separators are '/', sorted for reproducible archives.
function Write-Zip([string] $Root, [string] $ZipName) {
    $zip = Join-Path $OutDir $ZipName
    if (Test-Path $zip) { Remove-Item $zip -Force }

    $rootFull = (Resolve-Path $Root).ProviderPath.TrimEnd('\')
    $files = Get-ChildItem -Path $Root -Recurse -File | Sort-Object { $_.FullName }

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
    Write-Host ("  {0,-28} {1,12} bytes   {2} files" -f $ZipName, $size, $files.Count)
}

# --- checks ---------------------------------------------------------------

if (-not (Test-Path $SrcMod)) { throw "mod source missing: $SrcMod" }
if (-not (Test-Path $Readme)) { throw "README.txt missing: $Readme" }

if (-not $SkipPack) {
    if (-not (Test-Path -LiteralPath $GameDir -ErrorAction Ignore)) {
        throw ("Game folder not found: '$GameDir'. Pass -GameDir <folder holding " +
               "Laysara.exe>, or -SkipPack to build without the Pack.")
    }
    $missing = @($PackFiles | Where-Object {
        -not (Test-Path -LiteralPath (Join-Path $GameDir $_) -ErrorAction Ignore) })
    if ($missing.Count -gt 0) {
        throw ("UE4SS is not installed under '$GameDir'. Missing: " + ($missing -join ', ') +
               ". Install UE4SS there first, or pass -SkipPack.")
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
    Copy-Mod (Join-Path $mod $ModRel)
    Copy-Into $Readme (Join-Path $mod 'README.txt')
    Copy-Into (Join-Path $Docs 'INSTALL-mod.txt') (Join-Path $mod 'INSTALL.txt')
    Write-Zip $mod "$ModName.zip"

    # --- pack: UE4SS + mod ------------------------------------------------

    if ($SkipPack) {
        Write-Host '  (skipped Pack)'
    } else {
        $pack = New-Staging 'pack'
        foreach ($rel in $PackFiles + $PackDirs) {
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
            Copy-Into (Join-Path $GameDir "$Ue4ssRel\Mods\$name") (Join-Path $pack "$Ue4ssRel\Mods\$name")
        }

        # Ship the loader quiet: no console window on launch. Plain UTF-8, no BOM.
        $settings = Join-Path $pack "$Ue4ssRel\UE4SS-settings.ini"
        $lines = Get-Content $settings
        $quiet = $lines -replace '^(GuiConsoleEnabled|ConsoleEnabled)\s*=.*', '$1 = 0'
        if (Compare-Object $lines $quiet -SyncWindow 0) {
            [System.IO.File]::WriteAllLines($settings, $quiet, (New-Object System.Text.UTF8Encoding($false)))
        }

        Copy-Mod (Join-Path $pack $ModRel)
        Copy-Into $Readme (Join-Path $pack 'README.txt')
        Copy-Into (Join-Path $Docs 'INSTALL-pack.txt') (Join-Path $pack 'INSTALL.txt')
        Write-Zip $pack "$ModName-Pack.zip"
    }

    # --- source -----------------------------------------------------------

    $src = New-Staging 'src'
    foreach ($item in @('docs', 'reference', 'package.ps1')) {
        $from = Join-Path $PSScriptRoot $item
        if (Test-Path $from) { Copy-Into $from (Join-Path $src $item) }
    }
    Copy-Mod (Join-Path $src "src\$ModName")
    Copy-Into $Readme (Join-Path $src 'README.txt')
    Copy-Into (Join-Path $Docs 'BUILD.txt') (Join-Path $src 'BUILD.txt')
    Write-Zip $src "$ModName-Source.zip"

} finally {
    if (Test-Path $Staging) { Remove-Item $Staging -Recurse -Force }
}

Write-Host ''
Write-Host "Done -> $OutDir" -ForegroundColor Green
