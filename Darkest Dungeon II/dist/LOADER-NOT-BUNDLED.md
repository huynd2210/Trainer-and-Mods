# No BepInEx loader in this folder

There is a `DD2.KillTracker-Pack.zip`: BepInEx 5.4.23.5 (x64) plus the mod, self-contained, so
there is no separate loader install to get wrong. It is **not** in this repo, because of the rule
in `INVENTORY.md` that no mod loader is bundled anywhere here.

## Rebuilding it

`src/package.ps1` builds all three zips (mod, Pack, source). It expects
`BepInEx_win_x64_5.4.23.5.zip` from the BepInEx GitHub releases in `vendor\`:

```powershell
.\package.ps1 -GameDir "C:\Program Files (x86)\Steam\steamapps\common\Darkest Dungeon® II\"
```

## Installing without the Pack

Install BepInEx 5 x64 (5.4.23.5 tested) into the game folder, then extract
`DD2.KillTracker.zip` there too. Darkest Dungeon II is a Mono game, so BepInEx 5 is the right
line, not BepInEx 6/IL2CPP.

BepInEx is LGPL-2.1 licensed.
