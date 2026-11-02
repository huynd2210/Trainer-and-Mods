# Gun Spawner (Paint the Town Red)

Puts guns into any level. Paint the Town Red already ships 15 guns, but you mostly only find them in Beneath
and the level editor. This mod spawns them on demand through the game's own weapon spawner, so ammo, damage,
breaking and pickup all work the way the game designed them.

## Controls

| Key | Does |
| --- | --- |
| **F7** | Drops the next gun where you're aiming (or on the floor ~3 m ahead) and shows its name |
| **Shift + F7** | Drops the previous gun instead |

Pick a gun up with the game's normal pickup key (E).

Guns, in order: AK47, M4, MP5, Pistol 1911, Auto Revolver, Revolver, Pirate Pistol, Shotgun, Double Shotgun,
Nailgun, Ranged Taser, Crossbow, Triple Crossbow, Speargun, Harpoon Gun.

## Settings

`BepInEx\config\com.pttr.gunspawner.cfg` (created on first launch):

- `SpawnKey`: the key (default `F7`)
- `Distance`: how far ahead the gun lands, in metres (default `3`)
- `Guns`: which guns to cycle through, and in what order (paths under `NetObjects/Weapons/`)

## Install

- **No BepInEx yet:** extract `GunSpawner-Pack.zip` into the game folder (the one with `PaintTheTownRed.exe`).
- **BepInEx 6 IL2CPP already installed:** extract `GunSpawner.zip` into the game folder.

The first launch with BepInEx takes a minute or two longer while it generates its interop files.

Requires BepInEx 6 (IL2CPP, bleeding-edge build 781 tested). Tested on the 2025-07-26 build (Unity 2020.3.39).

## Building

`dotnet build -c Release` with the game at `..\..\game\` and BepInEx installed and run once (needs
`BepInEx\interop`). `dotnet build -c SelfTest` adds a harness that loads a level, spawns every gun, picks one
up, pulls the trigger, logs the results to `LogOutput.log` and screenshots to `BepInEx\selftest\`, then
quits. Set `PTTR_SELFTEST_LEVEL` to choose the level (default `BikerBar`).
