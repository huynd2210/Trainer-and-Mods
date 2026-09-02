# Quasimorph Kill Tracker

Counts every kill the game registers — the same funnel the vanilla kill counter uses — and lets you
browse the numbers by **unit type**, by **faction**, and by the game's own creature-class buckets,
across the current raid, the current campaign, and every campaign you have ever played.

## Controls

| | |
|---|---|
| **F11** | Open / close the kill tracker window |

Pick one scope and one axis with the toggle rows at the top, then type to filter the rows by id or
display name. Kills are written to disk every few seconds, so nothing is lost if the game crashes.

## Settings

Edit `BepInEx/config/com.claude.quasimorph.killtracker.cfg`, or use
[ConfigurationManager](https://github.com/BepInEx/BepInEx.ConfigurationManager).

| Setting | Default | What it does |
|---|---|---|
| `ToggleKey` | `F11` | Rebind the window key. |
| `SaveIntervalSeconds` | `10` | How often dirty kill counts are flushed to disk. Lower = more writes and less to lose on a crash; higher = fewer writes. |
| `TrackAllyKills` | `true` | Also count creatures that were allied to the player when they died. Turn off to count only hostiles. |

## What the numbers mean

- **Unit type** is the creature's `MobClassId` (e.g. `ensign`) — the main new axis.
- **Faction** is the creature's faction id. Factionless creatures (monsters, fauna) are bucketed
  under `none` and shown as "(no faction)".
- **Creature class** is the game's own bucket (Human / Quasimorph / QuasimorphBaron / Cyborg /
  Fauna / Illusion) — the same bucket the vanilla kill counter uses.

On the **Campaign** scope the footer shows the vanilla counter's total next to this mod's, so you can
see the two agree. The other scopes don't show it: the vanilla counter is kept per campaign save, so
comparing it against a single raid or against your all-time total would be meaningless.

## Notes

- The tracker deliberately counts exactly what the vanilla counter counts — every creature that
  reaches the game's kill funnel, allies included by default — so the grand total matches the game.
  Turn `TrackAllyKills` off to exclude allies.
- Kills are stored per save slot in `BepInEx/config/QM.KillTracker/` (plus one lifetime file). The
  game's own save files are never touched — which also means the counts don't rewind when you reload
  an earlier save. Loading a save from before a fight keeps those kills on the tally; the vanilla
  counter, which lives inside the save, will rewind and the two will then disagree.
- The **Raid** scope clears when a new raid starts (detected from the game's raid metadata) and when
  the raid moves to a new location without going back to the ship in between.
- Only the first 200 matches are drawn at once; refine the search to see the rest. The list is
  capped because drawing thousands of rows every frame would tank the framerate.
- If a stats file is missing or damaged, the tracker logs a warning and starts from empty rather
  than crashing or deleting anything.
