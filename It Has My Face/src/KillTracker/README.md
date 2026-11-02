# KillTracker

A BepInEx 5 mod for **It Has My Face**. It counts everything you kill, keeps the totals between sessions, and shows
them in the game's own font.

## On screen

- **Each kill:** a line under the crosshair for about two seconds, e.g. `Innocent +1   level 2   run 5   lifetime 41`.
  Several kills of the same kind in a row add up on one line (`+3`).
- **Hold F4:** the full table. Every kind you have ever killed, with counts for this level, the current run and your
  lifetime, most-killed first.

Nothing else is on screen.

## What counts

Only kills the game itself credits to you, by the same rule it uses for its own statistics: you are the killer, or
the game marks the person's death as an environment kill.

| Kind | What it is |
|---|---|
| Clone | Your clone, the level's target |
| Innocent | A civilian with no job |
| Police, Elite police, Cannibal, Other clone | People with that job. A job the mod doesn't know shows its game id |
| Tentacle monster | Any tentacle monster, including the one your clone can turn into |
| Worm | The worms some clones release |

**Runs:** story and endless mode each keep their own run, as the game does. A run ends when the game resets its
difficulty (the result screen after a loss, or a new chapter), and the next kill starts a fresh one. **Level** counts
start from zero in each new level and aren't saved.

Multiplayer kills aren't counted.

## Settings

`BepInEx\config\com.ithasmyface.killtracker.cfg`

| Setting | Default | Meaning |
|---|---|---|
| `Key` | `F4` | The table key. Names come from Unity's Input System (`F6`, `Backquote`, `Numpad1`, ...) |
| `HoldToPeek` | `true` | `false`: the key toggles the table instead |
| `ShowKillLine` | `true` | The line under the crosshair after each kill |
| `MaxRawRecords` | `5000` | Individual kill records kept in the file; the oldest are dropped first. Totals stay exact. `0` = totals only |

## Where the data lives

`BepInEx\config\KillTracker\kills.json`, written within a second of each kill and when you quit. The game's own
saves are never touched.

Every write goes through a temp file, and the previous version is kept as `kills.json.bak`. A file that can't be
parsed is moved aside as `kills.json.corrupt-<time>`, with a warning in the log, and the `.bak` is used instead. It is
never deleted. A file that can't be *read* (it's locked, for example) is left alone, and nothing is saved that session.

Each record holds the UTC time, the kind, the mode (`story`/`endless`), the scene and the level number within the run.

## Install

**No BepInEx yet:** extract `KillTracker-Pack.zip` into the game folder (the one with `It Has My Face.exe`).

**BepInEx 5 already installed:** extract `KillTracker.zip` into the game folder.

## Uninstall

Delete `BepInEx\plugins\KillTracker.dll`. Your counts stay in `BepInEx\config\KillTracker\` until you delete that
folder too.

## Building

Run `package.ps1`. It needs the .NET SDK and expects the game in `..\game` and BepInEx 5.4.23.5 in `vendor\`.
