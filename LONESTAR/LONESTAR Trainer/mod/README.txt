LONESTAR Trainer
================

INSTALL
  Put this LonestarTrainer folder into:
    %USERPROFILE%\AppData\LocalLow\Shuxi\LONESTAR\Mods\Local\
  so that ...\Mods\Local\LonestarTrainer\mod.json exists.

  Start the game, open Mods, switch "LONESTAR Trainer" on, then restart the game.

  No BepInEx and no other mod loader is needed. LONESTAR has its own C# mod
  loader with Harmony built in.

HOTKEYS
  F1   God Mode - your ship takes no damage in battle
  F2   One-Hit Kill - any hit destroys the enemy ship
  F3   Infinite Star Coins - refills to the cap when you run low
  F4   No Hull Loss Outside Battle
  F5   Repair hull to full
  F6   Win the current battle
  F7   +1 cargo slot
  F8   +250 Star Coins
  F9   +1 move
  F11  Show/hide the overlay

  F1-F4 are toggles and are saved. F5-F9 fire once.

SETTINGS
  The gear icon on the mod's row in the Mods menu opens its settings: the
  toggles above, bonus battle energy, and the built-in developer console.

DEVELOPER CONSOLE
  Optional, off by default, needs a game restart after enabling. Press Enter
  in game to open it, Esc to close.

  WARNING: some of its commands (unlock, resetachi, resettutorial,
  resettreasureunlock, recoverdata, clearsteamachi) rewrite or wipe your
  permanent save with no undo. Back up
  %USERPROFILE%\AppData\LocalLow\Shuxi\LONESTAR\Save first.

KNOWN LIMITS
  Damage previews ignore One-Hit Kill - the preview is computed on a separate
  code path. The real hit still kills.
