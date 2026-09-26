# Nuclear Option mods

BepInEx 5 plugins for Nuclear Option 0.33.4. BepInEx is not included; see
`dist\LOADER-NOT-BUNDLED.md`.

| Mod | What it does | Download |
|---|---|---|
| AIM-120 AMRAAM 1.1.0 | Adds a separate active-radar air-to-air missile with its own model to ARH-capable hardpoints: $1M per round, 140 km range, Mach 4. Shots beyond 20 km fly a lofted arc. If fired without a target, or if it loses its target, it searches for the first enemy its seeker detects. | `dist\AMRAAM.zip` |
| Kill and Cost Tracker 1.4.0 | In-mission overlay (F7) of kills, destroyed value and munition spending per player and faction, persisted across missions. | `dist\NuclearOption.KillCostTracker.zip` |

## Install

1. Install BepInEx 5 (x64, Mono) into the game folder and start the game once.
2. Extract the mod zip into the game folder (the one with `NuclearOption.exe`).

The AMRAAM zip already has the `BepInEx\plugins\AMRAAM\` path inside it. Its
settings are in `BepInEx\config\nuclearoption.amraam.cfg` after the first run.

## Status

**AMRAAM:** registration, loadout listing, model rendering and seeker search are
checked in an automated game run. The flight path is checked in a kinematic test.
It has **not been flown in a live mission yet**, so loft behaviour, hit rate and
multiplayer are unverified. The mod README lists the test flights to do.

## Source

`src\<Mod>\`, plus `dist\AMRAAM-Source.zip`. Each project references the game's
assemblies by relative path and expects to sit at `<game>\ModSource\<Mod>`.
