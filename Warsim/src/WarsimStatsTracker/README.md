# Warsim Stats Tracker

Keeps stats for every Warsim: The Realm of Aslona game you play, across new games and deleted
saves, and turns them into a report page with charts.

## Install

Copy `winmm.dll` into the game folder, next to `Warsim.exe`:

    Steam\steamapps\common\Warsim The Realm of Aslona\

Then play as usual. To uninstall, delete `winmm.dll`; your stats stay in `WarsimStats\`.

## Your stats

Open `WarsimStats\stats.html` in a browser. The tracker rewrites it while you play, and it reloads
when you switch back to it.

- **Lifetime:** runs, years ruled, battles, enemies slain, troops lost, gold earned, time played.
- **Records:** most gold held, largest army, most land, best public opinion, longest reign, each
  with the kingdom that set it.
- **Runs:** every playthrough with its status: playing, fallen (with the game's reason), or
  retired / year limit reached.
- **Per run:** gold, men, land and public opinion by year; yearly income against expenses; the
  battle toll; a full year-by-year table.

## How runs are counted

- **New game:** starts a new run.
- **Loading a save:** continues the run it came from (same start year, and same ruler or kingdom
  name). A save made before you installed the tracker starts a run of its own.
- **Loading an older save:** rewinds the run. Years after the save are dropped, because they no
  longer happened.
- **Defeat:** recorded with the reason. Loading a save afterwards resumes the run, and the defeat
  still counts.

Restarting a premade scenario such as WarbandMod continues the same run, because it is the same
kingdom from the same start year.

## Files

All files live in `WarsimStats\` in the game folder, or in `%LOCALAPPDATA%\WarsimStats\` if the
game folder can't be written to.

| File | What |
|---|---|
| `stats.html` | the report |
| `runs\run-NNNN.tsv` | one file per run, with one row of stats per year; plain text you can open in a spreadsheet |
| `tracker.log` | what the tracker did, and any problems |

## Other winmm.dll mods

Only one file can be called `winmm.dll`. To run another mod that is also a `winmm.dll` (such as
the Warsim Invasion Mod), rename it, for example to `InvasionMod.dll`, and put it in a `mods\`
folder in the game folder. The tracker loads every DLL in `mods\` when the game starts.

## How it works

`winmm.dll` forwards every Windows sound call to the real `winmm.dll`, so the game behaves as
before. It only reads the game; nothing in the game is changed. Stats are sampled four times a
second. Breakpoints on the game's main menu, load, game-over and time's-up functions tell new games
from loaded ones. Addresses come from the symbol table inside `Warsim.exe`, so a game update only
breaks the tracker if it renames those functions or variables. If that happens, the tracker turns
itself off and says why in `tracker.log`.

Tested with Warsim 1.3.5 (Steam).

## Building

Needs MSYS2 with the ucrt64 gcc toolchain and Python on `PATH`. Run:

    sh build.sh

This produces `build/winmm.dll`. Stats to track are listed in `src/columns.h`, one line each.
