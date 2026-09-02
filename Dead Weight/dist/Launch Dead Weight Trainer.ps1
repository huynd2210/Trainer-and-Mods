$ErrorActionPreference = 'Stop'

$trainerRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$gameRoot = Split-Path -Parent $trainerRoot
$trainerExecutable = Join-Path $trainerRoot 'Dead_weight.trainer.exe'
$saveRoot = Join-Path $env:APPDATA 'Godot\app_userdata\Dead_weight'

if (-not (Test-Path -LiteralPath $trainerExecutable -PathType Leaf)) {
    throw "Trainer executable not found: $trainerExecutable"
}

$runningGame = Get-Process -Name 'Dead_weight', 'Dead_weight.trainer', 'Dead_weight.trainer_v11' -ErrorAction SilentlyContinue
if ($runningGame) {
    throw 'Dead Weight is already running. Close the current game session, then launch the trainer again.'
}

if (Test-Path -LiteralPath $saveRoot -PathType Container) {
    $timestamp = Get-Date -Format 'yyyy-MM-dd_HH-mm-ss'
    $backupRoot = Join-Path $trainerRoot 'Trainer Save Backups'
    $backupPath = Join-Path $backupRoot $timestamp
    New-Item -ItemType Directory -Force -Path $backupPath | Out-Null

    $saveItems = Get-ChildItem -LiteralPath $saveRoot -Force | Where-Object {
        $_.Name -eq 'settings' -or $_.Name -like 'saves*'
    }
    foreach ($saveItem in $saveItems) {
        Copy-Item -LiteralPath $saveItem.FullName -Destination $backupPath -Recurse -Force
    }

    Write-Host "Save backup created: $backupPath"
}

Start-Process -FilePath $trainerExecutable -WorkingDirectory $gameRoot
