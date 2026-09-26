# No BepInEx loader in this folder

An `AMRAAM-Pack.zip` exists: BepInEx 5.4.23.5 (x64, Mono) plus the AMRAAM mod,
self-contained, so there is no separate loader install to get wrong. It is **not**
in this repo, following the rule in `INVENTORY.md`: no mod loader is bundled
anywhere here.

The Pack is 4.9 MB, of which about 0.6 MB is BepInEx. `AMRAAM.zip` in this
folder is the same mod without it, at 4.2 MB (mostly the missile mesh and texture).

## Installing without the Pack

Get **BepInEx 5.4.x, x64** (the Unity Mono build) from the BepInEx releases page.
Extract it into the game folder (the one with `NuclearOption.exe`) and start the
game once. Then extract `AMRAAM.zip` over it. That puts the mod in
`BepInEx\plugins\AMRAAM\`.

Nuclear Option 0.33.4 is Unity 2022.3, **Mono, x64**:

* BepInEx **6** and the IL2CPP builds will not load it.
* An **x86** build will not load an x64 process.

`NuclearOption.KillCostTracker.zip` needs the same BepInEx.

## Rebuilding the Pack

`src\AMRAAM\package.ps1` builds and tests the mod, then writes `AMRAAM.zip`,
`AMRAAM-Pack.zip` and `AMRAAM-Source.zip` to `release\`. It takes BepInEx
(`winhttp.dll`, `doorstop_config.ini`, `BepInEx\core`) from the game install it
sits in, so the source folder must be at `<game>\ModSource\AMRAAM` and that install
must already have BepInEx 5. Before building, restore `assets\` from `AMRAAM.zip`
(see `src\AMRAAM\README.md`).

BepInEx is LGPL-2.1, Copyright (c) 2018 Bepis.
