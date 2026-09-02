$ErrorActionPreference = 'Stop'

$gameRoot = Split-Path -Parent $PSScriptRoot
$originalPackage = Join-Path $PSScriptRoot 'backup\Die for the Lich.original.pck'
$activePackage = Join-Path $gameRoot 'Die for the Lich.pck'
$expectedHash = 'ECBB0E635722D2955CC0BF362361D16125A6263677A00A3E547AB1F23F79FE33'

$running = Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.ProcessName -like 'Die for the Lich*' }
if ($running) {
    throw 'Close Die for the Lich before restoring the original package.'
}

$packageHash = (Get-FileHash -LiteralPath $originalPackage -Algorithm SHA256).Hash
if ($packageHash -ne $expectedHash) {
    throw "Original package verification failed. Expected $expectedHash but found $packageHash."
}

Copy-Item -LiteralPath $originalPackage -Destination $activePackage -Force
$installedHash = (Get-FileHash -LiteralPath $activePackage -Algorithm SHA256).Hash
if ($installedHash -ne $expectedHash) {
    throw 'The restored package does not match the verified original build.'
}

Write-Host 'Original game package restored successfully.'
