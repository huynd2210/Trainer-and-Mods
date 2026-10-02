# Installs (or updates) the ADOM Trainer into the Steam copy of ADOM.
# Copies src\adom-trainer.noe into <ADOM>\games, creates <ADOM>\adomtrainer (log folder), and adds
# one loader line to games\adom.noe (a backup of the original is kept as adom.noe.trainer-backup).
# Uninstall: .\install.ps1 -Uninstall
param(
  [string]$GameDir = "C:\Program Files (x86)\Steam\steamapps\common\ADOM",
  [switch]$Uninstall
)
$ErrorActionPreference = "Stop"
$games = Join-Path $GameDir "games"
$main = Join-Path $games "adom.noe"
$backup = "$main.trainer-backup"
$hook = 'pcall(dofile, "games/adom-trainer.noe") -- ADOM Trainer'
$anchor = 'rungame("whatever")'

if (-not (Test-Path $main)) { throw "adom.noe not found in $games" }
$text = [IO.File]::ReadAllText($main)

if ($Uninstall) {
  if ($text.Contains($hook)) {
    [IO.File]::WriteAllText($main, $text.Replace("$hook`n", "").Replace($hook, ""))
  }
  Remove-Item (Join-Path $games "adom-trainer.noe") -ErrorAction SilentlyContinue
  "ADOM Trainer removed (adomtrainer\ folder with the log kept)."
  return
}

Copy-Item (Join-Path $PSScriptRoot "src\adom-trainer.noe") $games -Force
New-Item -ItemType Directory -Force (Join-Path $GameDir "adomtrainer") | Out-Null

# The trainer's hook line goes directly before rungame, after any other mod's line (the ADOM
# Bot's): loaded last, its key handler sees keys first. Re-running this moves it back there.
if (-not $text.Contains($anchor)) { throw "Could not find '$anchor' in adom.noe; game version changed?" }
if (-not $text.Contains("$hook`n$anchor")) {
  if (-not (Test-Path $backup)) { Copy-Item $main $backup }
  $text = $text.Replace("$hook`n", "").Replace($hook, "")
  [IO.File]::WriteAllText($main, $text.Replace($anchor, "$hook`n$anchor"))
  "Hook placed in adom.noe."
} else {
  "Hook already in place."
}
"ADOM Trainer installed. In game: Ctrl+F5 god mode, Ctrl+F6 heal, Ctrl+F7 +1000 XP, Ctrl+F8 remove corruption, Ctrl+F9 always hit, Ctrl+F10 enemies always miss, Ctrl+F11 cure statuses, Ctrl+F12 teleport to the surface."
"God mode and always hit are on from the start (see adomtrainer_defaults in games\adom-trainer.noe)."
