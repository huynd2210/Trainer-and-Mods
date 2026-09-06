# MV Kill Tracker

Counts enemy ships killed, broken down by ship type and by cause.

| Hotkey | Effect |
|---|---|
| `F2` | show/hide the kill panel |
| `F3` | dump the full breakdown to `FTL.log` |

The panel sits top-right, drawn with FTL's own `WindowFrame` and font. It lists the top 12 ship
types by kills with a `hull / crew` column, a note if more types are hidden, and a running total.

## The two causes

| Cause | Meaning |
|---|---|
| `hull` | the ship was blown apart |
| `crew` | every crew member aboard died, ship left intact |

These are mutually exclusive per encounter. Crew-wiping a ship and *then* blowing up the hulk
counts once, as a crew kill. Automated (crewless) ships can only ever score a hull kill — they
start with no crew, which is not a kill.

## How detection works

Hyperspace has **no ship-destroyed event**, and a crew-wiped ship is never "destroyed" — it stays
intact as a lootable hulk. So the tracker polls on `ON_TICK` and watches the current enemy for
whichever transition happens first:

- `bDestroyed` or hull ≤ 0 → **hull**
- previously crewed, not automated, `CountCrew(false)` reaches 0, hull > 0 → **crew**

A once-per-encounter guard prevents double counting. Encounter state resets when the enemy ship
disappears or on `JUMP_ARRIVE`, so killing two ships of the same type in a row counts twice.

## How persistence works, and its one limit

Counts live in Hyperspace's `metaVariables` map, which persists across runs and restarts. Keys are
`kt_h_<SHIP_TYPE>`, `kt_c_<SHIP_TYPE>`, plus `kt_total_hull` / `kt_total_crew`.

That map stores `(string, int)` pairs and survives fine — but **Lua cannot iterate it**, and ship
blueprints are not enumerable from Lua either. So `roster.lua` ships the list of 1752 ship type
names extracted from Multiverse's own blueprint data, and the tracker probes the map by name on
load.

**The limit:** a ship type that is not in `roster.lua` is still counted and still saved correctly,
and still shows in the panel for the rest of the session — but it will not be listed again after a
restart, because there is no name left to probe with. The running totals are unaffected. This only
bites for ship types added by other mods on top of Multiverse. Regenerate the roster to include
them:

```bash
python gen_roster.py "<path to Multiverse Data zip or .ftl>" data/killtracker_scripts/roster.lua
```

## Rebuilding after an edit

```bash
python pack.py . "../../mods/MV Kill Tracker.ftl"
```

Then re-apply. Patching is **not** incremental — restore a vanilla `ftl.dat` first, then apply the
whole stack in order:

```bash
ftlman patch -d "<FTL dir>" Hyperspace.ftl "Multiverse 5.5 - Assets.ftl" "Multiverse 5.5.1 - Data.ftl" "MV Trainer.ftl" "MV Kill Tracker.ftl"
```

## Tests

Two stub harnesses, both run through `ftlman lua-run` with no game required.

```bash
# 27 checks: detection, causes, double-count guards, panel rendering, hotkeys
python kt_test_harness.py data/killtracker_scripts/killtracker.lua kt_test.lua
ftlman lua-run kt_test.lua

# 10 checks: restore from a pre-seeded save, using the real 1752-entry roster
python kt_restore_harness.py data/killtracker_scripts/killtracker.lua data/killtracker_scripts/roster.lua kt_restore.lua
ftlman lua-run kt_restore.lua
```

The restore harness deliberately uses real Multiverse blueprint names
(`ELITE_SHIP_MANTIS`, `ELITE_SHIP_REBEL`). Vanilla FTL names like `MANTIS_SCOUT` do not exist in
Multiverse and will silently restore nothing — which is the roster limitation above, working as
designed.
