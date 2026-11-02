# Builds the mod and writes dist\AutoIdentify.zip (the mod) and dist\AutoIdentify-Source.zip.
# Both zips unpack to an AutoIdentify\ folder, which goes inside the GnollHack game folder.
$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot

dotnet build (Join-Path $here 'AutoIdentify.csproj') -c Release -nologo -v q
if ($LASTEXITCODE -ne 0) { throw "build failed" }

$dist = Join-Path $here 'dist'
New-Item -ItemType Directory -Force $dist | Out-Null
$stage = Join-Path ([IO.Path]::GetTempPath()) ('autoidentify-' + [Guid]::NewGuid().ToString('N'))

$sets = @{
    'AutoIdentify.zip'        = @('bin\AutoIdentify.dll', 'Play GnollHack (mods).cmd', 'README.md')
    'AutoIdentify-Source.zip' = @('AutoIdentify.cs', 'AutoIdentify.csproj', 'Play GnollHack (mods).cmd', 'README.md', 'package.ps1')
}
foreach ($zip in $sets.Keys) {
    $root = Join-Path $stage ($zip -replace '\.zip$', '')
    $folder = Join-Path $root 'AutoIdentify'
    New-Item -ItemType Directory -Force $folder | Out-Null
    foreach ($f in $sets[$zip]) { Copy-Item -LiteralPath (Join-Path $here $f) -Destination $folder }
    Compress-Archive -Path $folder -DestinationPath (Join-Path $dist $zip) -Force
}
Get-ChildItem $dist -Filter *.zip | ForEach-Object { '{0}  {1} bytes' -f $_.Name, $_.Length }
