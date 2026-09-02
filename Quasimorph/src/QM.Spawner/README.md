# Quasimorph Item Spawner

Search the game's entire item catalogue and drop anything you like straight into your ship's cargo
hold.

## Controls

| | |
|---|---|
| **F10** | Open / close the spawner window |

Type to filter by item id or name, set a quantity, and hit **Spawn**. The window is draggable, and
the status line at the bottom tells you what happened.

**You need a campaign loaded.** The ship's cargo only exists once a save is running — from the main
menu the window will tell you to load a save first.

## Settings

Edit `BepInEx/config/com.claude.quasimorph.spawner.cfg`, or use
[ConfigurationManager](https://github.com/BepInEx/BepInEx.ConfigurationManager).

| Setting | Default | What it does |
|---|---|---|
| `ToggleKey` | `F10` | Rebind the window key. |
| `HideInternalItems` | `true` | Hides placeholder records and the implicit weapon/ammo parts that aren't real items. Turn off to browse the raw catalogue. |

## Notes

- Items are created and deposited exactly the way the game's own `allitems` debug command does, so
  spawned gear is indistinguishable from gear you found.
- Spawning stacks merges them with what you already have, same as picking items up.
- If the cargo grid is full, the game grows it to fit rather than dropping anything — spawn a few
  hundred items and your cargo screen will get very tall. That is the game's own behaviour, not a
  bug in the mod.
- Only the first 200 matches are drawn at once; narrow your search to see the rest. The list is
  capped because drawing thousands of rows every frame would tank the framerate.
- Save-safe in the sense that it uses normal game code paths, but it obviously changes your save —
  back up first if you care about the run.
