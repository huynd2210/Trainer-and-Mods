<#
    Deadzoned Dice Mod - build the two distribution zips.

        .\package.ps1
        .\package.ps1 -SkipBuild     # skip the compile check

    Produces:
        dist\DeadzonedDiceMod.zip          end user: installer + GML + patch script
        dist\DeadzonedDiceMod-Source.zip   the whole workspace minus local state

    Neither zip contains data.win: it is the game's, not ours to redistribute.
    install.ps1 patches the copy already on the user's disk.
#>
[CmdletBinding()]
param([switch] $SkipBuild)

$ErrorActionPreference = 'Stop'
$root  = $PSScriptRoot
$dist  = Join-Path $root 'dist'
$stage = Join-Path $dist 'DeadzonedDiceMod'

function Step($m) { Write-Host "==> $m" -ForegroundColor Cyan }
function Fail($m) { Write-Host "ERROR: $m" -ForegroundColor Red; exit 1 }

# The zips ship GML, not a binary, so "it packaged" is not "it builds".
if (-not $SkipBuild) {
    Step 'Compile check (src -> build\data.win, from the vanilla backup)'
    $env:DZM_SRC = Join-Path $root 'src'
    $log = & (Join-Path $root 'tools\utmt\UndertaleModCli.exe') load (Join-Path $root 'backup\data.win.vanilla') `
        -s (Join-Path $root 'tools\patch.csx') -o (Join-Path $root 'build\data.win') -f 2>&1 | Out-String
    if ($log -notmatch 'PATCH OK') { Write-Host $log; Fail 'the sources do not build - nothing was packaged' }
    Write-Host '    PATCH OK'
}

Step 'Syncing src and patch.csx into the staging folder'
New-Item -ItemType Directory -Force (Join-Path $stage 'src'), (Join-Path $stage 'tools') | Out-Null
Copy-Item (Join-Path $root 'src\dzm.gml')       (Join-Path $stage 'src\dzm.gml') -Force
Copy-Item (Join-Path $root 'tools\patch.csx')   (Join-Path $stage 'tools\patch.csx') -Force

# Entries are added one at a time: Windows PowerShell 5.1's Compress-Archive
# writes backslash separators, which the ZIP spec forbids and non-Windows
# unzippers turn into flat files named "a\b\c".
Add-Type -AssemblyName System.IO.Compression.FileSystem
function Pack([string] $base, [string] $prefix, [string] $zipName, [scriptblock] $keep) {
    $zip = Join-Path $dist $zipName
    if (Test-Path $zip) { Remove-Item $zip -Force }
    $files = Get-ChildItem $base -Recurse -File | Where-Object {
        $rel = $_.FullName.Substring($base.Length).TrimStart([char]92)
        & $keep $rel
    }
    $a = [IO.Compression.ZipFile]::Open($zip, 'Create')
    try {
        foreach ($f in $files) {
            $rel = $f.FullName.Substring($base.Length).TrimStart([char]92).Replace([char]92, '/')
            $e   = $a.CreateEntry("$prefix/$rel", [IO.Compression.CompressionLevel]::Optimal)
            $dst = $e.Open()
            $in  = [IO.File]::OpenRead($f.FullName)
            try { $in.CopyTo($dst) } finally { $in.Close(); $dst.Close() }
        }
    } finally { $a.Dispose() }
    Write-Host ("    {0}  ({1} KB, {2} files)" -f $zipName, [math]::Round((Get-Item $zip).Length / 1KB, 1), $files.Count)
    return $zip
}

Step 'Packing'
# local state, never payload: game data, the downloaded tool, builds, test runs
$local = { param($rel)
    $parts = $rel.Split([char]92)
    -not ($parts | Where-Object { @('backup', 'build', 'decomp', 'utmt', 'run') -contains $_ }) -and
    $rel -notmatch '(^|\\)data\.win' -and $rel -notmatch '\.zip$' -and $rel -notmatch 'utmt_cli'
}
$z1 = Pack $stage 'DeadzonedDiceMod' 'DeadzonedDiceMod.zip' $local
$z2 = Pack $root  'DeadzonedDiceMod-Source' 'DeadzonedDiceMod-Source.zip' $local

Step 'Verifying the zips'
$need = @{
    $z1 = @('install.ps1', 'uninstall.ps1', 'INSTALL.txt', 'README.md', 'src/dzm.gml', 'tools/patch.csx')
    $z2 = @('src/dzm.gml', 'tools/patch.csx', 'tools/verify.csx', 'tools/dumpall.csx', 'test/dzmtest.gml',
            'test/testbuild.csx', 'build.ps1', 'package.ps1', 'README.md', 'dist/DeadzonedDiceMod/install.ps1')
}
foreach ($z in @($z1, $z2)) {
    $name = Split-Path $z -Leaf
    $a = [IO.Compression.ZipFile]::OpenRead($z)
    try {
        $names = @($a.Entries | ForEach-Object { $_.FullName })
        if ($names | Where-Object { $_.Contains([char]92) }) { Fail "$name has backslash entry names" }
        $bad = $names | Where-Object { $_ -match '(^|/)(backup|build|decomp|utmt|run)/' -or $_ -match 'data\.win' }
        if ($bad) { Fail "$name contains files it should not: $($bad -join ', ')" }
        foreach ($n in $need[$z]) {
            if (-not ($names | Where-Object { $_ -like "*/$n" })) { Fail "$name is missing $n" }
        }
        $e  = $a.Entries | Where-Object { $_.FullName -like '*/src/dzm.gml' } | Select-Object -First 1
        $sr = New-Object IO.StreamReader($e.Open())
        $packed = $sr.ReadToEnd(); $sr.Close()
        if ($packed -ne (Get-Content (Join-Path $root 'src\dzm.gml') -Raw -Encoding UTF8)) { Fail "$name shipped a stale dzm.gml" }
        Write-Host "    ${name}: $($names.Count) entries, dzm.gml matches src\"
    } finally { $a.Dispose() }
}

Write-Host ''
Write-Host 'Packaged:' -ForegroundColor Green
Write-Host "  $z1"
Write-Host "  $z2"
