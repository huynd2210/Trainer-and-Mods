param(
    [string]$GameDir
)

$ErrorActionPreference = 'Stop'

$modDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if ([string]::IsNullOrWhiteSpace($GameDir)) {
    $adjacentGameDir = Split-Path -Parent $modDir
    if (Test-Path -LiteralPath (Join-Path $adjacentGameDir 'data.win') -PathType Leaf) {
        $GameDir = $adjacentGameDir
    } else {
        $GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\TYPECAST'
    }
}
$gameDir = [System.IO.Path]::GetFullPath($GameDir)
$dataPath = Join-Path $gameDir 'data.win'
$backupPath = Join-Path $gameDir 'AutoFireMod\data.win.original.backup'
$expectedGameDir = [System.IO.Path]::GetFullPath($gameDir)
$resolvedData = [System.IO.Path]::GetFullPath($dataPath)
$resolvedBackup = [System.IO.Path]::GetFullPath($backupPath)
$testedOriginalHash = '8D1C83D68BF6EB673D5A1973DC7AD2A6EAF3E04FBE31DF522C134B2E327126C9'
$testedInstalledHash = 'EE24A34FD8BC7B2DB75D84B865CCD4FC2CE50EF5032997F6FCCC46065BE93F81'

if (-not $resolvedData.StartsWith($expectedGameDir, [System.StringComparison]::OrdinalIgnoreCase) -or
    -not $resolvedBackup.StartsWith($expectedGameDir, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Resolved uninstall paths escaped the TYPECAST directory.'
}
if (-not (Test-Path -LiteralPath $backupPath -PathType Leaf)) {
    throw "Original backup not found: $backupPath"
}

$currentHash = (Get-FileHash -LiteralPath $dataPath -Algorithm SHA256).Hash
$backupHash = (Get-FileHash -LiteralPath $backupPath -Algorithm SHA256).Hash
if ($backupHash -ne $testedOriginalHash) {
    throw "The retained backup is not the tested original TYPECAST archive. Refusing to overwrite data.win. Backup SHA-256: $backupHash"
}
if ($currentHash -ne $testedInstalledHash -and $currentHash -ne $testedOriginalHash) {
    throw "data.win changed after this mod was installed, possibly because TYPECAST was updated. Refusing to replace it with an older backup. Use Steam's Verify integrity of game files instead. Current SHA-256: $currentHash"
}

Copy-Item -LiteralPath $backupPath -Destination $dataPath -Force
$restoredHash = (Get-FileHash -LiteralPath $dataPath -Algorithm SHA256).Hash
Write-Host "Original TYPECAST data.win restored. SHA-256: $restoredHash"
Write-Host 'The backup was retained so the restore remains repeatable.'
