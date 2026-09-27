# Restore the unmodified Deadzoned data.win (removes every mod).
$game = 'C:\Program Files (x86)\Steam\steamapps\common\Deadzoned\data.win'
$van  = "$PSScriptRoot\backup\data.win.vanilla"
if (Get-Process Deadzoned -ErrorAction SilentlyContinue) { Write-Host 'Close the game first.' -Foreground Red; exit 1 }
Copy-Item $van $game -Force
Write-Host 'Vanilla data.win restored.' -Foreground Green
