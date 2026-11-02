# Member-by-member decompile of the types listed in -TypeList. A member that takes more than
# $PerMemberSeconds (or crashes the process) is recorded in decomp_members.state.skip and emitted as IL.
param([string]$Dll,[string]$Out,[string]$TypeList,[int]$MaxMinutes=30,[int]$PerMemberSeconds=10)
$exe = Join-Path $PSScriptRoot 'DecompMembers\bin\Release\net8.0\DecompMembers.exe'
$state = Join-Path (Split-Path $Out -Parent) 'decomp_members.state'
$deadline = (Get-Date).AddMinutes($MaxMinutes)
while ((Get-Date) -lt $deadline) {
    $p = Start-Process $exe -ArgumentList @("`"$Dll`"","`"$Out`"","`"$state`"","`"$TypeList`"") -PassThru -WindowStyle Hidden -RedirectStandardOutput "$state.out" -RedirectStandardError "$state.err"
    $killed = $false
    while (-not $p.HasExited -and (Get-Date) -lt $deadline) {
        Start-Sleep -Seconds 1
        if (((Get-Date) - (Get-Item $state).LastWriteTime).TotalSeconds -gt $PerMemberSeconds) { $p.Kill(); $p.WaitForExit(); $killed = $true; break }
    }
    if (-not $p.HasExited) { $p.Kill(); 'deadline: killed own child'; break }
    $cur = (Get-Content $state -Raw).Trim()
    if ($cur -eq 'DONE') { 'done'; break }
    "skipping member: $cur"
    Add-Content "$state.skip" $cur
}
