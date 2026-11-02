# It Has My Face mods

Two BepInEx 5 mods for **It Has My Face** (Unity 6000.0.58, Mono). Each works on its own; install either or both.

| Mod | What it does |
|---|---|
| **CloneSight** | **F3** outlines your clone through walls, in pink, with a faint tint over the body. The hostile "other clones" are outlined in amber. Single-player only. |
| **KillTracker** | Counts everything you kill, by kind (Clone, Innocent, Police, Tentacle monster, ...), for this level, the current run and your lifetime. Kept between sessions. A line under the crosshair after each kill; hold **F4** for the full table. |

Full details are in each mod's README: `src/CloneSight/README.md` and `src/KillTracker/README.md`.

## Install

1. Install BepInEx 5 x64 (5.4.23.5 tested) into the game folder, the one with `It Has My Face.exe`.
   See `dist/LOADER-NOT-BUNDLED.md`.
2. Extract `dist/CloneSight.zip` and/or `dist/KillTracker.zip` into the same folder.

## Layout

- `dist/`: mod zips and source zips
- `src/CloneSight/`, `src/KillTracker/`: source, each with its own `package.ps1` and README
