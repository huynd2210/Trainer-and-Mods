param(
    [string]$GameDir,
    [string]$UtmtCli
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
$gameModStateDir = Join-Path $gameDir 'AutoFireMod'
$backupPath = Join-Path $gameModStateDir 'data.win.original.backup'
$stagedPath = Join-Path $gameModStateDir 'data.win.autofire-staged'
$modScript = Join-Path $modDir 'AutoFire.csx'
$expectedGameDir = [System.IO.Path]::GetFullPath($gameDir)
$testedOriginalHash = '8D1C83D68BF6EB673D5A1973DC7AD2A6EAF3E04FBE31DF522C134B2E327126C9'
$testedInstalledHash = 'EE24A34FD8BC7B2DB75D84B865CCD4FC2CE50EF5032997F6FCCC46065BE93F81'

if (-not (Test-Path -LiteralPath $dataPath -PathType Leaf)) {
    throw "TYPECAST data file not found: $dataPath"
}
if (-not (Test-Path -LiteralPath $modScript -PathType Leaf)) {
    throw "Mod script not found: $modScript"
}
if (-not (Test-Path -LiteralPath $gameModStateDir -PathType Container)) {
    New-Item -ItemType Directory -Path $gameModStateDir | Out-Null
}
if (Test-Path -LiteralPath $stagedPath) {
    throw "Staged output already exists; remove it after checking the path: $stagedPath"
}

$currentHash = (Get-FileHash -LiteralPath $dataPath -Algorithm SHA256).Hash
if ($currentHash -eq $testedInstalledHash) {
    Write-Host 'TYPECAST AutoFire is already installed.'
    exit 0
}

if ([string]::IsNullOrWhiteSpace($UtmtCli)) {
    $toolRoot = Join-Path $env:LOCALAPPDATA 'TYPECAST-AutoFire\UTMT-0.9.2.0'
    $UtmtCli = Join-Path $toolRoot 'cli\UndertaleModCli.exe'
    if (-not (Test-Path -LiteralPath $UtmtCli -PathType Leaf)) {
        New-Item -ItemType Directory -Path $toolRoot -Force | Out-Null
        $zipPath = Join-Path $toolRoot 'UTMT_CLI_v0.9.2.0-Windows.zip'
        if (-not (Test-Path -LiteralPath $zipPath -PathType Leaf)) {
            Write-Host 'Downloading UndertaleModTool CLI v0.9.2.0...'
            Invoke-WebRequest `
                -Uri 'https://github.com/UnderminersTeam/UndertaleModTool/releases/download/0.9.2.0/UTMT_CLI_v0.9.2.0-Windows.zip' `
                -OutFile $zipPath
        }
        Expand-Archive -LiteralPath $zipPath -DestinationPath (Join-Path $toolRoot 'cli')
    }
}

$UtmtCli = [System.IO.Path]::GetFullPath($UtmtCli)
if (-not (Test-Path -LiteralPath $UtmtCli -PathType Leaf)) {
    throw "UndertaleModCli.exe not found: $UtmtCli"
}

if ($currentHash -eq $testedOriginalHash) {
    Write-Host 'Recognized the tested TYPECAST data.win build.'
} else {
    Write-Warning "This data.win differs from the tested build. The mod script will abort if its required code markers are absent. Current SHA-256: $currentHash"
}

if (-not (Test-Path -LiteralPath $backupPath -PathType Leaf)) {
    Write-Host "Backing up the original data.win to $backupPath"
    Copy-Item -LiteralPath $dataPath -Destination $backupPath
} else {
    Write-Host "Keeping existing backup: $backupPath"
}

Write-Host 'Compiling TYPECAST AutoFire...'
& $UtmtCli load $dataPath --scripts $modScript --output $stagedPath --verbose
if ($LASTEXITCODE -ne 0) {
    throw "UndertaleModTool exited with code $LASTEXITCODE. The installed data.win was not changed."
}
if (-not (Test-Path -LiteralPath $stagedPath -PathType Leaf)) {
    throw 'UndertaleModTool did not produce the staged data file.'
}

$resolvedStage = [System.IO.Path]::GetFullPath($stagedPath)
$resolvedData = [System.IO.Path]::GetFullPath($dataPath)
if (-not $resolvedStage.StartsWith($expectedGameDir, [System.StringComparison]::OrdinalIgnoreCase) -or
    -not $resolvedData.StartsWith($expectedGameDir, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Resolved install paths escaped the TYPECAST directory.'
}

Move-Item -LiteralPath $stagedPath -Destination $dataPath -Force
$installedHash = (Get-FileHash -LiteralPath $dataPath -Algorithm SHA256).Hash
Write-Host "TYPECAST AutoFire installed. SHA-256: $installedHash"
Write-Host 'AutoFire starts enabled. Press F6 during play to toggle it.'
