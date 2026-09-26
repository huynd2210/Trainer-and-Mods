# No BepInEx loader in this folder

A `MoreMoney-Pack.zip` exists: BepInEx 5.4.23.5 x64 plus the mod, self-contained.
It is **not** in this repo, following the rule in `INVENTORY.md` that no mod loader
is bundled here. The Pack is 629 KB, almost all of it BepInEx; `MoreMoney.zip`
in this folder is the same plugin without it, at 3 KB.

## Installing without the Pack

Get **BepInEx 5.4.23.5, x64** (`BepInEx_win_x64_5.4.23.5.zip`) from the BepInEx
releases page and extract it into the game folder (the one with `Restory.exe`),
then extract `MoreMoney.zip` over it.

ReStory is Unity 6000.3.10f1, **Mono, x64**:

* BepInEx **6** and the IL2CPP builds will not load it.
* An **x86** build will not load an x64 process.

## Rebuilding the Pack

`src\MoreMoney\package.ps1` builds all three zips, the Pack included. It expects an
extracted BepInEx at `vendor\BepInEx-5.4.23.5\` next to it, and the game at
`..\game\` (the `.csproj` references `Restory_Data\Managed\Restory.Assembly.dll`).

BepInEx is LGPL-2.1, Copyright (c) 2018 Bepis.
