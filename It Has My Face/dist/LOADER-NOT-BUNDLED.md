# No BepInEx loader in this folder

Each mod also has a `-Pack.zip` (`CloneSight-Pack.zip`, `KillTracker-Pack.zip`): BepInEx 5.4.23.5 (x64) plus that
one mod, self-contained, so there is no separate loader install to get wrong. They are **not** in this repo, because
of the rule in `INVENTORY.md` that no mod loader is bundled anywhere here.

## Rebuilding them

Each mod's `package.ps1` (`src/CloneSight/`, `src/KillTracker/`) builds all three of its zips (mod, Pack, source). It
expects the game in `..\game` and BepInEx 5.4.23.5 unpacked in `vendor\BepInEx-5.4.23.5\` (from
`BepInEx_win_x64_5.4.23.5.zip` on the BepInEx GitHub releases):

```powershell
.\package.ps1
```

## Installing without a Pack

Install BepInEx 5 x64 (5.4.23.5 tested) into the game folder, then extract the mod zips there too. It Has My Face is
a Mono game (Unity 6000.0.58), so BepInEx 5 is the right line, not BepInEx 6/IL2CPP. Once BepInEx is in, any of
these mod zips can be added on its own.

BepInEx is LGPL-2.1 licensed.
