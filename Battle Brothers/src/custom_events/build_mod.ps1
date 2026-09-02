# Builds mod_custom_events.zip from this source tree into the game's data folder.
# Packs only the scripts/ tree, writes explicit directory entries and forward-slash
# names so ::include / this.new resolve correctly (see the packaging notes in the
# project's modding memory - a zip without directory entries loads nothing).
#
# Usage:  powershell -ExecutionPolicy Bypass -File build_mod.ps1

$ErrorActionPreference = "Stop"

$SrcRoot = Split-Path -Parent $MyInvocation.MyCommand.Path   # ...\_modsrc\custom_events_build
$Payload = Join-Path $SrcRoot "scripts"
$OutZip  = "C:\Games\Battle Brothers\data\mod_custom_events.zip"

if (-not (Test-Path $Payload)) { throw "Payload folder not found: $Payload" }

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

if (Test-Path $OutZip) { Remove-Item $OutZip -Force }

$fs = [System.IO.File]::Open($OutZip, [System.IO.FileMode]::CreateNew)
$zip = New-Object System.IO.Compression.ZipArchive($fs, [System.IO.Compression.ZipArchiveMode]::Create)

try {
    $dirEntries = New-Object System.Collections.Generic.HashSet[string]

    function Add-DirEntry([string]$rel) {
        if ([string]::IsNullOrEmpty($rel)) { return }
        if ($script:dirEntries.Add($rel)) {
            [void]$zip.CreateEntry($rel + "/")   # explicit directory entry
        }
    }

    $files = Get-ChildItem -Path $Payload -Recurse -File
    foreach ($f in $files) {
        $rel = $f.FullName.Substring($SrcRoot.Length + 1) -replace '\\','/'  # scripts/.../x.nut

        # Ensure every ancestor directory has an explicit entry.
        $parts = $rel.Split('/')
        $acc = ""
        for ($i = 0; $i -lt $parts.Length - 1; $i++) {
            $acc = if ($acc -eq "") { $parts[$i] } else { "$acc/$($parts[$i])" }
            Add-DirEntry $acc
        }

        $entry = $zip.CreateEntry($rel, [System.IO.Compression.CompressionLevel]::Optimal)
        $in = [System.IO.File]::OpenRead($f.FullName)
        $out = $entry.Open()
        try { $in.CopyTo($out) } finally { $out.Dispose(); $in.Dispose() }
    }
}
finally {
    $zip.Dispose()
    $fs.Dispose()
}

Write-Host "Built $OutZip"
# List contents for a quick sanity check.
$verify = [System.IO.Compression.ZipFile]::OpenRead($OutZip)
try {
    $verify.Entries | ForEach-Object { Write-Host ("  {0}" -f $_.FullName) }
} finally { $verify.Dispose() }
