# No BepInEx in this folder

`MissileControl.zip` is the mod only. It needs BepInEx 5 (x64, Mono) already installed in
Nuclear Option. A self-contained build, BepInEx 5.4.23.5 + Doorstop + the mod (~0.6 MB),
exists as `MissileControl-1.0.0-NO0.34.1-Dist.zip`. It is **not** in this repo,
because no mod loader is bundled anywhere here (see `INVENTORY.md`).

## Rebuilding the Dist zip

Extract `MissileControl-Source.zip` into the game folder so that `ModSource\MissileControl`
sits beside `NuclearOption_Data` and `BepInEx`. Then, with BepInEx installed, the game closed
and .NET 8 available:

```powershell
powershell -ExecutionPolicy Bypass -File .\ModSource\MissileControl\package.ps1
```

This builds against the installed game, runs the steering-math tests and writes the Mod,
Dist and Source zips to `release\independent\MissileControl`. Dist takes BepInEx from the
game's own `BepInEx\core`, `winhttp.dll` and `doorstop_config.ini`. The license texts come
from `ModSource\Packaging\licenses`, which is in the workspace and not in this repo.
Without that folder, Dist is built without them.

The `INSTALL.txt` inside the zips calls them `Mod.zip`/`Dist.zip`. Here they are published as
`MissileControl.zip`/`MissileControl-Source.zip`.
