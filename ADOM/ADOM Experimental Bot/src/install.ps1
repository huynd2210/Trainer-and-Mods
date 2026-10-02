# Installs (or updates) the ADOM bot into the Steam copy of ADOM.
# Copies src\*.noe into <ADOM>\games, creates <ADOM>\adombot, and adds one loader line to
# games\adom.noe (a backup of the original is kept as adom.noe.bot-backup).
# Uninstall: .\install.ps1 -Uninstall
param(
  [string]$GameDir = "C:\Program Files (x86)\Steam\steamapps\common\ADOM",
  [switch]$Uninstall
)
$ErrorActionPreference = "Stop"
$games = Join-Path $GameDir "games"
$main = Join-Path $games "adom.noe"
$backup = "$main.bot-backup"
$hook = 'pcall(dofile, "games/adom-bot.noe") -- ADOM Bot'
$anchor = 'rungame("whatever")'

if (-not (Test-Path $main)) { throw "adom.noe not found in $games" }
$text = [IO.File]::ReadAllText($main)

if ($Uninstall) {
  if ($text.Contains($hook)) {
    [IO.File]::WriteAllText($main, $text.Replace("$hook`n", "").Replace($hook, ""))
  }
  Get-ChildItem $games -Filter "adom-bot*.noe" | Remove-Item
  "ADOM Bot removed (adombot\ folder with logs kept)."
  return
}

Copy-Item (Join-Path $PSScriptRoot "src\*.noe") $games -Force
New-Item -ItemType Directory -Force (Join-Path $GameDir "adombot") | Out-Null

if (-not $text.Contains($hook)) {
  if (-not $text.Contains($anchor)) { throw "Could not find '$anchor' in adom.noe; game version changed?" }
  if (-not (Test-Path $backup)) { Copy-Item $main $backup }
  [IO.File]::WriteAllText($main, $text.Replace($anchor, "$hook`n$anchor"))
  "Hook added to adom.noe."
} else {
  "Hook already present."
}
"ADOM Bot installed. In game: F12 toggles the bot, Shift+F12 reloads its scripts."
