# How to add a Custom Event

A practical, copy-paste guide. For the full field reference and internals, see
[README.md](README.md).

---

## TL;DR — three steps

1. Open **`scripts/custom_events/definitions.nut`** and add a `::CustomEvents.register({ ... })` block.
2. Rebuild the zip:
   ```
   powershell -ExecutionPolicy Bypass -File _modsrc\custom_events_build\build_mod.ps1
   ```
3. **Restart Battle Brothers.** Random/conditional events join the travel pool; load a
   save and travel (or enter a town, or press **Ctrl+Shift+E**) to see them.

> You must restart the game after every rebuild — BB only reads mods at launch.

---

## Step 1 — Start from the smallest possible event

Paste this at the bottom of `definitions.nut`, above nothing in particular — order
doesn't matter. This is a complete, working event:

```squirrel
::CustomEvents.register({
    id = "event.mymod.first_event",      // must be unique across ALL events
    title = "A Quiet Evening",
    trigger = "random",                   // shows up while travelling
    weight = 10,                          // how likely (higher = more often)
    screens = [
        {
            id = "A",                     // the FIRST screen MUST have id "A"
            text = "The company makes camp. Nothing stirs in the dark.",
            options = [
                { text = "Rest." }        // no 'goto' => closes the event
            ]
        }
    ]
});
```

Rebuild, restart, travel a while — eventually "A Quiet Evening" pops up. That's the
whole loop. Everything below is just adding choices and consequences.

---

## Step 2 — Add a choice with consequences

An event is a list of **screens**. The player sees screen `"A"` first. Each **option**
can send them to another screen with `goto`. Each screen can carry **effects** that
fire when it's shown.

```squirrel
::CustomEvents.register({
    id = "event.mymod.lost_coin",
    title = "On the Road...",
    trigger = "random",
    weight = 10,
    cooldown = 10,                        // days before it can fire again
    screens = [
        {
            id = "A",
            image = "gfx/ui/events/event_16.png",
            text = "%subject% spots a coin purse half-buried in the mud. Take it?",
            options = [
                { text = "Pocket it.",  goto = "Took" },
                { text = "Leave it." }                  // closes
            ]
        },
        {
            id = "Took",
            text = "A tidy little find. %subject% grins.",
            effects = [
                { type = "money", value = [40, 90] }    // a random 40-90 crowns
            ],
            options = [ { text = "Onward." } ]
        }
    ]
});
```

`%subject%` is a random brother the framework picks for this firing (its pronouns
`%they_subject%`, `%their_subject%`… work too).

---

## Step 3 — Branch on luck

Add `chance` (a percent) to an option. On success it follows `goto`; on failure,
`gotoElse`:

```squirrel
{ text = "Try to grab the runaway goat.", goto = "Caught", chance = 60, gotoElse = "Missed" }
```

```squirrel
{
    id = "Caught",
    text = "Got it! Fresh meat tonight.",
    effects = [ { type = "item", id = "scripts/items/supplies/cured_venison_item" } ],
    options = [ { text = "Dinner." } ]
},
{
    id = "Missed",
    text = "The goat bolts. %subject% trips into a ditch.",
    effects = [ { type = "injury", target = "subject", severity = "light" } ],
    options = [ { text = "Ow." } ]
}
```

---

## The building blocks

```
event
 ├─ id / title / trigger / weight / cooldown / condition / variables
 └─ screens[]
      ├─ id            ("A" = entry, required)
      ├─ image         (optional banner, "gfx/ui/events/event_NN.png")
      ├─ text          (flavour, supports %placeholders% and {a | b} variants)
      ├─ effects[]     (applied when the screen shows; each prints an outcome row)
      └─ options[]
           ├─ text
           ├─ goto      (next screen id; omit/0 = close)
           ├─ chance / gotoElse   (probabilistic branch)
           └─ action    (optional: function(_event){ ... } run on click)
```

---

## Recipe book

### A pure dialogue / lore event
```squirrel
{ id = "A", text = "An old soldier shares a war story by the fire.",
  options = [ { text = "Listen." } ] }
```

### Pay crowns for something
```squirrel
options = [ { text = "Buy supplies (-150).", goto = "Bought" }, { text = "Pass." } ]
// ...
{ id = "Bought", effects = [
    { type = "money", value = -150 },
    { type = "item",  id = "scripts/items/supplies/bread_item", count = 3 }
], options = [ { text = "Done." } ] }
```
(Spending is clamped — you can never drop below 0 crowns.)

### Reward / punish a specific brother
```squirrel
effects = [
    { type = "meleeskill", target = "subject", value = 2 },   // permanent
    { type = "xp",         target = "subject", value = 150 }
]
```
`target` can be `"subject"`/`"random"`, `"all"`, `"player"`, or a roster index like `0`.

### Affect the whole company
```squirrel
effects = [
    { type = "mood",   target = "all", value = 0.3, note = "A morale-boosting victory" },
    { type = "renown", value = 25 }
]
```

### Fires only in towns
```squirrel
::CustomEvents.register({
    id = "event.mymod.market_day", title = "Market Day", trigger = "settlement",
    cooldown = 6,
    screens = [ { id = "A", text = "%settlement% bustles with traders today.",
        options = [ { text = "Browse." } ] } ]
});
```

### Fires only when wealthy / late game (conditional)
```squirrel
trigger = "random",
condition = { minDay = 30, minMoney = 5000 },
```
Available condition keys: `minDay`, `maxDay`, `minMoney`, `maxMoney`,
`minBrothers`, `maxBrothers`, `hasFlag`, `notFlag`. Or a predicate:
```squirrel
condition = function(_event) { return ::World.getPlayerRoster().getAll().len() >= 8; }
```

### Player-triggered (Ctrl+Shift+E)
```squirrel
trigger = "onDemand",
menuLabel = "Hold a war council",     // label shown in the hotkey menu
```

### Remember something across events (flags)
```squirrel
// In one event's outcome:
effects = [ { type = "flag", key = "mymod.helpedWitch", value = true } ]

// Then gate another event on it:
condition = { hasFlag = "mymod.helpedWitch" }
```

### Do something the effects list can't (custom code)
```squirrel
effects = [
    { type = "custom", func = function(_event, _screen) {
        ::World.Assets.getStash().add(::new("scripts/items/weapons/named/named_axe"));
        return { id = 10, icon = "ui/icons/special.png", text = "A rare blade!" };
    } }
]
```

---

## Effect cheat-sheet

| `type` | Fields | Notes |
|--------|--------|-------|
| `money` | `value` | negative = spend (clamped at 0) |
| `renown` | `value` | business reputation |
| `moral` | `value` | moral reputation |
| `hp` `resolve` `fatigue` `meleeskill` `rangedskill` `meleedefense` `rangeddefense` `initiative` | `target`, `value` | permanent base-stat change |
| `xp` | `target`, `value` | experience (+ auto level-up) |
| `injury` | `target`, `severity` | `"light"` or `"heavy"` |
| `mood` | `target`, `value`, `note` | needs Legends/MSU (skipped otherwise) |
| `item` | `id`, `count` | `id` = an item script path |
| `flag` | `key`, `value` | persistent company flag |
| `custom` | `func` | `function(_event,_screen)`; return a row or array of rows |

- `value` may be an integer or a `[min, max]` range (rolled each time).
- Add `note = "..."` to any effect to override its outcome-row text.

---

## Text tricks

| Placeholder | Becomes |
|-------------|---------|
| `%subject%` | the brother chosen for this event |
| `%they_subject%` `%their_subject%` `%them_subject%` | his/her pronouns |
| `%settlement%` | the town entered (settlement events) |
| `%companyname%` | your company's name |
| `%randombrother%` `%randomname%` `%randomtown%` | random flavour |
| `%SPEECH_ON%` … `%SPEECH_OFF%` | wraps spoken dialogue styling |

`{like | this | one}` in text picks one variant at random. Declare your own with
`variables = { merchant = "Old Aldo" }` then write `%merchant%`.

Find banner images by browsing `gfx/ui/events/event_*.png` in the game's `data` —
e.g. `event_16` (plains), `event_20` (town), `event_76` (generic), `event_82` (camp).

---

## Gotchas

- **The entry screen's `id` must be `"A"`.** Other screens can be named anything.
- **`id` must be globally unique.** Prefix with your mod, e.g. `event.mymod.thing`.
- **Rebuild + restart** after every change — there is no hot-reload.
- **`onDemand` needs MSU** for the hotkey. Random/settlement/conditional work without it.
- **`item` ids are script paths without `.nut`,** e.g. `scripts/items/supplies/medicine_item`.
- An option with no `goto` (or `goto = 0`) **closes** the event.
- Put consequences on the **destination screen's** `effects`, not on the option.

---

## Did it work? Check the log

The game writes `log.html` to (on this PC):
```
C:\Users\Admin\OneDrive\Documents\Battle Brothers\log.html
```

After launching, you should see:
```
CustomEvents: framework ready (N definitions registered)
CustomEvents: on-demand hotkey registered (Ctrl+Shift+E)
```
After loading a save:
```
CustomEvents: injected N custom event(s) into the world event pool
```

If an event misbehaves, search the log for `CustomEvents:` (warnings about unknown
effect types or unfireable ids) or for your event's `id`. A line like
`The BB class '...' was never proceessed for hooks` means a hooked class path is wrong
(framework bug, not your data).
