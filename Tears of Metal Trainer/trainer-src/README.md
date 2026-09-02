# Tears of Metal Trainer

Local Windows save trainer for the Unity IL2CPP build of Tears of Metal (save build 56935).

## Features

- Edit Coins, Crosses, Statues, Gems, Scrolls, and Meteors
- Block the resolved-hit HP subtraction inside `PlayerDamageReceiver.ReceiveHit` while the game is running
- One-click `Max all` preset
- Automatic timestamped backup before every edit
- Manual backup and validated restore
- Detects the game process and blocks unsafe writes while the game is running
- Launches `ToM.exe` from the trainer

## Use

1. Run `Tears of Metal Trainer.exe`.
2. For save edits, close the game, enter values, and click `Apply changes`.
3. For no-damage mode, launch the game and click `Enable no damage`.

No-damage mode is session-only. It leaves hit reactions, healing, normal HP initialization, and the HUD untouched. The original HP-subtraction instruction is restored when you click the button again or close the trainer. Use live cheats only in offline/local play.

Backups are written beside the normal save:

`%USERPROFILE%\AppData\LocalLow\Paper Cult\Tears of Metal`

The trainer never modifies the game files on disk and does not bypass online/anti-cheat systems.
