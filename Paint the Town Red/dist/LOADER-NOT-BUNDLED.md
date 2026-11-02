# No BepInEx loader in this folder

Gun Spawner also has a `GunSpawner-Pack.zip` (33 MB): BepInEx 6.0.0-be.781 IL2CPP x64, with its `dotnet` runtime
and Doorstop, plus the mod. It is self-contained, so there is no separate loader install to get wrong. It is **not**
in this repo, because of the rule in `INVENTORY.md` that no mod loader is bundled anywhere here.

## Rebuilding it

`src/GunSpawner/package.ps1` builds all three zips (mod, Pack, source) into `dist\` next to it. It expects the
workspace two levels below the game folder (`<game root>\mods\GunSpawner`, game at `..\..\game`), with BepInEx
already installed there and run once so `BepInEx\interop` exists. The Pack takes BepInEx from that install
(`BepInEx\core`, `dotnet\`, `winhttp.dll`, `doorstop_config.ini`, `.doorstop_version`), leaving out generated and
per-user data (interop, cache, config, logs).

```powershell
.\package.ps1
```

## Installing without a Pack

Install BepInEx 6 IL2CPP x64 into the game folder (be.781 tested; get it from builds.bepinex.dev), start the game
once so BepInEx generates its interop assemblies, then extract `GunSpawner.zip` there too. Paint the Town Red is an
IL2CPP game (Unity 2020.3.39), so it needs the BepInEx 6 IL2CPP line, not BepInEx 5.

BepInEx is LGPL-2.1 licensed.
