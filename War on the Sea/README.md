# War on the Sea — Cheat Pack

Four BepInEx mods for **War on the Sea v1.09a** (x64 Mono build), distributed together.
Each is independent: its own hotkey, its own config file, and deletable on its own.

| | Default keys | What it does |
|---|---|---|
| **Trainer** | F1, F2 | Infinite command points; removes fog of war on the campaign map and in battles |
| **Ace Aviators** | F5, F6 | Your dive, torpedo and level bombers aim true — and optionally cannot miss |
| **More Targets** | F7 | The enemy campaign AI never runs out of command points |
| **Fire Control** | F8 | Close-in AA stops spraying; naval fire-control solutions acquire instantly |

Every key is a default only. All four write a file into `BepInEx\config\` on first run
and read their hotkeys from it.

Everything is player-side. No mod here improves enemy aim, enemy AA or enemy fire
control. More Targets is the only one that changes anything on the enemy's side, and all
it does is stop their treasury running dry.

## Install

`dist\WoTSCheatPack.zip` — the four plugins. Needs BepInEx 5.4.23.2 x64 already installed;
extract into the game folder. See `dist\LOADER-NOT-BUNDLED.md` for why the loader is not
shipped here and how to get the right build.

## What each one actually changes

**Trainer** — campaign purchases never fail and your pool never depletes. Deliberately does
*not* inflate the shared command-point array, because the enemy AI reads the same slot.
Reveal All puts every enemy unit on the campaign map with exact composition in tooltips, and
marks every enemy in a battle detected and identified.

**Ace Aviators** — removes the random aim error the game rolls into every air attack (70 m
radius for level bombing, 80 m for an aerial torpedo, more again for a dive against a fast
turning target). `AimOnly`, the default, stops there: your crews stop missing for no reason
but a destroyer that puts the helm over still gets away. `AimAndGuidance` additionally steers
released ordnance onto the target and suppresses duds.

**More Targets** — overrides one enemy-side reading of the campaign budget, so the AI never
sits out a strategic decision for lack of funds and always raises the largest force tier.
Campaign only. It does **not** raise the sortie *rate*; that is set by two other throttles,
left at stock and exposed in config.

**Fire Control** — AA: the mount already aims at an exact intercept, so misses come from the
emitter cone plus a mount that re-aims only a few times a second. Both are tightened; nothing
homes. Gunnery: the fire-control solution converges on the first director tick instead of
being walked up over a minute of shooting. Shell dispersion is untouched — the guns still
scatter, they just stop aiming at a guess.

## How these were checked

Every mod ships two harnesses, both of which gate its build:

* a **signature harness** that loads the real `Assembly-CSharp.dll` and asserts every patched
  member exists with the expected signature. Harmony resolves targets by name at runtime, so
  a drifted signature would compile cleanly and fail silently in game.
* a **behaviour harness** that compiles the mod's real source — not a copy — and measures what
  it does. Ace Aviators flies its actual guidance code at a manoeuvring ship and scores hits
  against real hull dimensions, reproducing the game's own release logic; More Targets replays
  the campaign AI's decision sequence; Fire Control models the AA geometry from the game's own
  constants, which is how its defaults were chosen.

Some measured results, from those harnesses:

* Level bombing a turning light cruiser: **18%** hit rate vanilla, certain hit with the aim
  error removed.
* Close-in AA: collapsing the emitter cone *alone* makes AA **worse** (62% of rounds connect
  at the stock aim rate). Cone and aim rate together reach 100%. The defaults come from that
  table.

What none of them can judge is how any of this feels to play. That part is unverified.

## Source

`src\` has all four workspaces including both harnesses for each, plus `package.ps1` which
builds, tests, installs and zips the whole set. `src\BUILDING.txt` covers the prerequisites —
notably that BepInEx is not vendored here and which build you need.

`dist\WoTSCheatPack-Source.zip` is the same tree as a single zip.
