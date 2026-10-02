# Dev helper: send lines to the running bot (adombot\cmd.txt), wait, then print status.txt.
# Example: .\botctl.ps1 "keys {ENTER}" -Wait 1.5
param(
  [string[]]$Commands = @(),
  [double]$Wait = 1.2,
  [int]$Lines = 60,
  [string]$GameDir = "C:\Program Files (x86)\Steam\steamapps\common\ADOM"
)
$d = Join-Path $GameDir "adombot"
if ($Commands.Count -gt 0) {
  $tmp = Join-Path $d "cmd.tmp"
  [IO.File]::WriteAllText($tmp, ($Commands -join "`n") + "`ndump`n")
  Move-Item $tmp (Join-Path $d "cmd.txt") -Force
} else {
  [IO.File]::WriteAllText((Join-Path $d "cmd.txt"), "dump`n")
}
Start-Sleep -Milliseconds ([int]($Wait * 1000))
Get-Content (Join-Path $d "status.txt") | Select-Object -First $Lines
