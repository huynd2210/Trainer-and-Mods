<#
    Builds the mod and produces three zips in .\dist :

      DD2.KillTracker.zip          BepInEx\plugins\DD2.KillTracker.dll + README.md  (for people who already have BepInEx 5)
      DD2.KillTracker-Pack.zip     BepInEx 5.4.23.5 x64 + the mod + INSTALL.txt     (self-contained)
      DD2.KillTracker-Source.zip   source, tests and helper tools, without bin/obj/decomp/vendor/dist

    The mod zips extract directly into the Darkest Dungeon II game folder.
    Nothing here writes into the game folder; the build only reads the game's assemblies.

    Usage:  powershell -File package.ps1 [-GameDir <path to the Darkest Dungeon II folder>] [-SkipTests]
#>
param(
    [string]$GameDir,   # default: the path in src\Directory.Build.props
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$Root    = $PSScriptRoot
$Dist    = Join-Path $Root 'dist'
$Staging = Join-Path $Root 'obj\staging'
$Vendor  = Join-Path $Root 'vendor'
$BepZip  = Join-Path $Vendor 'BepInEx_win_x64_5.4.23.5.zip'
$BepDir  = Join-Path $Vendor 'BepInEx'
$Name    = 'DD2.KillTracker'
$Project = Join-Path $Root "src\$Name\$Name.csproj"
$Dll     = Join-Path $Root "src\$Name\bin\Release\$Name.dll"

# ---------------------------------------------------------------- build (and test)

Write-Host "Building $Name..."
$buildArgs = @($Project, '-c', 'Release', '--nologo', '-v', 'quiet')
# a trailing backslash before the closing quote would escape it on the command line, hence the doubled one
if ($GameDir) { $buildArgs += ('-p:GameDir=' + $GameDir.TrimEnd([char]92) + '\\') }
dotnet build @buildArgs
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
if (-not (Test-Path $Dll)) { throw "Missing build output: $Dll" }

if (-not $SkipTests) {
    Write-Host 'Running unit tests...'
    dotnet run --project (Join-Path $Root 'tests\DD2.KillTracker.Tests\DD2.KillTracker.Tests.csproj') -c Release --nologo -v quiet
    if ($LASTEXITCODE -ne 0) { throw "Tests failed ($LASTEXITCODE failing)." }
}

# ---------------------------------------------------------------- BepInEx

if (-not (Test-Path (Join-Path $BepDir 'BepInEx\core\BepInEx.dll'))) {
    if (-not (Test-Path $BepZip)) { throw "Missing $BepZip (BepInEx 5.4.23.5 x64)." }
    Expand-Archive -Path $BepZip -DestinationPath $BepDir -Force
}

# ---------------------------------------------------------------- helpers

function New-Stage([string]$Label) {
    $dir = Join-Path $Staging $Label
    if (Test-Path $dir) { Remove-Item $dir -Recurse -Force }
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    return $dir
}

function Add-Mod([string]$Stage) {
    $plugins = Join-Path $Stage 'BepInEx\plugins'
    New-Item -ItemType Directory -Path $plugins -Force | Out-Null
    Copy-Item $Dll $plugins -Force
    Copy-Item (Join-Path $Root 'README.md') $Stage -Force
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
    '{0,-30} {1,10:N0} bytes' -f (Split-Path $ZipPath -Leaf), (Get-Item $ZipPath).Length | Write-Host
}

# ---------------------------------------------------------------- package

if (Test-Path $Dist) { Remove-Item $Dist -Recurse -Force }
New-Item -ItemType Directory -Path $Dist -Force | Out-Null

# 1. the mod alone
$plain = New-Stage 'plain'
Add-Mod $plain
New-Zip $plain (Join-Path $Dist "$Name.zip")

# 2. BepInEx + the mod
$pack = New-Stage 'pack'
Copy-Item (Join-Path $BepDir 'winhttp.dll')           $pack -Force
Copy-Item (Join-Path $BepDir 'doorstop_config.ini')   $pack -Force
Copy-Item (Join-Path $BepDir '.doorstop_version')     $pack -Force
New-Item -ItemType Directory -Path (Join-Path $pack 'BepInEx') -Force | Out-Null
Copy-Item (Join-Path $BepDir 'BepInEx\core') (Join-Path $pack 'BepInEx\core') -Recurse -Force
Add-Mod $pack
@"
DD2 Kill Tracker
================

Contents: BepInEx 5.4.23.5 (x64) and the DD2 Kill Tracker mod.

Install
-------
1. Extract everything in this zip into your Darkest Dungeon II game folder, the one that contains
   "Darkest Dungeon II.exe". Say yes if asked to merge folders.
2. Launch the game once. BepInEx creates its config files on first start.
3. In game press F11 to open the kill tracker (change the key in
   BepInEx\config\com.claude.dd2.killtracker.cfg). README.md describes everything it records.

Your kill ledger is saved in BepInEx\config\DD2.KillTracker\ . The game's own save files are never touched.

Uninstall
---------
Delete BepInEx, winhttp.dll, doorstop_config.ini, .doorstop_version and README.md from the game folder.
To remove only this mod, delete BepInEx\plugins\DD2.KillTracker.dll (your ledger stays in BepInEx\config).
"@ | Set-Content -Path (Join-Path $pack 'INSTALL.txt') -Encoding utf8
New-Zip $pack (Join-Path $Dist "$Name-Pack.zip")

# 3. source (no bin/obj/decomp/vendor/dist)
$src = New-Stage 'source'
$exclude = '\\(bin|obj|decomp|decomp_ilspycmd_partial|vendor|dist)(\\|$)'
foreach ($item in 'src', 'tests', 'tools', 'package.ps1', 'README.md') {
    $from = Join-Path $Root $item
    if (-not (Test-Path $from)) { continue }
    if ((Get-Item $from).PSIsContainer) {
        foreach ($f in Get-ChildItem $from -Recurse -File -Force) {
            $rel = $f.FullName.Substring($Root.Length)
            if ($rel -cmatch $exclude) { continue }   # case-sensitive: tools\Decomp (the decompiler helper) must stay, decomp\ must go
            $to = Join-Path $src $rel.TrimStart('\')
            New-Item -ItemType Directory -Path (Split-Path $to) -Force | Out-Null
            Copy-Item $f.FullName $to -Force
        }
    } else {
        Copy-Item $from $src -Force
    }
}
New-Zip $src (Join-Path $Dist "$Name-Source.zip")

Write-Host ''
Write-Host "Done. Packages are in $Dist"
