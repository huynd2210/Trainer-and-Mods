# Sephiria trainer (v1.0.33)

With a save loaded, press **F6** to add 1,000 saphires to the active profile or **F7** to add 10,000 gold to the current player. Each press adds the stated amount again. Saphires are saved immediately. Gold belongs to the current run and follows the game's normal money handling.

The installer backs up `Sephiria_Data/Managed/Assembly-CSharp.dll` as `Assembly-CSharp.dll.trainer-original`, installs `SephiriaTrainer.dll`, and adds one call to the player's update loop. It is intended for this v1.0.33 build. Run `uninstall.ps1` with the game closed to restore the original assembly.
