# Fallen Aces Maximum Gun Accuracy

Installed separately as `BepInEx/plugins/FallenAces.MaximumGunAccuracy.dll`.

Player-fired bullets and nails travel along the player's head aim with zero crosshair deviation and zero projectile spread, including all shotgun pellets. The firearm reticle bloom is cleared. This does not auto-aim or remove weapon animations. Enemy accuracy, projectile count, damage values, rate of fire and bullet speed are unchanged. Concentrated shotgun pellets can naturally deliver more damage to one target.

Requires this game's BepInEx 5 installation. Run `./GunAccuracySource/build.ps1` to rebuild and install, or add `-NoInstall` to build only. Close the game before replacing DLLs. Remove this DLL to uninstall; no save changes are required.

Verification: compiled against the installed game; Harmony startup passed alongside Expanded Inventory, Enhanced Throwing, Kill Tracker and SuperHot. An isolated headless test exercised the patched native Fire method for 200 bullets/nails with zero spread and verified enemy spread remains. The harness suppressed collision/hitscan side effects; it did not test real target impacts or visually assess the reticle.

Manual check: fire while standing, moving and firing repeatedly at a wall; confirm shots track the crosshair. Try a shotgun too. Report unexpected spread or reticle behavior.
