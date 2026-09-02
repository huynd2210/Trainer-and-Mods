CULTIC God Mode v1.0.1
======================
A developer-style BepInEx mod for CULTIC. This is more than an invulnerability
switch: it adds flight/no-clip, key bypass, world freeze, super speed, infinite
stamina/ability energy, and a full restock command.

Default hotkeys
---------------
  Insert Toggle the whole God Mode suite.
  F10   Toggle flight/no-clip (also enables God Mode if it was off).
  F11   Freeze/unfreeze the world (also enables God Mode).
  F12   Toggle 3x movement speed (also enables God Mode).
  Home  Refill health, armor, energy, all ammo reserves, and magazines.

While flying, use the normal movement controls, Space to rise, Left Ctrl to
descend, and hold the normal Run control for a 3x flight sprint.

Core God Mode behavior
----------------------
While Insert is on:
  - damage to the local player is blocked;
  - stamina and ability energy stay full;
  - locked doors unlock and open on the first interaction;
  - keypads, rolling-door panels, and other key-gated map mechanisms work
    without consuming or possessing their key.

Doors marked "stuck" and doors blocked by a physical/scripted blocker are not
forced. Those are progression or set-piece states rather than key locks. Flight
can still be used to inspect the other side.

Safety and compatibility
------------------------
  - God Mode only changes gameplay in offline/single-player sessions. It stands
    down in multiplayer, menus, cutscenes, death, and level transitions.
  - Flight restores the player's collider, physics, and previous state when it
    is switched off or gameplay is interrupted.
  - Freeze uses a 0.01 time scale instead of literal zero so CULTIC code that
    divides by delta time stays safe. Camera look and no-clip flight remain
    responsive. The freeze is applied after other LateUpdate time mods, so it
    can coexist with the included SuperHot plugin.
  - Defaults avoid the existing mods' F1-F4, F6-F9, M, and ] bindings.

Configure
---------
BepInEx creates this file after the first launch:

  BepInEx\config\local.codex.culticgodmode.cfg

All hotkeys, the speed multiplier, flight speed, freeze scale, startup state,
and HUD readout are configurable there. Edit the file while the game is closed.

Install
-------
This game copy already includes BepInEx. The built DLL is installed at:

  BepInEx\plugins\GodModePlugin.dll

Restart CULTIC after replacing the DLL.

Build
-----
Run package.ps1 from this folder. It compiles with the installed .NET Framework
C# compiler, installs the DLL, and creates release\GodMode.zip (mod only) plus
release\GodMode-Pack.zip (BepInEx bundled).
