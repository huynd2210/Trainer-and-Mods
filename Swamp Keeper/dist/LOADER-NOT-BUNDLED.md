# No BepInEx loader in this folder

There is a `SwampKeeperCoop-Pack.zip`: BepInEx 6.0.0-be.789 (Windows, IL2CPP, x86), the Unity base libraries for
offline first launch, a `BepInEx.cfg` with the console off, `INSTALL.txt`, and the mods. It is **not** in this repo,
because of the rule in `INVENTORY.md` that no mod loader is bundled anywhere here.

## Rebuilding it

`src/SwampKeeperCoop/package.ps1` builds all three zips (mod, Pack, source) from the sources in a Swamp Keeper v1.05
game folder laid out as `<game>\ModsSource\{SwampKeeperCoop,SwampKeeperMods,tests,tools}`. It expects
`ModsSource\vendor\BepInEx-IL2CPP-x86.zip` (BepInEx 6.0.0-be.789 IL2CPP x86 from builds.bepinex.dev) and a game
that already has BepInEx installed, for the interop assemblies, `BepInEx.cfg` and `unity-libs\2022.3.62.zip`.

```powershell
powershell -File ModsSource\SwampKeeperCoop\package.ps1
```

## Installing without the Pack

Install BepInEx 6.0.0-be.789 IL2CPP **x86** into the game folder and launch once. Then extract
`SwampKeeperCoop.zip` there too. It contains both plugins: co-op and Swamp Keeper Mods 1.2.0, which co-op requires.
The game is 32-bit IL2CPP, so BepInEx 5 and the x64 builds do not work.

BepInEx is LGPL-2.1 licensed.