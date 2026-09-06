# MV Trainer

Resource and repair cheats for FTL: Multiverse, on F4–F10.

| Hotkey | Effect |
|---|---|
| `F4` | fully heal — hull to max, all your crew to full health |
| `F5` | +100 scrap |
| `F6` | +1000 scrap |
| `F7` | refill fuel to 30 |
| `F8` | refill missiles to 30 |
| `F9` | refill drone parts to 30 |
| `F10` | refill fuel, missiles and drone parts |

`F11` and `F12` are deliberately left unbound — F11 is the usual fullscreen toggle and F12 is
Steam's screenshot key.

Refills only ever top up — if you already have more than the target, nothing is taken away.
Each press fires once; holding a key does not repeat.

`F4` repairs the hull to its current maximum (it does not *raise* the maximum) and heals every
crew member belonging to your ship — including an away team aboard the enemy. Enemy boarders
standing on your deck are deliberately left wounded, which is the `iShipId` filter in
`full_heal`.

The only requirement is a run in progress with a player ship. The hotkeys work during the tactical
pause, while a store or event box is open, with the menu open, and mid-jump.

If a press is refused, it says so in `FTL_HS.log`:

```
[MV Trainer] F5 ignored: no run in progress
```

> **Earlier bug, kept here as a warning.** v1.1 copied Multiverse's hotkey guard, which blocks on
> `event_pause` / `menu_pause`. MV needs that because its hotkeys fire game events that would
> conflict with an open dialog — a resource cheat has no such conflict. The effect was that the
> hotkeys did nothing whenever a store or event box was up, which is most of the early game, and
> the guard returned *silently*, so it was indistinguishable from a broken mod. Fixed in v1.2:
> the guard was narrowed to what is genuinely required, and every refusal is now logged.

## Changing the hotkeys or amounts

Amounts are at the top of `data/trainer_scripts/trainer.lua`:

```lua
T.fuelTarget = 30
T.missileTarget = 30
T.droneTarget = 30
```

Key codes are SDL 1.2 keysyms, listed in the `KEYS` table. Values come from the `@field` table in
Hyperspace's `lua/luaDefines.h` (F1 = 0x11A = 282, so F1–F10 are 282–291):

```lua
local KEYS = { F5 = 286, F6 = 287, F7 = 288, F8 = 289, F9 = 290, F10 = 291 }
```

Cheats are a registry — each is one self-contained `register(keyName, label, apply)` call, and the
key dispatcher never changes when you add one:

```lua
register("F5", "+100 scrap", give_scrap(100))
register("F7", "fuel refilled", refill_fuel)
```

## Rebuilding after an edit

```bash
python pack.py . "../../mods/MV Trainer.ftl"
```

Then re-apply. Patching is **not** incremental — restore a vanilla `ftl.dat` first, then apply the
whole stack in order:

```bash
ftlman patch -d "<FTL dir>" Hyperspace.ftl "Multiverse 5.5 - Assets.ftl" "Multiverse 5.5.1 - Data.ftl" "MV Trainer.ftl"
```

## Tests

`test_harness.py` builds a stub Lua file (fake ship + fake `script`/`Hyperspace` globals) and
asserts the cheat logic — 23 checks covering amounts, top-up clamping, key-repeat suppression, the
pause/jump guards, and that F11/F12 stay inert:

```bash
python test_harness.py data/trainer_scripts/trainer.lua trainer_test.lua
ftlman lua-run trainer_test.lua
```
