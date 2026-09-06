$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:exe "/out:$PSScriptRoot\Verify.exe" "$PSScriptRoot\KillTrackerState.cs" "$PSScriptRoot\KillTrackerStore.cs" "$PSScriptRoot\Verify.cs"
if ($LASTEXITCODE -ne 0) { throw 'Verification runner failed to compile.' }
& "$PSScriptRoot\Verify.exe"
if ($LASTEXITCODE -ne 0) { throw 'Verification failed.' }

$pluginSource = Get-Content -LiteralPath "$PSScriptRoot\KillTrackerPlugin.cs" -Raw
foreach ($required in @('PlayerKnockedOutEnemy', 'PlayerKilledEnemy', 'PlayerKnockedOutNpcEvent', 'PlayerKilledNpcEvent', 'NpcAwakenedEvent', 'WorldStartPart1')) {
    if ($pluginSource -notmatch [regex]::Escape($required)) {
        throw "Regression: required integration '$required' is missing."
    }
}
Write-Host 'PASS: legacy and current NPC event integrations are present.'

