CULTIC Replay v1.4.0
====================

A standalone BepInEx plugin that records your gameplay and plays it back at
normal speed through ghost actors - pair it with the SuperHot mod and your
frozen-time dodges look like superhuman action-hero moves when replayed.

WHAT'S REPLAYED
---------------
  - Every actor as sprite ghosts (enemies, corpses, gibs).
  - Player/enemy bullets using CULTIC's native matBulletTracer material,
    original player/enemy widths, recorded line geometry and 3D trajectory.
  - Thrown axes, TNT, Molotovs, gas grenades, Molomites, nail jars,
    grenade shells, impact projectiles, spit, flares, nails and pitchforks.
  - Your first-person weapon (the arm/weapon UI is recorded and replayed).
  - Sound: gunshots, hits, footsteps and everything else the game played
    through AudioSource, re-played at their recorded world positions.
  - Your exact camera movement, from your recorded point of view.
  - Sprite gore: flying heads, limbs, organs and other scrGib debris.
  - Sprite-based barrels, crates and breakable scenery, plus CULTIC's native
    explosion particle/light prefabs as presentation-only replay effects.

SUPERHOT PACING
---------------
By default the replay neutralizes SuperHot pacing by replaying the recording on
its scaled game-time timeline. Frozen wall-clock stretches disappear completely,
and actors, projectiles, camera, weapon and audio all use that same clock. Set:
  [Playback] CompressFrozenTime = false
to preserve the original slow-motion wall-clock pacing instead.

HOW IT WORKS
------------
While you play, the mod records the current gameplay take from its beginning by
default. CULTIC renders every character as a sprite billboard, so the mod
records exactly what you see: every actor's position, facing, sprite frame,
and every enemy projectile. On playback it reloads the map, removes the live
AI, and re-renders your run through ghost sprites - at full speed.

RECORDING
---------
  - Recording is automatic whenever you are in offline gameplay.
  - F9 (configurable) saves the current take as a replay file.
  - Dying also auto-saves a replay of your final moments (configurable).
  - Set RecordWholeTake = false to use the old rolling BufferSeconds window.

  Note: the default is F9 because several other common CULTIC mods use F5-F8.

WATCHING
--------
  Main menu -> REPLAYS -> pick a file -> BACK / click a row to play.
  The map reloads and your run plays out from your recorded first-person
  point of view - camera movement, aiming, everything. ESC exits back to
  the main menu.

  Playback speed is configurable (BepInEx\config\local.codex.culticreplay.cfg,
  [Playback] Speed) - 1.0 is real time; try 0.5 for extra drama.

FILES
-----
  Replays are saved to BepInEx\replays\*.cshrep. Delete them there to remove
  them from the menu. The menu lists the newest 12.

CONFIG (BepInEx\config\local.codex.culticreplay.cfg)
----------------------------------------------------
  [Recording]
  RecordWholeTake = true   record from the beginning of the current take
  BufferSeconds   = 120    rolling window when RecordWholeTake is false (15-600)
  SampleRate      = 30     samples per second (10-60)
  AutoSaveOnDeath = true   save a replay automatically when you die

  [Hotkeys]
  SaveReplay      = F9     save the buffer in-game

  [Playback]
  Speed              = 1      playback speed multiplier
  HideHud            = true   hide the in-game HUD during playback
  DisablePostFx      = true   park the camera's retro post effects while playing
  CompressFrozenTime = true   remove SuperHot slow-motion from playback
  PlayRecordedAudio  = true   replay captured sounds
  ShowWeapon         = true   replay the recorded first-person weapon

NOTES & LIMITATIONS
-------------------
  - Offline/single-player only (like SuperHot).
  - Replays capture actor sprites, known projectile/throwable classes, sprite
    gibs, audio and the first-person weapon image. The weapon is stored as its
    final normalized screen-space quad, so nested arm-canvas offsets are kept.
    Native scrExplosion effects are replayed, but arbitrary free-running
    particle systems and mesh blood decals (blood mist, smoke, fire pools) are
    not reconstructed. Sprite-less non-bullet projectiles use a visible tracer
    approximation.
  - Replay format v1/v2 files remain readable, but recordings made before the
    projectile capture fix cannot recover projectile samples that were never
    stored. Make a fresh recording with v1.3.0 or newer for native bullets, corrected
    weapon placement and gore. Version 4 recordings are required for native
    explosions and recorded destructible scenery.
  - The stage during playback is still a freshly loaded map for doors and
    non-sprite world state; sprite destructibles are replaced by their recorded
    states.
  - Very long recordings grow the file quickly at high sample rates;
    defaults give roughly 1-3 MB per minute.
  - Works fine without the SuperHot mod too - replays just look normal-speed
    because they are.

  This mod records and plays back locally; it is not affiliated with or
  endorsed by SUPERHOT Team or Jasozzogames.
