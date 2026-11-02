# GnollHack mods

Three cheat mods for your own play, for the Steam version of GnollHack on Windows
(October 2026 build). Each one is a single hotkey:

| Mod | Key | What it does |
|---|---|---|
| Auto-Identify | Ctrl+I | Identifies everything you carry: item types, blessed/cursed status, enchantment, charges |
| Reveal Secrets | Ctrl+E | Finds every secret door and hidden passage on the current level and puts them on your map |
| Map Level | Ctrl+F | Maps the whole current level, traps included |

All three use the game's own code to do the work (its identify-everything, secret-door
detection and level-mapping routines). No game time passes, and no game file is changed.

## Install

1. In Steam, right-click GnollHack, then Manage, then Browse local files.
2. Extract any of `dist\AutoIdentify.zip`, `dist\RevealSecrets.zip` and
   `dist\MapLevel.zip` into that folder. Each one adds its own folder, such as
   `GnollHack\MapLevel\`, next to `GnollHackM.exe`.
3. Close GnollHack, then double-click `Play GnollHack (mods).cmd` in any of those
   folders. It starts the game with every mod installed this way.

Starting the game from Steam loads no mods. If GnollHack is already open, the launcher
says so and stops: save and close the game first. To uninstall, delete the mod's folder.

No mod loader is involved, so there is nothing else to install. Each mod is a .NET
startup hook that the launcher passes to the game through `DOTNET_STARTUP_HOOKS`.

## Layout

- `dist\` holds the mod zips (`<Mod>.zip`) and source zips (`<Mod>-Source.zip`).
- `src\<Mod>\` holds the source. Run `package.ps1` there (.NET 8 SDK) to rebuild the
  zips; each mod's README explains how it works.
