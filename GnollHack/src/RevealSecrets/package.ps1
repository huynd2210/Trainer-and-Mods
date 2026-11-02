# Builds the mod and writes dist\RevealSecrets.zip (the mod) and dist\RevealSecrets-Source.zip.
# Both zips unpack to a RevealSecrets\ folder, which goes inside the GnollHack game folder.
$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot

dotnet build (Join-Path $here 'RevealSecrets.csproj') -c Release -nologo -v q
if ($LASTEXITCODE -ne 0) { throw "build failed" }

$dist = Join-Path $here 'dist'
New-Item -ItemType Directory -Force $dist | Out-Null
$stage = Join-Path ([IO.Path]::GetTempPath()) ('revealsecrets-' + [Guid]::NewGuid().ToString('N'))

$sets = @{
    'RevealSecrets.zip'        = @('bin\RevealSecrets.dll', 'Play GnollHack (mods).cmd', 'README.md')
    'RevealSecrets-Source.zip' = @('RevealSecrets.cs', 'RevealSecrets.csproj', 'Play GnollHack (mods).cmd', 'README.md', 'package.ps1')
}
foreach ($zip in $sets.Keys) {
    $root = Join-Path $stage ($zip -replace '\.zip$', '')
    $folder = Join-Path $root 'RevealSecrets'
    New-Item -ItemType Directory -Force $folder | Out-Null
    foreach ($f in $sets[$zip]) { Copy-Item -LiteralPath (Join-Path $here $f) -Destination $folder }
    Compress-Archive -Path $folder -DestinationPath (Join-Path $dist $zip) -Force
}
Get-ChildItem $dist -Filter *.zip | ForEach-Object { '{0}  {1} bytes' -f $_.Name, $_.Length }
