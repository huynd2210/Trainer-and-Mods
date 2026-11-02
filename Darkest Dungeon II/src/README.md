# DD2 Kill Tracker

A BepInEx 5 mod for Darkest Dungeon II. It counts every enemy you kill, keeps the totals between sessions, and shows
which mod (or DLC, or the base game) each monster came from.

## What it tracks

For each kill it records the monster's definition id, its localized name, who killed it (hero class id and hero name,
or just the damage source: skill id, or `dot` + `blight`/`bleed`/`burn`), the region (biome) the party was in, the UTC
time, and the run. Anything the game does not provide is left out, never guessed.

Totals are kept per monster for the **lifetime** of the DD2 profile and for the **current run**.

What counts as a kill:

- Counted: enemies dying to skills, deathblows, damage over time, chain deaths, and kills on the enemy's own turn.
- Not counted: corpses (a dead monster is swapped for a corpse actor, and the corpse "dying" is not a second kill),
  props and scenery (barrels, book stacks, cover), heroes and allies, and clean-up deaths (corpse expiry, team
  clean-up, capture, cheat-menu kills).
- `CountNonEnemyDeaths = true` also counts heroes, allies and props. Corpses are never counted.

## Controls

| Key | Action |
|---|---|
| **F11** (configurable) | Open / close the window |

In the window: **Run / Lifetime** switches the scope, the filter box matches monster name, id and source, and clicking a
column header sorts by it (click again to reverse). Default sort is by count in the selected scope. Drag the title to
move the window. Monsters from mods show the mod name in gold, in square brackets; a mod that replaced a base-game
monster shows `[Mod name] overrides` in red. DLC monsters show `Name (DLC)`.

## Config

`BepInEx\config\com.claude.dd2.killtracker.cfg`

| Entry | Default | Meaning |
|---|---|---|
| `ToggleKey` | `F11` | Unity Input System key name, optionally with `Ctrl+`, `Shift+`, `Alt+` (e.g. `Ctrl+K`, `Backquote`) |
| `SaveIntervalSeconds` | `15` | How often unsaved kills are written. Also written when combat ends, on profile switch, and on quit |
| `MaxRawRecords` | `5000` | Individual kill records kept in the file; oldest are dropped first. Totals stay exact. `0` = totals only |
| `CountNonEnemyDeaths` | `false` | Also count heroes, allies and props |

## Where the data lives

`BepInEx\config\DD2.KillTracker\ledger_<profile>.json`, one file per DD2 profile (`profile_3`, or `mod_profile_3`:
the game keeps modded play in separate profiles, so they get separate ledgers). The game's own saves are never touched.

Writes go to a temp file that replaces the real one, and the previous version is kept as `.bak`. A file that cannot be
parsed is moved aside as `*.corrupt-<time>` with a warning in the log and the `.bak` is used instead; it is never
overwritten. A file that cannot be *read* (locked) is left alone and nothing is saved over it that session.

Example (compact JSON on disk; shown indented, values illustrative):

```json
{
  "version": 1,
  "profileId": "profile_3",
  "runId": "261006_183000",
  "runIdSynthetic": false,
  "totals": { "lifetime": 1280, "run": 41 },
  "droppedRecords": 0,
  "monsters": {
    "beastmen_huntsman": {
      "lifetime": 212, "run": 6, "name": "Huntsman",
      "source": { "kind": "base", "name": "Base game" },
      "first": "2026-10-01T18:22:04Z", "last": "2026-10-06T18:41:19Z"
    },
    "wolfpack_alpha": {
      "lifetime": 3, "run": 3, "name": "Alpha Wolf",
      "source": { "kind": "mod", "name": "Wolf Pack", "modId": "3412345678", "folder": "3412345678" }
    }
  },
  "kills": [
    { "t": "2026-10-06T18:41:19Z", "monster": "beastmen_huntsman", "name": "Huntsman", "killerClass": "highwayman",
      "killerHero": "Dismas", "sourceType": "skill", "sourceId": "highwayman_open_vein", "region": "crossroads",
      "run": "261006_183000", "content": "Base game" }
  ]
}
```

`source.kind` is `base`, `dlc`, `mod`, `override` or `unknown`.

## How mod attribution works

A monster in DD2 is an `ActorDataClass` element in a `*.Group.csv` data file. The game reads the base data folder, then
each owned DLC's folder, then every enabled mod's folder (its root, its `Overrides` folder, and game-type sub-folders),
and merges elements with the same id: a first sighting adds it; a repeat from an `Overrides` folder replaces it; a
repeat from anywhere else only extends it.

The mod hooks the game's folder loader and notes which monster ids each folder defines, then replays the same
merge rules to decide the owner. A folder is mapped to its owner by path: a loaded mod's folder gives the mod's title
(from its `manifest.json`, or the Steam title), Workshop id and folder name; a DLC folder gives the DLC name; anything
else under the data folder is the base game. If the mod starts after the game has already loaded its base data, the base
folders are read once to fill in the picture.

- A mod adding a new id: **Mod**.
- A mod replacing a base-game or DLC id (via `Overrides`): **Override**, shown as overridden by that mod.
- A mod that only extends an existing id: stays **Base game** / **DLC**.
- The source stored with a monster is the one in force when it was last killed.

## Known limitations

- Attribution covers monsters defined through the game's CSV data. A mod that adds a monster only through an
  Addressables catalog (asset bundle) with no matching `ActorDataClass` data would show `Unknown`.
- A mod that only *extends* a base monster (adds tags, skills) is not shown as touching it.
- The run counter follows the game's run save folder (set when a run is first saved) and starts a new run when the game
  reports one. Installing the mod mid-run counts that run from the first kill it sees.
- The window is IMGUI drawn over the game; clicks on it also reach the game UI underneath. Close it before clicking
  around in combat.
- No hook was found for adding "Killed N x" to the game's own enemy hover; the window is the only display.
- If a game update renames a hooked method, that feature switches off and the log says which one (search the log for
  `Patch '`). Kill counting and the data loader hooks are independent.

## Install

Extract `DD2.KillTracker-Pack.zip` (includes BepInEx 5.4.23.5) into the game folder, or `DD2.KillTracker.zip` if you
already have BepInEx 5. Launch the game once.
