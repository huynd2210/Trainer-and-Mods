# Speed Boost

Multiplies the player character's walk/run speed in Cat Mail Co. while leaving the game clock, NPCs, and delivery timers at their normal pace.

The multiplier is applied to the controller's max ground speed (the value all grounded movement and the parcel-weight slowdown are derived from), so walking and sprinting scale together while gravity and jumping stay intact. Air movement and climbing keep their own speeds.

## Controls

- `F6`: cycle the walk-speed multiplier through 1x, 2x, 3x, 5x
- `Left Shift + F6`: show or hide the on-screen speed indicator

The key is configurable in `BepInEx/config/com.catmailco.speedboost.cfg` (any UnityEngine.InputSystem key name, e.g. `F6` or `V`). The overlay position and visibility are configurable too.

## Build

Run `build.ps1` from this directory after BepInEx has generated the IL2CPP interop assemblies.

The installed plugin is `BepInEx/plugins/SpeedBoost/SpeedBoost.dll`. Settings live in `BepInEx/config/com.catmailco.speedboost.cfg`.
