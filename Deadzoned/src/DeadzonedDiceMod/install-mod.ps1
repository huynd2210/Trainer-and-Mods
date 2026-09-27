# Install the dice-mod-patched data.win into the Steam copy of Deadzoned.
$game = 'C:\Program Files (x86)\Steam\steamapps\common\Deadzoned\data.win'
$mod  = "$PSScriptRoot\build\data.win"
if (Get-Process Deadzoned -ErrorAction SilentlyContinue) { Write-Host 'Close the game first.' -Foreground Red; exit 1 }
if (-not (Test-Path $mod)) { Write-Host 'build\data.win missing - run build.ps1 first.' -Foreground Red; exit 1 }
Copy-Item $mod $game -Force
Write-Host 'Dice mod installed. Escape -> settings -> dice rolls.' -Foreground Green
