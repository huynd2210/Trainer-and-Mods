# Warsim: The Realm of Aslona

## Warsim Stats Tracker

Keeps stats for every game you play, across new games and deleted saves, and turns them into a
report page with charts: lifetime totals, records, every run with how it ended, and each run's
gold, men, land, public opinion, income, expenses and battle toll by year.

- `dist/WarsimStatsTracker.zip`: the mod (`winmm.dll` + README)
- `dist/WarsimStatsTracker-Source.zip`: the source
- `src/WarsimStatsTracker/`: the source, unpacked

**Install:** copy `winmm.dll` next to `Warsim.exe`, then play. Stats appear in
`WarsimStats\stats.html` in the game folder. No mod loader is needed. Delete `winmm.dll` to
uninstall.

Full details (how runs are counted, files, running it alongside other `winmm.dll` mods, building):
`src/WarsimStatsTracker/README.md`.

Tested with Warsim 1.3.5 (Steam).
