# Die for the Lich: Meta-Progression Mod

Installed for game version `v0.8.2` / Godot `4.4.1`.

**Testing balance override:** every Legacy upgrade level costs exactly **1 Soul
Shard**. A respec refunds 1 shard for every purchased level. Set
`TEST_UPGRADE_COST` to `0` in `source\mods\meta_progression.gd` and rebuild to
restore the normal costs stored in each upgrade definition.

## Features

- Normal runs award persistent **Soul Shards** when they end in victory or defeat.
- Reward formula: `ceil(highest depth / 2)`, plus `5` for a victory.
- Save-and-exit grants nothing, preventing duplicate rewards.
- Daily Runs grant no shards and disable all Legacy bonuses.
- The title screen has a new **Legacy** button displaying the current shard total.
- Legacy upgrades can be fully respecced for a 100% Soul Shard refund.
- Starting dice, trinkets, and abilities are added to a run-local copy of the
  character loadout; starting a new run never permanently grows the native loadout.
- Bulwark armor is granted every fight and survives the first player turn.
- The Stats screen lists every registered enemy, including Imp Brother and Imp
  Sister, which the base game's substring filter accidentally hid.
- **Runs Won** is a new persistent Stats category and increases only when a run
  ends in victory.
- Legacy upgrades:
  - **Vitality:** +5 starting maximum HP per level.
  - **Bulwark:** +2 armor at the start of every fight per level.
  - **Scholar:** +10% experience gained per level.
  - **Loaded Bag:** +1 random starting die; levels grant Common/Uncommon/Rare quality.
  - **Heirloom:** +1 random starting trinket; levels grant Common/Uncommon/Rare quality.
  - **Arcane Legacy:** +1 random starting ability; levels grant Uncommon/Rare/Legendary quality.
  - **Fated Arsenal:** the first weapon chest actually opened each run contains a Legendary weapon and Legendary shield.
  - **Lucky Sixes:** add 1/2/3 Lucky D6 to the starting bag.
  - **Lucky Twenties:** add 1/2/3 Lucky D20 to the starting bag.
  - **Inheritance:** start with 3/6/9 coins.
  - **Clover Patch:** add 1/2/3 clovers to the starting bag.
  - **Bottomless Draw:** on turn 1 of every fight, draw until the tray is full or the bag is exhausted.
  - **Second Chance:** +1 free die reroll per fight per level.

All entries above cost 1 shard per level in this testing build.

The Fated Arsenal claim is stored in the normal run save, so save/continue
cannot grant it twice. All run-start grants apply only to fresh non-Daily runs.

Meta-progression is stored separately from the game's normal save data at:

`%APPDATA%\Godot\app_userdata\Die for the Lich\data\<Steam ID or offline>\meta_progression.json`

## Installed files

- Active package: `Die for the Lich.pck`
- Verified original backup: `meta_progression_mod\backup\Die for the Lich.original.pck`
- Standalone mod build: `meta_progression_mod\build\Die for the Lich.meta-progression.pck`
- Retained source: `meta_progression_mod\source`
- UI captures: `meta_progression_mod\screenshots`

Run `meta_progression_mod\install.ps1` from PowerShell to reinstall the packaged mod build.

## Roll back

Close the game, open PowerShell in the game folder, and run:

```powershell
& '.\meta_progression_mod\uninstall.ps1'
```

The separate `meta_progression.json` save may be kept for later or deleted to reset all Soul Shards and upgrades.

## Verification

- Original SHA-256: `ECBB0E635722D2955CC0BF362361D16125A6263677A00A3E547AB1F23F79FE33`
- Mod SHA-256: `B6054E5E5C4176AE8521C041AE5E6BF1EC2012D9E95558B2954F97337499DC37`
- Main-menu Legacy label visual regression: passed.
- Scrollable 13-upgrade Legacy panel, readable max-state styling, and fixed respec footer visual regression: passed at both scroll extremes.
- Exact-engine title/menu runtime test: passed with zero script or load errors.
- Installed-package smoke test: passed with zero script or load errors.
- Earn-save-reload-purchase persistence cycle: passed.
- Version-2 save migration and full-refund respec cycle: passed.
- Fresh-run loadout immutability, all starting grants at level 3, Legendary ability, and five-use reroll probes: passed.
- Bulwark grant/retain/expire cycle and 15-slot full-bag first-turn draw: passed.
- First-open Legendary weapon/shield pair, later-chest fallback, and save-state persistence: passed.
- Flat-cost runtime probe: all 43 purchasable levels cost 1 shard; full respec refunds 43.
- Statistics runtime probe: all 20 registered enemies are visible, both Imp
  variants have distinct entries, and a simulated win increments Runs Won once.
