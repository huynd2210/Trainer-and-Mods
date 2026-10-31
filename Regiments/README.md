# Regiments mods

BepInEx 5 mods for Regiments 1.5.0 (Mono x64). BepInEx is not included; see `dist/LOADER-NOT-BUNDLED.md`.
Install any of them by extracting its zip from `dist/` into the game folder (beside `Regiments.exe`).

## Kill Tracker 1.0.0

Writes a text file for every battle to `KillTracker\` beside `Regiments.exe`. It lists each kill
your units score and each loss they take, written during the battle, then a summary at the end:
the result, then kills and losses by type and by platoon. It credits each soldier killed to the
platoon that fired, which the game's own after-battle report does not. Allied AI commanders are
left out. Settings are in `BepInEx/config/com.regiments.killtracker.cfg`.

**Status:** played in one War Path battle. Its vehicle kills and losses per platoon matched the
game's own records in the save exactly. Soldier kills can't be checked against the game, which
doesn't record them.

## Theater Command 1.0.0

A WARNO Army General-style campaign inside War Paths. Pick an empty War Path slot, set **Mode** to
**Theater Command**, and you get a grid map with terrain, towns, rivers and roads, with your NATO
formations and the Warsaw Pact's drawn as NATO unit counters. Rest the pointer on a counter to look
inside it: every platoon with how many are left, in the game's own platoon symbols (for the Pact,
the platoon types its battlegroup fields). Drag a counter to move it, or drag it onto an enemy to
attack, with your win chance shown on each enemy as you drag. Double-click one of yours to open it in
the game's regiment screen and spend its Operational Authority on reinforcements and upgrades; BACK
TO MAP brings the changes home. Right-click to refit, Ctrl+Z to undo, Enter for the next turn. The
Pact AI then moves, and every battle on the front is marked with your odds. Click one to **fight** it as a normal Regiments battle, with that formation's
own roster on the nearest town's battle map, or to **auto-resolve** it. The front line and territory
follow the fighting. Win by taking the Pact HQ, destroying the Pact, or holding more towns when time
runs out.

**Status:** played in the game from new campaign through moves, attacks, auto-resolve and two fought
battles, each returning to the map with the result applied. Not yet seen in the game: a fought
battle with heavy casualties carrying back to the map, and a fought defence battle. The terrain is
generated per campaign (a real Fulda Gap map is planned).

## Warpath OPs 1.1.0

Holds Operational Authority at 99,999 in War Path runs, so reinforcements, upgrades and task forces
never run out. 99,999 is the most the counter can show; v1.0.0's 999,999 wrapped onto two lines, and
saves and configs from it are brought down to 99,999 automatically. Settings are in
`BepInEx/config/com.regiments.warpathops.cfg`.

**Source:** `src/` (also the `-Source.zip` files in `dist/`). Each builds with its `package.ps1`
from a workspace at `<game>/../mod/<name>`, against the installed game's assemblies.
