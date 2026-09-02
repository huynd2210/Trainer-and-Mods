# Custom Events — a data-driven event framework for Battle Brothers

Define Battle Brothers world events as plain **data tables** instead of hand-written
event classes. The mod turns each definition into a real in-game event.

- **Installed mod:** `C:\Games\Battle Brothers\data\mod_custom_events.zip` (id `mod_custom_events`)
- **Editable source:** `C:\Games\Battle Brothers\_modsrc\custom_events_build\`
- **Dependencies:** Modern Hooks (required). MSU (optional — only the on-demand hotkey needs it).
  Built and tested against the Legends environment.

## Where things live

```
scripts/!mods_preload/mod_custom_events.nut   the framework (registry, effect/condition
                                              engine, triggers, hooks)
scripts/custom_events/custom_event.nut        generic event class (one instance per def)
scripts/custom_events/definitions.nut         >>> YOUR EVENTS GO HERE <<<
build_mod.ps1                                  re-zips the scripts/ tree into data\
```

To add or change events, edit **`definitions.nut`** and rebuild:

```
powershell -ExecutionPolicy Bypass -File _modsrc\custom_events_build\build_mod.ps1
```

The build writes explicit directory entries with forward-slash names so `::include`
and `this.new` resolve correctly (a zip without directory entries loads nothing).

## Triggers

| `trigger`      | When it fires                                                        |
|----------------|----------------------------------------------------------------------|
| `"random"`     | During world-map travel, weighted by `weight` (vanilla behaviour).   |
| `"settlement"` | When the company enters a town/city.                                 |
| `"onDemand"`   | Manually, via **Ctrl+Shift+E** (needs MSU). A menu appears if >1.    |

Any event may also carry a `condition` to gate when it's allowed to fire (e.g. only
when wealthy, only after day 20). A `"random"` event + a `condition` = a conditional
event.

## Definition format

```squirrel
::CustomEvents.register({
    id        = "event.my_mod.something",   // REQUIRED, unique
    title     = "On the road...",
    trigger   = "random",                    // random | settlement | onDemand
    weight    = 12,                          // random pool weight
    cooldown  = 15,                          // days before it can fire again
    menuLabel = "Do the thing",              // label in the on-demand menu
    condition = { minDay = 5, minMoney = 1000 },   // optional, see below
    variables = { merchant = "Old Aldo" },   // optional literal %placeholders%
    screens   = [ /* entry screen MUST have id "A" */ ]
});
```

**Screen**
```squirrel
{
    id      = "A",                            // entry screen MUST be "A"
    image   = "gfx/ui/events/event_16.png",  // optional banner
    text    = "Flavour text. %subject% steps forward.",
    effects = [ /* applied when this screen shows */ ],
    options = [ /* omit on pure outcome screens */ ]
}
```

**Option**
```squirrel
{ text = "Pay them",  goto = "Paid" }                      // goto a screen, or omit to close
{ text = "Rob them",  goto = "Win", chance = 60, gotoElse = "Lose" }   // probabilistic
{ text = "...", action = function(_event) { /* code on click */ } }
```

**Effects** (each adds a row to the outcome list)

| Effect | Example |
|--------|---------|
| Crowns | `{ type = "money", value = -100 }` (range ok: `value = [50,150]`) |
| Renown (business rep) | `{ type = "renown", value = 25 }` |
| Moral reputation | `{ type = "moral", value = -1 }` |
| Permanent stat | `{ type = "hp" \| "resolve" \| "fatigue" \| "meleeskill" \| "rangedskill" \| "meleedefense" \| "rangeddefense" \| "initiative", target = "subject", value = 5 }` |
| Experience | `{ type = "xp", target = "all", value = 100 }` |
| Injury | `{ type = "injury", target = "subject", severity = "light" \| "heavy" }` |
| Mood (Legends/MSU) | `{ type = "mood", target = "all", value = 0.5, note = "..." }` |
| Item | `{ type = "item", id = "scripts/items/supplies/roborant_potion_item", count = 1 }` |
| Persistent flag | `{ type = "flag", key = "my.flag", value = true }` |
| Custom code | `{ type = "custom", func = function(_event,_screen){ return {id=10,icon="..",text=".."}; } }` |

- **`target`**: `"subject"`/`"random"` (the event's chosen brother), `"all"`, `"player"`, or a roster index.
- **`value`**: an integer, or `[min,max]` rolled each time.
- **`note`**: overrides the auto-generated outcome row text.

**Conditions** (declarative — every listed key must pass):
`minDay`, `maxDay`, `minMoney`, `maxMoney`, `minBrothers`, `maxBrothers`,
`hasFlag = "x"`, `notFlag = "x"` — or a predicate for full control:
`condition = function(_event) { return ::World.Assets.getMoney() > 5000; }`
(predicates get the event as `_event`; use `::World`/`_event.World`, not bare `this`).

**Text placeholders:** `%subject%` (+ `%they_subject%`, `%their_subject%`, …),
`%settlement%`, `%companyname%`, `%randombrother%`, `%randomname%`, `%randomtown%`,
`%SPEECH_ON%`…`%SPEECH_OFF%`, plus any you declare in `variables`. Use `{a | b | c}`
in text to pick one variant at random.

## How it works (internals)

- All definitions are injected into the world event pool by hooking
  `event_manager.create` and pushing one configured `custom_event` instance per def.
- `"random"` events score themselves via `onUpdateScore` (= `weight`, gated by `condition`).
- `"settlement"` events fire from a hook on `settlement.onEnter`.
- `"onDemand"` events fire from an MSU keybind; the menu itself is a generated event.
- Forced fires (settlement/on-demand) mirror the vanilla manager's select-and-show
  tail and clear `ActiveEvent` if a screen can't be shown, so nothing gets stuck.
- Cooldowns are stored in company flags (`mod_ce.cd.<id>`) so they survive saves.

## Example events shipped in `definitions.nut`

1. **A Peddler on the Road** — `random`, chance-based haggle/rob branching, money/item/injury.
2. **Tavern Rumours** — `settlement`, uses `%settlement%`, renown + mood + a flag.
3. **Drill the Company** — `onDemand`, permanent stat gains, XP, mood.
4. **A Stranger's Proposition** — `random` + `minMoney` condition, big risk/reward, custom-code effect.
