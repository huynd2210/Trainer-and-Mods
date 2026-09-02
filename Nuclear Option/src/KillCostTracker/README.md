# Nuclear Option Kill and Cost Tracker

Displays an in-mission overlay with:

- direct local-player kills, destroyed value, and weapon expenditure;
- missile, bomb, and other-round counts for the local player;
- persistent per-weapon identity, type, fired count, and cumulative cost for the
  local player and each observable faction (kept out of the compact overlay);
- one category for every active faction, including faction kills, destroyed value,
  exact built-in faction munition expenditure, and combined cost impact;
- per-unit kill details, ordered by destroyed value.
- cumulative totals that persist across missions and game restarts.

Press **F7** to show or hide the fixed overlay. It is visible by default in the
main menu and when a mission begins.

Configuration is generated at `BepInEx/config/nuclearoption.killcosttracker.cfg`
after the first launch.

Persistent totals are written in two equivalent forms:

- `BepInEx/config/nuclearoption.killcosttracker.stats.json` is the canonical,
  versioned machine-readable save;
- `BepInEx/config/nuclearoption.killcosttracker.stats.txt` is the readable report.

Both files are refreshed every few seconds after changes and keep rolling
`.bak` backups. Save format v2 loads and migrates older v1 JSON without losing
aggregate totals. Weapon names cannot be reconstructed for shots recorded by
older builds; per-weapon history begins with v1.4.0.

## Build

From this directory:

```powershell
.\build-classic.ps1
```

This deliberately uses the .NET Framework compiler, matching the working
BepInEx 5 Mono plugins shipped for CULTIC. Copy
`NuclearOption.KillCostTracker.dll` to
`BepInEx/plugins/KillCostTracker/`.
