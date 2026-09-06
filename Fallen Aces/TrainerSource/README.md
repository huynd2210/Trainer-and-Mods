# Fallen Aces Trainer

A BepInEx plugin that puts the game's cheats behind hotkeys and an on-screen panel,
instead of typing console commands. Fifteen entries covering survival, combat,
movement and world state.

Built against Fallen Aces v0.9.1 (Unity 6000.3.10, BepInEx 5.4.23.5).

## Using it

`Insert` opens and closes the overlay. Inside it:

| Key | Does |
| --- | --- |
| `Up` / `Down` | Move the cursor |
| `Right` | Switch on, run, or step the value up |
| `Left` | Switch off, or step the value down |
| `Insert` | Close |

The four cheats you want mid-fight also have direct keys that work with the overlay
closed: `F5` god mode, `F7` infinite ammo, `F10` heal to full, `F11` noclip. Every key
is rebindable in `BepInEx/config/local.codex.fallenacestrainer.cfg`; set one to `None`
to unbind it.

With the overlay closed, a small panel in the bottom-left lists whatever is currently
switched on, and disappears entirely when nothing is. Toggling anything also raises the
game's own notification banner.

## What each entry does

**Survival**

- **God mode** — blocks all damage to the player. Falling out of the world still kills,
  matching the rule the game's own invulnerability uses; surviving it would strand you
  under the level.
- **Infinite stamina** — the game's `steroids` cheat: sprinting, kicking and dodging stop
  draining the bar.
- **Invisible to enemies** — the game's `ignoreme` cheat. Enemies stop noticing you;
  alerts already in progress still play out.
- **Heal to full** — refills health and toughness.

**Combat**

- **Infinite ammo** — tops every weapon in your inventory back up to its magazine size
  each frame, and tells the HUD so the counter agrees.
- **One-hit kills** — multiplies damage dealt to anything that is not the player, by
  enough to get through a full toughness bar. Enemies with divided health bars still lose
  one division per hit, because the game clamps damage at each division boundary.
- **Super punch** / **Roundhouse kick** — the game's `superpunch` and `roundhouser`.
- **Neutralise all enemies** — kills every living enemy in the level. Scripted encounters
  may not expect this; it is scoped to enemies, so civilians and other NPCs are untouched.

**Movement**

- **Noclip** — the game's own noclip movement state. Jump and crouch move up and down,
  sprint goes faster. The state machine refuses it in some states (dead, in a vehicle) and
  the trainer says so rather than silently claiming it worked.
- **Mark position** / **Recall position** — remember a spot and teleport back to it.

**World**

- **Game speed** — 0.1x to 4x in 0.25 steps, through the game's `TimeScaleManager`, so the
  pause menu still stops time properly and unpauses back to your setting.
- **Infinite lighter** — the game's `permalight`.
- **Add $500** — pays you the same way picking up loot does.

## Notes worth knowing

**It does not write cheats into your saves.** The game's own `god` console command sets
`Health.Invulnerability`, and that field *is* serialised into save files — quicksave with
it on and the save stays invulnerable. This trainer instead intercepts `Health.TakeDamage`
with a Harmony prefix and never touches the field, so saves stay clean. Every other toggle
uses a field the game does not serialise.

**Toggles survive level loads.** Flags like infinite stamina live on the `Player`, which is
rebuilt on every level and save load. The trainer re-applies whatever is switched on
whenever it sees a new `Player`, rather than writing them every frame.

**Hotkey defaults avoid keys already in use.** The game binds `1`-`5`, `WASD`, `` ` ``,
`CapsLock`, `Ctrl`, `E`, `Enter`, `=`, `Esc`, `F`, `F1`-`F4`, `F6`, `F9`, `G`, `H`, `-`,
`Q`, `R`, `Shift`, `Space`, `Tab`, `X` and `Z`. The other mods in this folder take `]`
(SuperHot), `F8` (Kill Tracker) and `6`-`0` (Expanded Inventory). The startup check fails
if a default lands on any of them. The arrow keys are bound by the game to a `Test Move`
action that no game code ever reads, which is why the overlay can use them.

**Game speed and the SuperHot mod both want the clock.** Setting game speed away from 1.0
makes SuperHot disengage on its own, because it only takes over while the unpaused
timescale is 1. Set it back to 1.00x to hand the clock back.

**The trainer hides while a game menu or the developer console is open**, and ignores its
own keys then, so arrows and F-keys stay theirs.

## Building

```powershell
.\build.ps1              # compile and copy into BepInEx\plugins
.\build.ps1 -NoInstall   # compile only
```

Needs a .NET SDK; it compiles with Roslyn directly against the game's `Managed` folder
and `BepInEx\core`.

## Verification

`build.ps1` then launching with `-trainer-smoke-test -batchmode -nographics` runs a
startup check and quits by itself. It confirms the damage patch installed, that the game
APIs each cheat depends on still exist, that feature ids are unique, and that no default
hotkey collides with the game or a sibling mod.

That check is startup only. **Gameplay behaviour and the on-screen overlay have not been
tested in a running level** — the smoke test runs headless, so nothing here has been seen
rendered in the game. The overlay's layout was checked against a pixel-faithful mock at
the same sizes and colours, which is not the same as seeing Unity draw it. Treat the
cheats and the panel as needing a manual pass.

## Layout

| File | Holds |
| --- | --- |
| `TrainerModel.cs` | Feature/binding model and the safe game accessors. No cheats. |
| `TrainerCheats.cs` | The registry: one entry per cheat, and nothing else. |
| `TrainerPlugin.cs` | BepInEx glue: config, input, overlay, damage patch, smoke test. |

Adding a cheat means adding one element to the array in `TrainerCheats.Build()`. The
input loop, overlay and ambient strip all walk that array and never name a cheat, so
nothing else changes.
