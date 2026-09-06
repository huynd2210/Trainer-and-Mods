# Fallen Aces Enhanced Throwing

Installed separately as `BepInEx/plugins/FallenAces.EnhancedThrowing.dll`.

Defaults: 3x player throw launch speed and 9x projectile travel-distance limit. Actual range depends on aim, gravity and obstacles. Props and special thrown props use the native throw event; carried bodies receive 3x native launch force. Damage, stun, impact-force values for props and gravity are untouched. Existing strength buffs still apply normally. Guns and enemy throws are unaffected.

The existing dashed prop trajectory uses the held prop's overridden projectile definition when present, otherwise the generic thrown-prop definition. It applies the same speed/distance values as the actual projectile and simulates up to five seconds. It retains the game's collision checks, visual hand offset and dashed renderer. Native carried-body physics use a different path; this mod does not add a new body-specific trajectory indicator.

Configuration: `BepInEx/config/local.codex.fallenacesthrowenhancement.cfg`. `SpeedMultiplier` defaults to 3 (range 1–10); `DistanceMultiplier` defaults to 9 (range 1–30). Restart after editing.

Run `./ThrowEnhancementSource/build.ps1` to rebuild/install or add `-NoInstall` to build only. Close the game before replacing DLLs. Remove only this mod's DLL to uninstall. No save changes are required.

Verification: compiled against this installation; all Harmony patches initialized alongside the other four gameplay mods. Isolated headless tests exercised native Fire over 50 throw resets, verified speed/distance, unchanged damage/stun/impact/gravity, no repeated-Fire multiplication and unchanged enemy projectile speed. Body patches passed initialization. Actual body flight, real collisions and the visible preview require gameplay verification.

Manual check: enable the game's projectile trajectory option. Aim a bottle or other prop toward an unobstructed floor, note the dashed arc/landing marker, then throw. Repeat with a heavy prop and a special throwable, with and without a strength boost if available. Confirm faster/farther travel and that the line agrees with the flight. Try throwing a carried body too. Report item type, strength-buff state and any mismatch.
