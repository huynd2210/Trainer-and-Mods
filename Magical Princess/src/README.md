# Magical Princess Trainer (BepInEx)

A hotkey trainer for **Magical Princess** (Unity 6, Mono). It loads through BepInEx and
modifies nothing on disk — no game file is patched, and removing the mod removes the cheats.
**Version 1.1.0.**

---

## Installation

**`MagicalPrincessTrainer-Pack.zip` — you don't have BepInEx yet (recommended)**

1. Close the game.
2. Extract the zip into your game folder — the one holding `MagicalPrincess.exe`.
   `winhttp.dll` must end up right next to the exe, with `BepInEx\` beside it.
3. Launch `MagicalPrincess.exe`. The panel appears at the top left.

**`MagicalPrincessTrainer.zip` — you already have BepInEx 5**

Extract it into the game folder, or just drop `MagicalPrincessTrainer.dll` into your
existing `BepInEx\plugins\`.

To uninstall: delete `winhttp.dll`, `doorstop_config.ini`, `.doorstop_version` and the
`BepInEx` folder.

---

## Hotkeys

| Key | Effect |
|-----|--------|
| **F1** | Show/hide the panel |
| **F2** | +10,000 money |
| **F3** | +100 black coin |
| **F4** | Stress → 0 |
| **F5** | Refill action points |
| **F6** | +25 to all 16 sub-attributes |
| **F7** | +10 skill points |
| **F8** | +1,000 battle EXP |
| **F9** | +50 father affection |
| **F10** | +50 reputation |
| **End** | +20 affection with every friend you have met (caps at 100) |
| **Page Up** | +250 achievement points (the meta-currency — works on the title screen) |
| **F11** | Toggle **god mode** — your party takes no damage |
| **F12** | Toggle **one-hit kill** — anything you hit dies |
| **Insert** | Toggle **no stress / full AP** — holds stress at 0 and action points at max |
| **Home** | Toggle **activities always succeed** |

Every press flashes a confirmation line under the panel and plays a short UI sound, so you
always know it registered. Toggles remember their state between launches.

---

## The panel

Top left, deliberately small: money, black coin, stress, action points, skill points and
battle level, plus the key list with each toggle's state. `F1` hides it entirely — the
hotkeys keep working, and confirmations still flash.

It reads *"No save loaded"* on the title screen and while loading. That is not an error:
the run cheats need a live run, so they refuse politely until you are in one. Achievement
points are the exception — they belong to the profile rather than the run, so that line
stays visible and Page Up keeps working at the title.

---

## Notes on individual cheats

**Attributes (F6)** raise the 16 sub-stats (strength, literature, beauty, …). The four
category levels and the battle stats derived from them are recalculated immediately, so
one press can move several levels at once. Level-up fanfares are suppressed on purpose.

**Stress and action points (Insert)** are re-applied every frame. Anything that would cost
you AP still runs its normal flow — the meter simply refills behind it.

**God mode (F11)** blocks physical and magical damage against your side, and also cancels
burn, poison and any other per-tick drain by topping your HP back up. It does not resurrect
someone already down.

**One-hit kill (F12)** raises the power of hits *you* land to a value nothing survives.
Enemy defence is still subtracted, so a boss's damage-immunity phases behave as the game
intends; it kills as soon as it can hurt them at all.

**Activities always succeed (Home)** forces the success roll used by work, training, dates
and crafting. Rewards, affection and item drops all follow the game's own success path.

**Achievement points (Page Up)** are the cross-run currency you spend in the *achievement
gift* menu when starting a new loop — the one that grants starting attributes, gold,
weapons or a head start on a friend's affection. Press it on the title screen with that
menu open and the budget and the item list update immediately; every gift you can now
afford becomes selectable. **990 points buys the most expensive gift in all 23 categories**,
so the default 250 per press covers the whole board in four taps. The points live in your
profile data (`gstatus`), so they stick once the game saves that slot — start the loop, and
save as usual.

Achievements themselves are not unlocked by this; only the currency changes.

**Friend affection (End)** only touches friends you have already met, and stops at the
game's own cap of 100. Love events still need their normal triggers.

Money, stress and the rest are edited in the save data the game holds in memory, so they
persist through a normal save. Nothing writes to your save file directly.

---

## Settings

`BepInEx\config\com.magicalprincess.trainer.cfg` is written on first launch. Every hotkey
is rebindable there, and every amount (money per press, attribute step, and so on) is a
plain number you can change. `UiScale` under `[Overlay]` enlarges the panel; it already
scales with your resolution.

---

## Building from source

```
dotnet build src\MagicalPrincessTrainer.csproj -c Release
```

`package.ps1` builds both zips; `package.ps1 -Install` also copies the result into the game
folder. The project references the game's own assemblies out of
`MagicalPrincess_Data\Managed`, so keep the `GameManaged` path in the `.csproj` pointing at
your install.
