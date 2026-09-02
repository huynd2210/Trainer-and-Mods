CULTIC Hotkey Trainer v1.2.0
============================
A small BepInEx trainer for CULTIC with configurable keyboard shortcuts for
common single-player conveniences.

Features and default hotkeys
----------------------------
  F1  Toggle no damage. Enabling it also refills health once.
  F2  Refill health once.
  F3  Add 100 rounds to the current weapon's reserve and fill its magazine.
      Temporary weapons and charge-based weapons are supported too.
  F4  Top up every armor slot and the currently equipped shield.
  End Unlock every weapon and usable equipment item, then refill reserve ammo,
      magazines, charges, the Field Kit, and the Pocket Lamp. Mission keys and
      story items are deliberately excluded.
  PgDn Complete the current level using CULTIC's normal results and progression
       flow. This action is available only during an offline level.

The no-damage toggle applies only to the local player. Status messages appear
briefly in the upper-left corner.

Install
-------
This game copy already includes BepInEx. Copy CulticTrainerPlugin.dll to:

  CULTIC\BepInEx\plugins\CulticTrainerPlugin.dll

Restart CULTIC after replacing the DLL.

Configure
---------
BepInEx creates this file after the first launch:

  BepInEx\config\local.codex.cultictrainer.cfg

Edit the six entries under [Hotkeys] while the game is closed. Any valid
BepInEx KeyboardShortcut value can be used.

F1 / developer-console compatibility
------------------------------------
CULTIC also binds F1 to its internal developer console. The trainer suppresses
that console when one of its hotkeys is pressed. Version 1.0.1 also releases
the console-owned gameplay state, preventing the invisible input lock/freeze
that occurred in version 1.0.0. Version 1.0.3 refreshes CULTIC's actual ammo
counter after F3, covers magazine, temporary, and charge weapons, and uses the
actively equipped loadout entry even when CULTIC's ownership flag is stale.
Version 1.1.0 adds the End-key all-gear action. It is safe to press repeatedly
and does not grant level keys or other progression-sensitive quest items.
Version 1.2.0 adds the Page Down complete-level action as a workaround for maps
whose normal exit cannot be triggered.

Build
-----
Compile CulticTrainerPlugin.cs as a .NET Framework library and reference:

  BepInEx\core\BepInEx.dll
  BepInEx\core\0Harmony.dll
  CULTIC_Data\Managed\Assembly-CSharp.dll
  CULTIC_Data\Managed\netstandard.dll
  CULTIC_Data\Managed\Unity.Netcode.Runtime.dll
  CULTIC_Data\Managed\UnityEngine.dll
  CULTIC_Data\Managed\UnityEngine.CoreModule.dll
  CULTIC_Data\Managed\UnityEngine.IMGUIModule.dll
  CULTIC_Data\Managed\UnityEngine.InputLegacyModule.dll

The resulting assembly must be named CulticTrainerPlugin.dll. Running
package.ps1 builds and installs it, then creates CulticTrainer.zip (mod only)
and CulticTrainer-Pack.zip (BepInEx bundled) under release\.
