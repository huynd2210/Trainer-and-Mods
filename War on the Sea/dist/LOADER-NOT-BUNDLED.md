# No BepInEx loader in this folder

A `WoTSCheatPack-Pack.zip` exists — BepInEx 5.4.23.2 x64 plus all four mods,
self-contained, so there is no separate loader install to get wrong. It is **not**
in this repo, following the rule in `INVENTORY.md`: no mod loader is bundled
anywhere here. That rule was what took the repo from ~448 MB to ~82 MB.

The Pack is 663 KB, almost all of it BepInEx. `WoTSCheatPack.zip` in this folder
is the same four plugins without it, at 39 KB.

## Installing without the Pack

Get **BepInEx 5.4.23.2, x64** from the BepInEx releases page and extract it into
the game folder (the one with `WarOnTheSea.exe`), then extract
`WoTSCheatPack.zip` over it. That drops four DLLs into `BepInEx\plugins`.

Version matters. War on the Sea v1.09a is Unity 5.6.6f2, **Mono, x64**:

* BepInEx **6** and the IL2CPP builds will not load it.
* An **x86** build will not load an x64 process.

## Rebuilding the Pack

`src\package.ps1` builds all three zips, the Pack included. It expects an
extracted BepInEx at `wots-trainer\tools\bepinex\`; all four mod workspaces read
the loader from that one location. `src\BUILDING.txt` has the full setup.

BepInEx is LGPL-2.1, Copyright (c) 2018 Bepis.
