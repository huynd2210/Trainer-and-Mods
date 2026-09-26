# LONESTAR Tracker

A mod for **LONESTAR** (Unity, Mono) that keeps a permanent, detailed record of what
you actually did — not just how many enemies you beat, but who they were.

The game's own statistics screen counts kills by tier. This names them: the pilot, the
ship, the tier, the phase and day you met them, how many rounds it took, what it cost
your hull, and how long the fight ran. Voyages get a row of their own with the outcome.

## What it records

`battles.csv` — one row per battle won or lost:

| | |
|---|---|
| `when`, `when_unix` | wall-clock time of the result |
| `result` | `Win` (a kill) or `Loss` (you were destroyed) |
| `enemy_pilot`, `enemy_ship` | localised names, as the game shows them |
| `tier` | `Minion` / `Elite` / `Boss` |
| `danger` | the game's own danger wording for that tier |
| `enemy_id` | the row id in the game's EnemyShip table |
| `phase`, `phase_id`, `day` | where in the voyage it happened |
| `rounds` | how long the fight ran |
| `hp_left`, `hp_max`, `hp_lost` | your hull after, its maximum, and what the fight cost |
| `duration_s` | seconds from battle start to result |
| `player_ship`, `player_pilot` | what you were flying |
| `star_coins` | your coins at the time |
| `run_id`, `seed`, `mode` | which voyage, its seed, Standard / Custom / Boss rush |
| `run_kill_no` | this kill's number within the voyage |

`runs.csv` — one row per voyage: outcome, ship, pilot, seed, kills split by tier,
defeats, days, phase reached, time, coins earned, best line and total power, retries,
and what destroyed you.

Both files live in `%USERPROFILE%\AppData\LocalLow\Shuxi\LONESTAR\Tracker\`, beside
the saves rather than inside the mod folder, so updating or reinstalling the mod
cannot take the history with it.

**The logs are append-only.** Nothing is ever rewritten or deleted. A voyage that gets
written twice — once as `Incomplete` when you quit to the menu, again when you finish
it — keeps both rows on disk; the later one is the one the panel believes.

## Persistence

Both logs only gain a row once something has *finished*, so a third file holds the
part that would otherwise live in memory:

`checkpoint.csv` — the voyage currently under way: its counters, clock, coins, days
and phase, plus the starting hull and enemy of a battle in progress. It is written
when the voyage starts, when it is resumed, at the start of every battle, after every
result, on a 20-second timer and on quit. However the game stops — clean exit, crash,
alt-F4, power cut — at most the last few seconds are lost.

Unlike the two logs this one *is* rewritten, because it is a checkpoint rather than a
history, and it is written to a `.tmp` and moved into place so a crash part way
through leaves the previous checkpoint intact rather than half of a new one. It can
never hold the only copy of anything that happened: a battle reaches `battles.csv`
*before* the checkpoint is rewritten.

What this buys, in order of how much is kept:

| the game stopped… | what comes back |
|---|---|
| at the results screen | everything; the voyage has its own `runs.csv` row |
| by quitting to the menu mid-voyage | everything; the voyage is written as `Incomplete` |
| by crashing or alt-F4 mid-voyage | everything up to the last checkpoint (≤20 s) |
| with the checkpoint also lost | the voyage is rebuilt from its own battle rows — kills, tiers, days and ship survive; its clock, coins and best power do not |

On the next launch an unfinished voyage appears in the history immediately, as
`Incomplete`, with its real counters. Load that save and the tracker picks the same
row back up — matched on the voyage seed, which survives the game's own save/load —
rather than starting a second voyage. Start a different voyage instead and the
unfinished one is written out as `Incomplete` and kept.

The panel also remembers where you left it — which view, which filter, which order —
in the mod's `config.json`, stored by name rather than by index so that reordering the
filters in a later version cannot silently change what a saved choice meant.

The schema that describes a voyage is written once, in `Records.cs`, and the
checkpoint *projects* it rather than restating it, so a field added to a voyage is
persisted mid-voyage too with no second edit. The harness asserts that.

## In game

`F10` opens the panel: **Overview**, **This voyage**, **Battles**, **Voyages**.

| key | |
|---|---|
| `←` `→` | switch view |
| `↑` `↓`, wheel | scroll |
| `PgUp` `PgDn`, `Home`, `End` | scroll faster |
| `F` | Battles: cycle the filter — all / kills / minions / elites / bosses / defeats |
| `S` | Battles: cycle the order — newest, oldest, toughest tier, longest fight, closest call, enemy name |
| `Esc` | close |

A one-line readout in a screen corner shows the running voyage's kill count while you
play.

The panel has nothing to click, deliberately: a click it did not consume would fall
through to the ship underneath it, and an IMGUI overlay cannot reliably swallow input
from the game's own input system. Everything is a key or the wheel, and the hint strip
along the bottom pays for that.

Hotkey, readout corner, text size and what gets logged are on the gear icon on the
mod's row in the Mods menu.

## Build

```
dotnet build -c Release src\LonestarTracker\LonestarTracker.csproj
.\install.ps1          # copies into Mods\Local\LonestarTracker (close the game first)
.\package.ps1          # writes dist\LONESTAR-Tracker.zip and -Source.zip
```

Targets **netstandard2.1**, not 2.0 — the game's assemblies are netstandard 2.1 and a
2.0 target fails with CS1705.

No BepInEx and no `-Pack` zip: LONESTAR loads C# mods natively and ships Harmony
2.2.2 in its own `Managed` folder, so there is no loader to bundle.

## How it hooks in

| what | where |
|---|---|
| kill | `EventName.battleVictory`, which carries the enemy's `DataEnemyShip` |
| defeat | `EventName.battleFail`, same payload |
| voyage start | `WantedManager.InitData` |
| voyage resumed | `WantedManager.Load` |
| voyage over | `WantedManager.ProcessOver` |
| voyage dropped from memory | `WantedProcess.ClearListener` |
| battle start | `BattleManager.InitBattleGrid` |

The two battle hooks are the game's own event centre rather than Harmony patches —
that is the designed extension point, and both triggers already hand over exactly the
enemy row the panel wants to show. Every handler swallows its own exceptions: these
run inside the game's delegate chain, and a tracker that throws would take the battle
down with it.

Colours come from the game's `RareColor`. Its rarity ramp — grey, blue, orange —
already reads as "ordinary, notable, rare" in LONESTAR, which is the minion / elite /
boss ladder, so the tiers borrow it rather than inventing a palette.

## Verification

`src` has no test project; the data layer is exercised by a harness that compiles the
real `Csv.cs`, `Records.cs`, `Checkpoint.cs`, `Stats.cs` and `TrackerStore.cs` (no
copies) against a stub for the single Unity call they make. It covers CSV
round-tripping of names with commas, quotes and newlines; every field of all three
schemas surviving a write/read cycle; rebuilding an orphaned voyage; duplicate voyage
rows; reading files whose columns were reordered, removed or added by another version;
the checkpoint surviving a restart and a torn write; the checkpoint schema staying in
step with `RunRecord`; and the overview arithmetic including the divide-by-zero cases.
