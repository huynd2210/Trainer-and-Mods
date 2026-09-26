# Developing CSD2 Autoplay

## Layout

    src/autoplay.gml     the bot (appended to O_foodbrain : End Step)
    src/indicator.gml    in-shift status line (appended to O_foodbrain : Draw GUI)
    src/campaign.gml     campaign mode, menu side (O_mainmenu : Begin Step)
    src/campaign_hud.gml campaign status line (O_mainmenu : Draw GUI)
    src/splash.gml       confirms medal / start-of-day screens (O_splashstartend : Begin Step)
    src/rewards.gml      clears the post-shift reward screens (O_unlockingmenu : Begin Step)
    tools/patch.csx      UndertaleModTool script that applies all six
    tools/verify.csx     decompiles the patched events back out
    tools/dumpall.csx    decompiles the whole game, for reference
    build.ps1            build without installing
    package.ps1          build the two distribution zips (repo root only)
    install.ps1          build + install (fetches the CLI if needed)
    uninstall.ps1        restore the verified backup

Edit `src/*.gml`, run `.\build.ps1`, then `.\install.ps1`. Both always start from
`backup/data.win.vanilla`, so patches never stack.

## Toolchain notes

- **`UndertaleModCli.exe` hangs forever unless stdin is closed.** Redirect from `/dev/null` (or
  `$null`). 0% CPU with no output means it is blocked on stdin, not working.
- This bytecode version (17, GMS 2.0) does **not** process `\n` escapes in strings. Use `chr(10)`.
- Locals may not shadow GML builtins — `fps`, `room`, `id` and friends fail to compile.
- `patch.csx` prints compile errors verbatim; don't let them get swallowed into a stack trace.

## How the bot works

It never simulates game logic. Each frame it reads the state the game reads, decides which key a
player would press, and calls `keyboard_key_press` / `keyboard_key_release`. Everything after
that — scoring, combos, validation — runs on untouched code.

Ingredient buttons read `keyboard_check` (held) gated by `LV_firstlockout`, which only re-arms on
key **release**. A press with no release locks that button permanently, so every tap is one frame
down then a release.

## Game mechanics you must know

These were expensive to work out. Changing the bot without them will break it.

### What to place

`RCP_ar[slot][2]`, produced by `SCRIPT_recipepopulate`. This is the same table the grader compares
against in `O_NEWingredientpane` user event 2:

    (LVT_<slot>button - LVT_STORE<slot>button) != RCP_ar[slot][2]  ->  +1 error

Slots map A1-A8 = 1-8, B1-B8 = 9-16, C1-C8 = 17-24. `LVT_STORE<slot>button` snapshots
`LVT_<slot>button` at pane open and decrements once per press, so the delta is the placed count.

Many recipes are `RCP_ar[arg3]` — the required slot is chosen at **random per order**. Pass the
pane's `LVG_randnumb*` args through and it is correct automatically. Do not try to enumerate
recipes statically; you will badly under-count.

Run `SCRIPT_recipepopulate` in `O_foodbrain` scope, **never on the pane**: it sets
`LSRCHK_cookrawtilt`, which the grader reads *before* recomputing it, so calling it on the pane
adds a phantom error to every order. None of the 32 variables it writes collide with
`O_foodbrain`.

### `LV_lockunlockcode` is a tier ladder, not a flag

It is a press **allowance**, not a requirement. Reading it as "still needed" makes the bot pile
every topping onto every dish.

The single press gate accepts only these bands: 0-11, 100-111, 200-211, 300-311, 400-411,
500-511, 600-610, 700-711, 900-911, 1000-1011, 10000-20001. Buttons often start **outside** every
band (611, 641, 751, 851, 451) and are cascaded down by pressing others. Landing a press on
exactly `x00` fires that tier's cascade:

| lands on | effect |
|---|---|
| `200` | `851` -> **1**, `751` -> **701**, own band -> -100, `250-261` and `750-761` -> -50 |
| `600` | own band -> -100, `610-700` -> -10 |
| `700` | `750-761` -> -50 only; **does not** lock siblings, so 700 is freely repeatable |
| `0`   | **everything else -> -100**, except `450-461` -> -50 (i.e. -> 401, unlocked) |

The `400` band has no cascade entry at all, so it is freely repeatable too.

**A button at code `1` is a finishing ingredient.** Pressing it ends placement for the whole dish.
Hold it back until every other wanted slot is either placed or sitting in `451-460` — that class
is unlocked *by* the finisher, so waiting for it instead deadlocks. Okonomiyaki (227) and
Agedashi Tofu (252) are the two shapes to test against.

### Two helpers per slot

`O_NEWingredientpane` creates **two** `O_NEWingredientbuttonshelper` instances per slot. Never
`+= 1` over helper instances to get a per-slot total — flag into a per-slot array and sum that.
Flags set with `= 1` and first-wins `if (x < 0)` picks are fine.

### Pages are not visibility

Off-page buttons are moved thousands of pixels away in `y`; every press handler gates on
`y < 1080`. Every button is `visible`. Page turn cycles `O_NEWingredientpane.LVT_panel` 1->2->3.
Both buttons and helpers move, but only those with `LV_controlkeycode < 2000` — serve and
page-turn helpers stay put.

### Stages, cooking, and tickets with no pane

`LV_stagephase` advances by 2 per Enter, or by 1 into `LV_endingstagephase` to finish.
`LVT_whichstage` counts **down** and selects the ingredient set; it is passed to
`SCRIPT_recipepopulate`, so stage correctness is free.

`LV_status`: 0/6 waiting, 1 cooking, 2 cooked and ready, 3 burnt, 100 done. In status 2 the dish
is on a timer to burning (and a kitchen fire), so ready dishes must outrank new orders. Never
open a status 1 ticket — there is nothing to place, Enter is locked, and opening a *cooking* dish
adds a raw-food error via `LV_rawfoodtilt`.

**Some tickets get no ingredient pane at all** (`LV_endingstagephase == 2`; or `== 6` reopened
from status 1/2; or `LS_HSstatus == 10`). `LV_selected` is cleared by the grader, which lives on
the pane — so those finish at status 100 with `LV_selected` still 1. Derive "which station is
open" from `LV_selected` **and** `LV_status != 100`, or the bot parks on dead stations.

### Mash tickets

`LV_ingredientminimum` counts **total presses** and can exceed the recipe: trash 416/417 want 6
and 11 for a two-item recipe, and the fire chore (402) wants 1000 with a limit of 100 — there,
`O_foodGFXcontroller` force-clears `LV_enterlocked` once the flames are out.

Gate this on `LVNEW_ingredientcount < LV_ingredientminimum`, **never** on `LV_enterlocked`, which
lags: it is cleared by the serve helper's `alarm[5]`, armed 5 frames out and re-armed each frame.
Reading the flag makes the fallback fire on ordinary dishes and add a stray ingredient. Every
slot is a candidate in that fallback, not just recipe slots — trash needs Sanitize, which has
`RCP_ar == 0` because it is in `RCP_arDoNotList`.

### Held pours

`LV_lockunlockcode > 10000` is hold-to-fill, and `LV_A7buttonreturn` is the code at the instant of
release. Beer (300/301) is graded on landing in (11925, 11935] — **tap** it rather than holding,
stopping on the code rather than a tap count. Food 208 wants (10900, 10920] and still holds.

### Holding stations

A dish with `LV_holdingstationcheck == 2` raises `LS_HSstatus == 8`; the item it wants is its own
foodcode **+ 1000**. `Hstations_array[101..109]` is the registry (`[1]` foodcode, `[2]` servings)
— initialised for exactly 101-109, so scanning outside that range throws. Menu candidates are
`LVA_fullmenuarray[120+k][1]`; `[3]` is `FOODGLOSS[base][15]`, which becomes the menu button's
`LV_ingredientkeycode`, so match on that.

A shift opens with a **prep phase** (`LV_prepornot == 0`, "Prep any food to start day") where
holding stations are stocked and **no `O_foodbar` exists**. Do not gate the bot on
`instance_exists(O_foodbar)` or it is inert for exactly that phase.

Stocking policy: track how many stations hold each dish as a **count**, not a covered/not flag.
Variety first (stock anything at count 0), then keep filling spares with whichever dish is held
least, always leaving one reserve free for an emergency. With a flag, five stations and two dish
types stop at 1+1 and leave three idle; with a count they reach 2+2 and leave one free.

## Campaign mode (F9)

`O_foodbrain` **is not placed in any room** — it is created at runtime when a shift starts. So
menu-side code cannot live in a foodbrain event; it silently never runs. Four separate bugs in
this feature were all the same shape: correct code hosted in an object that is not alive in that
room. Check which objects exist in a state before automating it.

| state | what is alive | host used |
|---|---|---|
| `ROOM_mainmenu` | `O_mainmenu` | `campaign.gml` on its Begin Step |
| medal / start-of-day splash in `ROOM_mainroom` | `O_splashstartend` only | `splash.gml` on its Begin Step |
| post-shift reward screens | `O_unlockingmenu` only | `rewards.gml` on its Begin Step |
| a running shift | `O_foodbrain` | `autoplay.gml` on its End Step |

The end of a shift walks all the way down that table: `O_foodbrain` shows the day-complete bar
and **deactivates itself** on the confirm, the `O_splashstartend` it spawns shows the results and
**destroys itself** when it spawns `O_unlockingmenu`, and that shows the rewards. Each hand-off
kills the previous host, so one confirm cannot cover two screens.

Starting a shift is what `O_itemcatalogdisplay` user event 0 does: spawn `O_loadingscreen` with
`LVAN_progress = 1`, then set `global.LVLwhichrest` / `global.LVLwhichstage`.

**Do not track progress in `global.LVLwhichstage`.** `O_mainmenu` user event 0 resets it (and
`LVLwhichrest`) to 0 in three places, so "continue from last + 1" restarts at shift 1 — replaying
the shift just finished. Keep a private cursor (`global.APC_rest` / `APC_shift`).

`global.MODEchefforhire[rest][shift]` **is the medal**, round-tripped verbatim into the save's
`[C4H]` `REST<n>` keys: `-1000` not a shift, `-1` locked, `0` unplayed, `1` bronze, `2` silver,
`3` gold. "Skip what is already gold" is `>= 0 && < 3`. `O_initvars` fills `[0..59][0..69]` and
reading outside that crashes.

Medal thresholds (`O_splashstartend` Step_2, only ever upgraded): bronze `global.Obad < 8`; silver
`Obad + Oaverage + Osick < 5`; **gold that sum == 0**, i.e. a flawless shift. Silver and gold also
require `global.UAsaturn[0] == 0` — with that assist on, a perfect shift still caps at bronze and
a gold grind can never finish, so the mod logs a warning when F9 is pressed with it set.

Splash screens advance on `keyboard_check_pressed(vk_enter)`. Begin Step runs before Step, so the
key is down when the screen looks for it; hold ~2 frames then release, because a key that never
goes up can never press again. Confirm from **one** host only — two presses advance two screens.

### The reward screens

**The number of presses varies and cannot be counted up front.** `O_unlockingmenu`'s `alarm[0]`
tallies the shift into `LV_gift` (+4 per level gained) and `LV_foodgift`; each accepted press
spends exactly one, and both counters are decremented at the *top* of their handler, so the walk
always terminates. After the gifts there may be one more screen for a restaurant upgrade
(`LVUNLa_stage == 4`) and one for a level-up (`stage == 5`). Press until the object is gone.

**Gate the press on `LVUNLa_stage >= 3`, not on a timer.** User event 15 drops input entirely
below that. Every reward restarts the animation at stage 0 and `alarm[1]` walks it back to 3 over
~24 frames, so a fixed-interval press throws away most of the rewards and then stalls holding the
ones it never saw.

**Release outside the gate.** A press that lands resets `LVUNLa_stage` to 0, which closes the
gate — a release placed behind it never runs and the key stays down forever.

## Debugging

The mod logs to `%LOCALAPPDATA%\NEW_CSD2_PS4Steam\autoplay.log`: every ticket state change, every
key press with the reason it was chosen, placed-vs-required at serve time, and a snapshot of what
the bot could see whenever it stalls. Nearly every bug in this mod's history was found by reading
that log and none of them were found by reasoning from the source alone — reach for it first.

## Licence

No licence file is included; add one before publishing. The mod is applied with
[UndertaleModTool](https://github.com/UnderminersTeam/UndertaleModTool), downloaded at install
time rather than redistributed. No game assets or decompiled game code are included here.
