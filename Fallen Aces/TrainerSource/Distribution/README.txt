Fallen Aces Trainer 1.0.0
=========================
Puts the game's cheats behind hotkeys and an on-screen panel instead of
typed console commands. Built against Fallen Aces v0.9.1.

USING IT
--------
Insert opens and closes the trainer overlay. Inside it:

  Up / Down     move the cursor
  Right         switch on, run, or step the value up
  Left          switch off, or step the value down
  Insert        close

Four cheats also have direct keys that work with the overlay closed:

  F5    God mode
  F7    Infinite ammo
  F10   Heal to full
  F11   Noclip

With the overlay closed, a small panel in the bottom-left lists whatever is
switched on, and disappears when nothing is.

WHAT IS IN IT
-------------
Survival   God mode, Infinite stamina, Invisible to enemies, Heal to full
Combat     Infinite ammo, One-hit kills, Super punch, Roundhouse kick,
           Neutralise all enemies
Movement   Noclip, Mark position, Recall position
World      Game speed (0.1x-4x), Infinite lighter, Add $500

Notes on a few of them:

  God mode      Falling out of the world still kills you, the same rule the
                game's own invulnerability uses. Surviving it would leave you
                stranded under the level.
  One-hit kills Applies to everything except the player. Enemies with divided
                health bars lose one division per hit, because the game clamps
                damage at each division boundary.
  Noclip        Jump and crouch move up and down; sprint goes faster. The game
                refuses it while dead or in a vehicle, and the trainer says so.
  Game speed    Goes through the game's own time manager, so the pause menu
                still stops time and unpauses back to your setting.

IT DOES NOT WRITE CHEATS INTO YOUR SAVES
----------------------------------------
The game's built-in "god" console command sets a field that is stored in save
files, so quicksaving with it on leaves the save invulnerable. This trainer
intercepts damage instead and never touches that field. Every other toggle uses
a value the game does not serialise.

Toggles are re-applied automatically after a level load or save load.

CONFIGURATION
-------------
BepInEx\config\local.codex.fallenacestrainer.cfg, created on first launch.
Every hotkey is rebindable; set one to None to unbind it. Display options cover
the overlay scale and whether the bottom-left active list is shown.

KEYS THAT ARE ALREADY TAKEN
---------------------------
The game uses 1-5, WASD, backquote, CapsLock, Ctrl, E, Enter, =, Esc, F,
F1-F4, F6, F9, G, H, -, Q, R, Shift, Space, Tab, X and Z. If you also run
SuperHot it takes ], Kill Tracker takes F8, and Expanded Inventory takes 6-0.
The trainer's defaults avoid all of these.

INTERACTION WITH OTHER MODS
---------------------------
Setting game speed away from 1.00x makes the SuperHot mod disengage on its own,
because it only drives the clock while the timescale is 1. Set it back to 1.00x
to hand the clock back.

The trainer hides itself and ignores its own keys while a game menu or the
built-in developer console is open.

VERIFICATION
------------
A startup self-check confirms the damage patch installed, that the game APIs
each cheat relies on still exist, and that no default hotkey collides with the
game or a sibling mod.

That check is startup only. Gameplay behaviour and the overlay have not been
tested in a running level. Treat this release as needing a manual pass.

Only one copy of FallenAces.Trainer.dll should be in BepInEx\plugins.
