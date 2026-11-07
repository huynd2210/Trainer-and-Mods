# No BepInEx loader in this folder

Each mod here also has a `-Pack.zip` (33 MB): BepInEx 6.0.0-be.781 IL2CPP x64, with its `dotnet` runtime and
Doorstop, plus that mod (`SteadyHands-Pack.zip`, `StartingCash-Pack.zip`). They are self-contained, so there is no
separate loader install to get wrong. They are **not** in this repo, because of the rule in `INVENTORY.md` that no
mod loader is bundled anywhere here.

## Rebuilding them

`src/<Mod>/package.ps1` builds all three zips (mod, Pack, source) into `dist\` next to it. It expects the
workspace at `<game root>\mods\<Mod>` with the game in `..\..\Potato Please`, BepInEx already installed there
and run once so `BepInEx\interop` exists (run `build.ps1` first), and a clean BepInEx copy in
`..\vendor\BepInEx-be781-IL2CPP-x64` (`BepInEx\core`, `dotnet\`, `winhttp.dll`, `doorstop_config.ini`,
`.doorstop_version`; no interop, cache, config or logs).

```powershell
.\build.ps1
.\package.ps1
```

## Installing without a Pack

Install BepInEx 6 IL2CPP x64 into the game folder (be.781 tested; get it from builds.bepinex.dev), start the game
once so BepInEx generates its interop assemblies, then extract the mod zip there too. Potato Please is an
IL2CPP game (Unity 6000.3), so it needs the BepInEx 6 IL2CPP line, not BepInEx 5.

BepInEx is LGPL-2.1 licensed.
