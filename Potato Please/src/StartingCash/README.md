# Starting Cash (Potato Please)

Press **F8** in a game to get ₽10,000. Press it again for more.

The money goes straight onto your balance. It doesn't count as earnings in your stats. Pressing F8 on the
main menu does nothing; load a save first.

## Settings

`BepInEx\config\dev.potatoplease.startingcash.cfg`, written on first launch:

| Setting | Default | Meaning |
| --- | --- | --- |
| `Hotkey` | `F8` | The key. Unity Input System key names, e.g. `F8`, `F9`, `Insert`, `Backquote`. |
| `Amount` | `10000` | Rubles added per press. |

## Install

- **BepInEx 6 IL2CPP already installed:** extract `StartingCash.zip` into the game folder.
- **Clean game:** extract `StartingCash-Pack.zip` into the game folder (BepInEx included).
  The first launch takes a minute or two longer while BepInEx sets itself up.

Uninstall: delete `BepInEx\plugins\StartingCash.dll`. Money already added stays in your saves.

## Requirements

Potato Please (Unity 6000.3, IL2CPP, win-x64) with BepInEx 6.0.0-be.781 IL2CPP x64.

## Building

`build.ps1` builds against the interop assemblies BepInEx generates in the game folder
(`..\..\Potato Please\BepInEx\interop`) and copies the DLL into `BepInEx\plugins`;
`package.ps1` then makes the mod, Pack and source zips in `dist\`.
