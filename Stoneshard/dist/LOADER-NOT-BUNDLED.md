# No ModShardLauncher in this folder

`Mercenaries.zip` is the mod only (`Mods\Mercenaries.sml`). It needs ModShardLauncher (MSL) 0.13.2,
which patches the game's `data.win`. A self-contained build, MSL 0.13.2 + the mod (~88 MB), exists
as `Mercenaries-Pack.zip`. It is **not** in this repo, because no mod loader is bundled anywhere
here (see `INVENTORY.md`).

## Rebuilding the Pack zip

1. Get ModShardLauncher 0.13.2 (the folder holding `ModShardLauncher.exe` and `ModShardLauncher.dll`).
2. Extract `Mercenaries-Source.zip` into its `ModSources\` folder, giving
   `ModSources\Mercenaries\` and `ModSources\_tools\mslpack\`.
3. With the .NET 8 SDK installed:

```powershell
powershell -ExecutionPolicy Bypass -File .\ModShardLauncher\ModSources\Mercenaries\package.ps1
```

It packs `Mercenaries.sml` (also copied into MSL's `Mods\`), then writes `Mercenaries.zip`,
`Mercenaries-Pack.zip` and `Mercenaries-Source.zip` to `<folder above MSL>\_release\Mercenaries\`.
The Pack is the MSL folder without its `ModSources\` and other mods, plus this mod and an `INSTALL.txt`.
