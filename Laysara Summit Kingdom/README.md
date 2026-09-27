# Laysara: Summit Kingdom

Unreal Engine 4.27 (`AS\Binaries\Win64\AS-Win64-Shipping.exe`). Mods run on
**UE4SS**, not BepInEx.

## Extra Income

Adds a monthly income of your choosing to the treasury of the map you are playing,
and shows it as its own **Extra income** row in the income breakdown (the tooltip on
the treasury), between *Exporting goods* and *Donations*. Total Revenue and Balance
in the breakdown include the extra.

**Set in game.** Hover the treasury, then press **+** / **-** while the breakdown is
open (numpad or main keyboard; **Shift** steps by 100 instead of 10). Each map keeps
its own amount in `amounts.txt`. Defaults and step sizes are in `Scripts/config.lua`.

How it works:

- **Payout.** When the game mode's month counter (`GetBaseTimeUnitCounterInt`)
  advances, the mod adds the amount straight into `ASMoneyManager.Money`, capped at
  treasury capacity like the game's own income. The game's `BaseIncome` is left
  alone: it is saved into the save file, so changing it would bake the extra into
  every save.
- **Breakdown.** A post-hook on `BP_ASMoneyTooltip_C:FillData` places two `AS_Text`
  widgets in the Revenues grid's unused row 5, styled like the "Assistance from The
  Capital" row.

Only the money itself reaches the save file. Remove the mod and income goes back to
normal.

## Contents

| Path | What |
|---|---|
| `dist/ExtraIncome.zip` | the mod, ready to extract into the game folder |
| `dist/ExtraIncome-Source.zip` | full source release |
| `src/ExtraIncome/` | the mod itself, as installed |
| `src/docs/` | the `INSTALL.txt` written into each zip, plus `BUILD.txt` |
| `src/reference/` | `pakx.py` (pak lister/extractor) and the probe mod used to map the money system |
| `src/package.ps1` | builds the zips |

`dist/ExtraIncome.zip` needs UE4SS already installed. No UE4SS loader is bundled
here; see `dist/LOADER-NOT-BUNDLED.md`. Extract the zip into the folder that holds
`Laysara.exe`. The mod ships an `enabled.txt`, so UE4SS loads it without a
`mods.txt` entry.

## Status

Tested in game on Map11 (sandbox). The breakdown row appears, and +/- changes the
amount and the totals live. The monthly payout was not observed with a non-zero
amount during that test; its log line reads `[ExtraIncome] Map11: +N for 1
month(s), treasury A -> B`.

## Uninstall

Delete `AS\Binaries\Win64\ue4ss\Mods\ExtraIncome\`. The mod does not patch or
replace any game file.
