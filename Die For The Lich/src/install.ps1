$ErrorActionPreference = 'Stop'

$gameRoot = Split-Path -Parent $PSScriptRoot
$modPackage = Join-Path $PSScriptRoot 'build\Die for the Lich.meta-progression.pck'
$activePackage = Join-Path $gameRoot 'Die for the Lich.pck'
$expectedHash = 'B6054E5E5C4176AE8521C041AE5E6BF1EC2012D9E95558B2954F97337499DC37'

$running = Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.ProcessName -like 'Die for the Lich*' }
if ($running) {
    throw 'Close Die for the Lich before installing the mod.'
}

$packageHash = (Get-FileHash -LiteralPath $modPackage -Algorithm SHA256).Hash
if ($packageHash -ne $expectedHash) {
    throw "Mod package verification failed. Expected $expectedHash but found $packageHash."
}

Copy-Item -LiteralPath $modPackage -Destination $activePackage -Force
$installedHash = (Get-FileHash -LiteralPath $activePackage -Algorithm SHA256).Hash
if ($installedHash -ne $expectedHash) {
    throw 'The installed package does not match the verified mod build.'
}

Write-Host 'Meta-progression mod installed successfully.'
