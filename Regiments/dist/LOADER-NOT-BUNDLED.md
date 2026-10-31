# No BepInEx in this folder

`TheaterCommand.zip`, `WarpathOPs.zip` and `KillTracker.zip` are the mods only. They need BepInEx 5
(x64, Mono) already installed in Regiments. Self-contained builds, BepInEx 5.4.23.2 + the mod
(~0.7 MB each), exist as `TheaterCommand-Pack.zip`, `WarpathOPs-Pack.zip` and `KillTracker-Pack.zip`.
They are **not** in this repo, because no mod loader is bundled anywhere here (see `INVENTORY.md`).

## Rebuilding the Pack zips

Each source zip is a workspace that expects to sit two levels below the game folder's parent:

```
Regiments.v1.5.0\
  game\                      Regiments.exe, Regiments_Data\Managed
  mod\theater-command\       TheaterCommand-Source.zip extracted here
  mod\regiments-warpathops\  WarpathOPs-Source.zip extracted here
  mod\kill-tracker\          KillTracker-Source.zip extracted here
  mod\tools\bepinex\         a BepInEx 5.4.23.2 x64 tree (winhttp.dll, doorstop_config.ini, BepInEx\core)
```

Then, with .NET SDK 8 installed:

```powershell
powershell -ExecutionPolicy Bypass -File .\mod\theater-command\package.ps1
powershell -ExecutionPolicy Bypass -File .\mod\regiments-warpathops\package.ps1
powershell -ExecutionPolicy Bypass -File .\mod\kill-tracker\package.ps1
```

Each builds against the game's assemblies, installs into `game\` if the game is closed, and writes
`<Mod>.zip`, `<Mod>-Pack.zip` and `<Mod>-Source.zip` to its `release\` folder. WarpathOPs also checks
every patched method and field against the installed game before packaging; KillTracker patches the
installed game for real and runs a scripted battle through it.
