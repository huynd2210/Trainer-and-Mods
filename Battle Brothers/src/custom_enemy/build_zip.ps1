# Packages the Custom Enemy mod into data/mod_custom_enemy.zip
# - includes explicit directory entries (so it matches a normal mod zip)
# - uses forward-slash entry names (back-slashes break BB mod loading)
# Re-run this after editing anything under scripts/ (or after adding gfx/ + brushes/).

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$src = "C:\Games\Battle Brothers\_modsrc\custom_enemy"
$out = "C:\Games\Battle Brothers\data\mod_custom_enemy.zip"

# Only these top-level folders go into the zip (add 'gfx','brushes' once you have art).
$includeRoots = @('scripts','gfx','brushes','preload','ui','sounds')

if (Test-Path $out) { Remove-Item $out -Force }

$files = Get-ChildItem -Path $src -Recurse -File | Where-Object {
    $rel = $_.FullName.Substring($src.Length + 1)
    $top = ($rel -split '[\\/]')[0]
    $includeRoots -contains $top
}

$zip = [System.IO.Compression.ZipFile]::Open($out, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    $dirsAdded = @{}
    foreach ($f in $files) {
        $rel = $f.FullName.Substring($src.Length + 1) -replace '\\','/'

        # ensure each parent directory has an explicit entry
        $parts = $rel -split '/'
        $acc = ''
        for ($i = 0; $i -lt $parts.Length - 1; $i++) {
            $acc = if ($acc -eq '') { $parts[$i] } else { "$acc/$($parts[$i])" }
            $dirEntry = "$acc/"
            if (-not $dirsAdded.ContainsKey($dirEntry)) {
                [void]$zip.CreateEntry($dirEntry)
                $dirsAdded[$dirEntry] = $true
            }
        }

        $entry = $zip.CreateEntry($rel, [System.IO.Compression.CompressionLevel]::Optimal)
        $in = [System.IO.File]::OpenRead($f.FullName)
        try {
            $es = $entry.Open()
            try { $in.CopyTo($es) } finally { $es.Dispose() }
        } finally { $in.Dispose() }
        Write-Output "  + $rel"
    }
} finally { $zip.Dispose() }

Write-Output ""
Write-Output "Wrote $out"
