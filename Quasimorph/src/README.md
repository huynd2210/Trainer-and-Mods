# Quasimorph mods — source

Three BepInEx 5 mods for Quasimorph v1.0 (Unity 2022.3.62f2, Mono x64).

| Mod | What it does | Hotkey |
|---|---|---|
| [QM.WarpDrive](QM.WarpDrive/README.md) | Ship travel speed — separately controls the real-time flight animation and the in-game hours a trip costs | **F9** toggle |
| [QM.Spawner](QM.Spawner/README.md) | Search the item catalogue and spawn anything into the ship's cargo | **F10** window |
| [QM.KillTracker](QM.KillTracker/README.md) | Counts every kill by unit type, faction, and the game's creature classes, across the current raid, campaign, and all time | **F11** window |

## Layout

```
ModSource/
  Directory.Build.props   shared build setup (compiles against the game's own assemblies)
  DESIGN.md               verified notes on the game APIs the mods rely on
  package.ps1             builds every mod and produces the distributable zips
  lib/                    BepInEx redistributable used by the -Pack zips
  QM.WarpDrive/
  QM.Spawner/
  QM.KillTracker/
  dist/                   packaging output
```

## Building

```bash
dotnet build QM.WarpDrive/QM.WarpDrive.csproj -c Release
```

No NuGet packages and no internet needed: the shared props file references the Mono BCL, Unity, and
`Assembly-CSharp` straight out of `Quasimorph_Data/Managed`, so the compile-time API surface is
exactly what the game loads at runtime. If your game lives somewhere else, pass `-p:GameDir=...`.

Drop the resulting DLL into `BepInEx/plugins/` to test, or run `package.ps1` for release zips.

## Packaging

```powershell
.\package.ps1
```

Produces in `dist/`:

- `QM.WarpDrive.zip`, `QM.Spawner.zip`, `QM.KillTracker.zip` — the mod alone, for people who already run BepInEx
- `QM.WarpDrive-Pack.zip`, `QM.Spawner-Pack.zip`, `QM.KillTracker-Pack.zip` — BepInEx bundled with that one mod, self-contained
- `QuasimorphMods-Bundle.zip` — BepInEx plus all three mods

All of them extract directly into the game folder.
