# Decompiles a dll one type at a time. A type that crashes the process (stack overflow) or takes more than
# $PerTypeSeconds is recorded in decomp.state.skip and skipped on the restart. Kills only its own child.
param([string]$Dll,[string]$Out,[int]$MaxMinutes=25,[int]$PerTypeSeconds=40)
$exe = Join-Path $PSScriptRoot 'Decomp\bin\Release\net8.0\Decomp.exe'
$state = Join-Path (Split-Path $Out -Parent) 'decomp.state'
$deadline = (Get-Date).AddMinutes($MaxMinutes)
while ((Get-Date) -lt $deadline) {
    $p = Start-Process $exe -ArgumentList @("`"$Dll`"","`"$Out`"","`"$state`"") -PassThru -WindowStyle Hidden -RedirectStandardOutput "$state.out" -RedirectStandardError "$state.err"
    while (-not $p.HasExited -and (Get-Date) -lt $deadline) {
        Start-Sleep -Seconds 2
        if (((Get-Date) - (Get-Item $state).LastWriteTime).TotalSeconds -gt $PerTypeSeconds) { $p.Kill(); $p.WaitForExit(); 'timeout: ' + (Get-Content $state -Raw).Trim(); break }
    }
    if (-not $p.HasExited) { $p.Kill(); 'deadline: killed own child'; break }
    $cur = (Get-Content $state -Raw).Trim()
    if ($cur -eq 'DONE') { Get-Content "$state.out"; break }
    "skipping: $cur"
    Add-Content "$state.skip" $cur
}
'finished'
