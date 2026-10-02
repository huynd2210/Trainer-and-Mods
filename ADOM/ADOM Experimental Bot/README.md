# ADOM Bot

A grinding bot for ADOM (Steam, 3.3.x). It runs inside the game's own Lua layer (NotEye), so it
reads the real game state (map, monsters, HP, messages) rather than the screen. It:

- explores, kills monsters it rates as safe, and leaves the level when something dangerous shows up
- rests when hurt, eats (rations first, then fresh corpses of its own kills), and prays in emergencies
- picks up food, gold and light loot, and chooses skills on level-up
- goes deeper as the character levels (one dungeon level per 3 character levels, up to `max_depth`)
- **sell trip:** when Burdened, or out of food, it walks up to the surface and on to the nearest
  town in view. There it buys food, sells its loot, and walks back to the dungeon it came from.

Whenever it meets something it doesn't understand, it **pauses**: ADOM is turn-based, so a
paused bot can't die. The reason and a copy of the screen go to `adombot\ALERT.txt`, and a
thunder sound plays.

## Install / uninstall

```powershell
.\install.ps1              # copy scripts into <ADOM>\games and add one hook line to adom.noe
.\install.ps1 -Uninstall   # remove the hook line and scripts
```

A Steam update or "verify files" restores `adom.noe` and removes the hook; run `install.ps1`
again after one.

## Use

- **F12** in game: start or stop the bot. **Shift+F12**: reload the scripts and settings.
- Start it inside a dungeon, or standing on a dungeon entrance in the wilderness. Tested in the
  Infinite Dungeon, next to Terinyo.
- `adombot\bot.log` has what it did and why; `adombot\status.txt` has its live state and the screen.
- Settings are in `games\adom-bot-config.noe` (source copy in `src\`).

## Danger model

Each monster is matched against three lists (Lua patterns, anchored to word starts; a `$`
means "ends with", so `giant$` is a hill giant but not a giant rat):

- **flee:** hydra, golem, demon, dragon, were…, ogre, ghost, gargoyle, …. The bot leaves the level.
- **ignore:** floating eye, molds, shopkeepers, townsfolk. Never attacked.
- **fight:** goblin, kobold, orc, rat, bandit, wolf, ….

Anything unlisted is fought only if the game rates it inexperienced. Bosses and named monsters
are fled from. A flee-list monster that is asleep or lying in wait is avoided (the bot keeps 5
squares away) rather than fled from.

Survival rules added after test characters died:

- It never turns its back on an adjacent enemy. When hurt it prays (if a prayer is due) or
  fights back, and only walks away when nothing is adjacent, or from stairs.
- Attacks from something **unseen** (the game reports HP lost to an attack with nothing in
  sight, or "Something hits you") mean: stop resting, leave the level, pray if low.
- Six punches in a row that "do not manage to harm" a monster: it is treated as a flee monster.
- No weapon wielded on start (monks excepted): pause. Any equipment slot emptying: pause.
- A god-displeasure message only counts right after a prayer ("angry sounds" once blocked
  prayers, and the character died without one).
- It never looks for fights while Starving.

## Selling and food

Shops only buy what they deal in. **Terinyo's shop buys only food**, so near Terinyo gear loot
can't be sold. What happens to unsold loot is the `unsold` setting:

- `"pause"` (the default): the bot stops when Burdened and tells you.
- `"drop"`: the bot throws away the loot it picked up itself (never your own gear) and keeps going.

Food trips work at Terinyo. The bot checks its gold, picks up only food it can afford, pays
before doing anything else, and puts goods back if it can't pay; it never leaves owing money.
`buy_food` sets how many rations it buys per trip. `sell_towns` lists the town tile types it
will travel to; it must be able to see the town from the dungeon entrance's wilderness view.

## When it pauses

Common, and intended:
- a flee-list monster blocks the only way to the stairs
- no food, no prayer due, and no town in reach
- a question or screen it doesn't know

Press F12 to resume after handling it.

## Dev tools

`tools\botctl.ps1 "<command>"` writes to `adombot\cmd.txt` and prints the status. Commands:
`start`, `stop`, `reload`, `dump`, `sell` (start a sell trip now), `keys <text>` (`{ESC}`,
`{ENTER}`, `{SPACE}`), `cmd GC_<NAME>` (post a game command), `lua <code>`.

## Files

- `src\adom-bot.noe` loads the rest and hooks the game's frame loop, event dispatcher and keys.
- `src\adom-bot-core.noe` has the command file, status, and key/command sending.
- `src\adom-bot-brain.noe` holds the ordered rules (pray, flee, attack, eat, rest, engage,
  pickup, explore, stairs), pathfinding, prompt handlers and stall detection.
- `src\adom-bot-sell.noe` is the sell/food trip. It plugs its rules and prompts into the brain's lists.
- `src\adom-bot-config.noe` holds all settings.
