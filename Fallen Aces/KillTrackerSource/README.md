# Fallen Aces Kill Tracker 1.1.0

This BepInEx mod is installed in the adjacent Fallen Aces game folder.

## What it tracks

- Player-caused kills and unconscious enemies as separate cumulative totals.
- A per-enemy-type breakdown, with killed and unconscious columns.
- Killing an unconscious enemy transfers that actor from unconscious to killed instead of counting both.
- Duplicate game events are ignored, and an enemy waking up is removed from the unconscious count.
- Both the legacy `Enemy` combat events and the newer `NPC` event pipeline are supported.

Counts persist across level changes, save loads, and game restarts. Every count change is saved to `BepInEx\config\local.codex.fallenaceskilltracker.counts.xml`, with atomic replacement and a previous-save `.bak` backup. Game save files are untouched. Totals are shared across all playthroughs; loading an older save does not roll them back, and replayed encounters add to the totals.

Actor identity and wake/kill transfers are tracked within the current world only. Unconscious counts from previous worlds remain historical totals; waking or killing a reloaded actor cannot remove its earlier-world knockout. Counts from before this update cannot be recovered because the old mod never saved them.

## Controls and configuration

Press **F8** to show or hide the tracker. It starts visible.

After the first launch, settings are in:

`BepInEx\config\local.codex.fallenaceskilltracker.cfg`

The configuration contains the startup visibility, toggle key, and HUD scale.

## Files and rebuilding

- `BepInEx\plugins\FallenAces.KillTracker.dll`: installed mod.
- `KillTrackerPlugin.cs`: game integration and HUD.
- `KillTrackerState.cs`: deterministic counting and transfer rules.
- `KillTrackerStore.cs`: persistent totals and backup recovery.
- `build.ps1`: compiles against this installed game's assemblies and installs the DLL.
- `verify.ps1`: isolated counting tests plus integration guards.

Close the game before rebuilding or replacing the installed DLL.

## Manual test

1. Launch Fallen Aces, load a level, and confirm the tracker appears at the upper-right.
2. Knock out one enemy. `UNCON.` should become 1 in both `ALL` and that enemy's row.
3. Kill that same unconscious enemy. `UNCON.` should return to 0 and `KILLED` should become 1; the sum must not become 2.
4. Directly kill a different enemy type and confirm its own row appears.
5. Press F8 twice and confirm the overlay hides and returns.
6. Note the totals, start or load another level, and confirm they remain.
7. Quit and relaunch the game, load a level, and confirm the same totals return. Kill another enemy and confirm the kill total increases by one.

If a step fails, include `BepInEx\LogOutput.log` and say which enemy type and attack were used.
