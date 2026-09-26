<#
    CSD2 Autoplay - developer build

    Compiles src\*.gml into a patched data.win WITHOUT installing it.

        .\build.ps1                       # needs backup\data.win.vanilla
        .\build.ps1 -Vanilla "D:\data.win.vanilla" -Out ".\build\data.win"

    Always builds from a vanilla data.win. patch.csx refuses an already-patched
    input, so a stale build can never stack two copies of the bot.

    Compile errors from the GML are printed verbatim - if you see
    "Declaring local variable over builtin", you have named a local after a GML
    builtin (fps, room, id ...).
#>
[CmdletBinding()]
param(
    [string] $Vanilla = "$PSScriptRoot\backup\data.win.vanilla",
    [string] $Out     = "$PSScriptRoot\build\data.win",
    [string] $Cli     = "$PSScriptRoot\tools\utmt\UndertaleModCli.exe"
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $Vanilla)) {
    Write-Host "ERROR: no vanilla data.win at $Vanilla" -ForegroundColor Red
    Write-Host "Copy an unmodified data.win there, or pass -Vanilla <path>."
    exit 1
}
if (-not (Test-Path $Cli)) {
    Write-Host "ERROR: UndertaleModCli.exe not at $Cli" -ForegroundColor Red
    Write-Host "Run install.ps1 once to fetch it, or pass -Cli <path>."
    exit 1
}

New-Item -ItemType Directory -Force -Path (Split-Path $Out) | Out-Null
$env:CSD2AP_SRC = Join-Path $PSScriptRoot 'src'

Write-Host "==> Building $Out" -ForegroundColor Cyan
$log = & $Cli load $Vanilla -s "$PSScriptRoot\tools\patch.csx" -o $Out -f 2>&1 | Out-String

if ($log -match 'PATCH OK') {
    Write-Host "    ok: $Out" -ForegroundColor Green
} else {
    Write-Host $log
    Write-Host 'BUILD FAILED' -ForegroundColor Red
    exit 1
}
