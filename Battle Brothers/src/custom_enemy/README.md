# Custom Enemy mod (`mod_custom_enemy`)

Adds a new **monster**, its own **faction** ("The Gloom"), and its own
**roaming parties** that wander the world map and can be fought. Built to run
**immediately with placeholder (vanilla bear) art** so you can test stats and
spawning now, then drop in your real sprite later with a one-line change.

Requires (and loads after) **Modern Hooks**, **MSU**, and **Legends** — already
installed here.

## Files

```
scripts/!mods_preload/mod_custom_enemy.nut   <- ALL config + registration + hooks
scripts/entity/tactical/custom_enemy.nut     <- the monster (tactical actor)
scripts/factions/custom_enemy_faction.nut    <- the faction class
```

Everything you tune lives in the `::CustomEnemy { ... }` block at the top of the
preload: names, sprite brush names, stats are a few lines below it
(`Const.Tactical.Actor.CustomEnemy`), and spawn behaviour (`MaxParties`,
`SpawnCheckInterval`, `MinDistFromPlayer`).

## Dropping in your sprite (later)

1. Make a PNG of the monster body (and optionally a separate head).
2. Compile it into a `.brush` with **`data/BB-Edit-standalone-*.exe`** (the game
   resolves sprites by *brush name*, not by raw PNG).
3. Put the files in this mod:
   - `gfx/<your>.png`
   - `brushes/<your>.brush`  (its brush name is what you reference below)
4. In the preload, point the sprite fields at your brush name(s):
   ```
   BodyBrush = "your_body_brush",
   HeadBrush = "your_head_brush",   // or = BodyBrush if it's a single image
   ```
   Optional extras the actor will auto-use if present:
   `"<BodyBrush>_injured"` (injury overlay) and `"<BodyBrush>_dead"` (corpse).
5. Rebuild the zip (see below).

The world-map party blob (`WorldFigure`) and `PartyBanner` are separate brushes;
leave the vanilla placeholders or swap them the same way.

## Rebuild the installed zip

Re-zip the contents of this folder (the `scripts/` tree, plus `gfx/`+`brushes/`
once you add art) to `data/mod_custom_enemy.zip`, **with directory entries** and
forward-slash paths. A helper script is kept alongside the mod source.

## Testing checklist

- Start a **new game** (a brand-new faction is created at world generation, so
  it won't appear in saves made before installing the mod).
- Watch `Documents/Battle Brothers/log.html` for the `CustomEnemy:` lines
  (registration complete, faction created, party spawned).
- Let world time pass a little; a "The Gloom" party should appear near a town
  away from you. Attack it — you should fight the placeholder monster.
- It tracks in your Kill Tracker automatically (it registers a real EntityType).

## Notes / current limits

- Stats and the spawn rate are placeholder values — tune to taste.
- The monster fights unarmed (`hand_to_hand`). To give it a weapon or custom
  skills, see the commented line in `custom_enemy.nut` `onInit()`.
- Roaming parties are spawned by a light custom spawner (not the vanilla faction
  AI/contract system), so there are no contracts/relations UI for this faction
  by design — it's a pure hostile monster faction.
