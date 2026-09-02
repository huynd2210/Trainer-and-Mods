CULTIC SuperHot v1.3.2
======================

A BepInEx plugin that gives CULTIC the "Super Hot" time mechanic:
game time only moves while you are doing something.

While you move, fire, reload, switch weapons, interact, jump, crouch, or hold
just about any action button, the world runs at full speed. The instant you
stop, the world slows to a near-stop (default 2% speed) - bullets hang in the
air, enemies freeze, and your own reload stalls until you move again.

LOOKING AROUND AND AIMING ARE NOT ACTIONS. Turning, aiming (holding right mouse)
and studying a frozen scene cost you nothing and do not advance time; only
committing to a move does. You can hold right-click to line up iron sights in a
frozen world - time only restarts when you move, fire, reload, or act.

  v1.3.0: right mouse button no longer counts as an action, so aiming no longer
  wakes the world (previously holding RMB kept time at full speed).

Turning stays as responsive as it is at normal speed. The game eases the camera
onto your aim point at a rate tied to game time, so with time frozen the camera
would otherwise drift after the mouse for a second or more - sluggish and
sickening. The mod re-applies that easing at real-time rate whenever it is the
thing holding time down, so a frozen turn tracks the mouse exactly like an
unfrozen one.

Enemy projectiles are extra-dodgeable:
  - While time is frozen they travel far slower than everything else (an
    extra multiplier on top of the freeze), so nothing sneaks up on you.
  - When you start acting again they ramp back up to full speed over a short
    grace period instead of snapping straight back, giving you a window to
    sidestep.

INSTALL (pick one)
------------------

  Option A - you already run CULTIC with BepInEx (the game folder has a
  "BepInEx" folder and winhttp.dll):
    Extract SuperHot.zip into the CULTIC game folder. It adds
    BepInEx\plugins\SuperHotPlugin.dll.

  Option B - fresh / self-contained (no separate BepInEx install needed):
    Extract SuperHot-Pack.zip into the CULTIC game folder. It brings
    BepInEx + the mod and the Doorstop bootstrap.

Then just launch CULTIC.exe. The mod is ON by default.

TOGGLE
------
  ] (RightBracket, default) toggles SuperHot on/off at any time. A short
  "[SuperHot] ON/OFF"
  message shows in the top-left corner.

CONFIG
------
  Edit BepInEx\config\local.codex.culticsuperhot.cfg (created on first run):

    FrozenTimeScale  = 0.02   time scale while idle. 0.01 already reads as
                              frozen; lower is more frozen. The game's own code
                              divides by Time.deltaTime, so 0 is not allowed.
    ActionGrace      = 0.35   seconds of real time that keep flowing after a
                              button press, so single-shot actions (weapon
                              switch / interact / jump) play out.
    EnabledOnStart   = true   start with the mod on.

    [Projectiles]
    SlowFactor       = 0.15   extra speed multiplier applied to enemy projectiles
                              while time is frozen (on top of the global freeze).
                              Lower = slower. Set to 1 to disable this part.
    RampUpTime       = 1.5    seconds of real time for enemy projectiles to ramp
                              back up to full speed once you start acting again.
                              This is the dodge window - raise it to make it easier.

  You can also rebind ToggleSuperHot under the [Hotkeys] section.

NOTES & LIMITATIONS
-------------------
  - Applies to offline / single-player only. In online play the mod does
    nothing (freezing local time would desync everyone else).
  - Pause menu, cutscenes, death, level-exit and load screens are left
    untouched - the game owns time there.
  - The weapon wheel keeps its built-in slow-motion feel.
  - While frozen the game's own audio slow-mo compensation plays, so the
    world sounds slowed/quieter rather than cutting out.
  - Camera ROTATION is kept responsive while frozen; the weapon-in-hand sway
    and camera bob are not. Their smoothing is time-scaled too, so the gun
    drifts back to centre slowly after a frozen turn. That one is left alone on
    purpose: the game writes those positions from ~29 different places,
    including instant snaps for weapon switches, and rescaling them all would
    break far more than it fixed.
  - Enemy projectiles covered: standard bullets (scrBullet), infected spit,
    vomit, incinerator fire particles, Archon energy waves and blood balls,
    and helicopter rockets.
  - This is a "mod" in the Super Hot spirit - it is not affiliated with or
    endorsed by SUPERHOT Team.
