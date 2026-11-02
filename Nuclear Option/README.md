# Nuclear Option mods

BepInEx 5 mods for Nuclear Option 0.34.1. BepInEx is not included; see `dist/LOADER-NOT-BUNDLED.md`.

The flat `AutoMissileDefense-*.zip` files beside this README predate the `dist/`/`src/` layout.

## Manual Missile Control 1.0.0

Fly your own missiles by hand from a third-person chase camera. Fire a missile (AGM, AAM,
cruise missile, glide bomb), press **F6**, and steer. The mouse/view axes move the
commanded direction like cockpit free-look. Stick or keys (Pitch, Roll/Yaw) swing it at a
set rate and follow your flight bindings. A cross marks where you are sending the missile and a
diamond marks where its current path meets the ground. On impact the view returns to the
cockpit after 2 s. F6 in flight hands the missile back to its own seeker.

While you steer, your aircraft holds neutral stick and its throttle, and ignores fire, gear and
eject. A seeker cannot self-destruct a missile you are steering; impacts and proximity fuses
still detonate it. Single player or multiplayer host only.

**Install:** with the game closed and BepInEx 5 installed, extract `dist/MissileControl.zip`
beside `NuclearOption.exe`. Settings are in `BepInEx/config/nuclearoption.missilecontrol.cfg`.
`src/MissileControl/README.md` has the full controls and settings.

**Status:** compiles against the 0.34.1 assemblies, the 19 steering-math tests pass, and every
patched game method and field was confirmed in the 0.34.1 code. **It has not been run in the
game yet:** loading, camera, steering feel and impacts are untested.

**Source:** `src/MissileControl` (also `dist/MissileControl-Source.zip`). To build, place it at
`ModSource/MissileControl` inside the game folder and run `build.ps1 -Install`.
