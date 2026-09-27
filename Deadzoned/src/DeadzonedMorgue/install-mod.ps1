# Install the morgue mod into the Steam copy of Deadzoned. If the dice mod is
# installed there, installs the dice + morgue build so it stays.
$game = 'C:\Program Files (x86)\Steam\steamapps\common\Deadzoned\data.win'
if (Get-Process Deadzoned -ErrorAction SilentlyContinue) { Write-Host 'Close the game first.' -Foreground Red; exit 1 }
$hasDice = [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes($game)).Contains('dicemod.ini')
$mod = if ($hasDice) { "$PSScriptRoot\build\data.win.with-dice" } else { "$PSScriptRoot\build\data.win" }
if (-not (Test-Path $mod)) { Write-Host "$mod missing - run build.ps1 first." -Foreground Red; exit 1 }
Copy-Item $mod $game -Force
Write-Host ("Morgue mod installed" + $(if ($hasDice) { " (with the dice mod)" } else { "" }) + ". Files go to %LOCALAPPDATA%\Deadzoned\morgue") -Foreground Green
