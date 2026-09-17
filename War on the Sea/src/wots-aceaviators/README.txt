ACE AVIATORS - War on the Sea (v1.09a)
======================================

Your pilots stop throwing the aim solution away. Dive bombers, torpedo bombers and level
bombers aim true - and, if you want it, cannot miss at all.

Enemy aircraft are not affected.


TWO MODES
---------

Both modes remove the game's random aim error. They differ in what happens after the
ordnance leaves the rack.

  AimOnly          (default)
    Remove the random aim error and stop there. The run is aimed true and the ordnance
    then flies on the game's own physics. A target that alters course after the release
    can still get out from under it. Duds still happen.

    This is the honest option: your crews stop missing for no reason, but a destroyer
    that puts the helm over still earns its escape.

  AimAndGuidance
    Also steer released ordnance onto the target for the rest of its flight, and suppress
    the dud roll on a round that lands. Nothing gets away.

Press F6 in game to switch between them, or set Mode in the config file. The choice is
written back to the config, so the mode you leave it in is the mode you get next launch.


WHAT EACH PART DOES
-------------------

Everything here applies only to ordnance released by a PLAYER aircraft.

1. Perfect aim solution.                (both modes)
   The game rolls a random horizontal offset into every attack run: a radius of 70 m for
   a level bombing run, 80 m for an aerial torpedo, and for a dive 30 m plus the target's
   speed plus its rate of turn - so more again against anything fast and manoeuvring.
   That roll is forced to zero, so the approach, the dive and the release point are aimed
   at where the target actually is.

2. Terminal guidance.                   (AimAndGuidance only)
   Once released, the round is steered onto the target for the rest of its flight. Bombs
   and aerial depth charges have their horizontal velocity trimmed each physics tick
   toward the solution that puts them exactly where the target will be when they arrive.
   Aerial torpedoes keep steering during their run instead of swimming dead straight, so
   a target that alters course after the drop no longer combs the track.

   The round is flown physically onto the hull. The hit is a real hit: the game's own
   collision, armour, penetration and damage code runs completely untouched. A bomb that
   lands on a battleship's belt still does what a bomb on a battleship's belt does.

3. No duds.                             (AimAndGuidance only)
   A guided round that reaches its target is not allowed to fail to go off. The dud rate
   is suppressed for that one detonation and restored immediately afterwards, so nothing
   leaks into enemy ordnance. In AimOnly mode the game's own dud rate applies unchanged.


CONTROLS
--------

  F5   toggle the mod on and off
  F6   switch between AimOnly and AimAndGuidance

A small "ACE AVIATORS" line in the top-left corner shows when it is on, which mode is
active, and how many rounds are currently under guidance.

Switching off, or switching to AimOnly, releases anything already in the air - those
rounds finish their flight ballistically.

Both keys, and everything else, are configurable. See CONFIGURATION below.


WHAT IS NOT INCLUDED
--------------------

Naval gunnery. Your ships' guns are completely untouched.

This was a deliberate call, not an oversight. Gun misses in this game are dominated by
the fire-solution and salvo-spotting system rather than by shell dispersion, so zeroing
dispersion would be a half-measure that does not deliver "never miss". It is also unsafe
to do naively: ammunition objects are pooled and handed back after impact, so a shell
permanently stripped of its dispersion follows that pooled object into an enemy ship's
guns on its next use. Doing gunnery properly means engaging the director and spotting
code, which is a separate piece of work.

Rockets. Also deliberate. They are ballistic, so the same guidance would accept them,
but a rocket is fired almost flat: solving its horizontal speed from the time it takes to
fall to deck height demands large speed changes that look wrong on screen. They want a
cross-track-only guidance of their own.

Kamikaze attacks are unaffected, beyond benefitting from the zeroed aim offset.


MANUAL DROPS
------------

Guidance needs to know what the aircraft is attacking. Normally that is the target you
assigned, and nothing more is needed.

If you press the drop button on a bomber with no assigned target, the mod looks for the
nearest enemy ship inside a 60-degree cone ahead of the aircraft, within about 300 m, and
guides onto that. If there is no such ship it guides nothing and the ordnance falls
ballistically, exactly as it does without the mod. It will not invent a target you were
not already flying at.

This only concerns AimAndGuidance. AimOnly never needs to know the target, because it
changes nothing after the release.


CONFIGURATION
-------------

First run writes BepInEx\config\com.wots.aceaviators.cfg. Settings worth knowing:

  Mode                      default AimOnly  - AimOnly or AimAndGuidance
  ToggleKey                 default F5
  ModeKey                   default F6
  EnabledOnStart            default true
  ShowStatusLine            default true
  BombGuidanceAuthority     default 40  - how hard a guided bomb may steer, in world
                                          units per second squared. The game world is
                                          1 unit = 10 metres. Lower looks more like a
                                          plain falling bomb but leaves less room to
                                          correct a bad release.
  TorpedoTurnRate           default 25  - degrees per second a guided torpedo may turn.
                                          Lower reads more like a real torpedo run; too
                                          low and a hard-manoeuvring target can still
                                          slip it.
  SpreadSticksAlongHull     default true - lay a stick of bombs down the length of the
                                          hull instead of stacking every bomb on one
                                          point. Cosmetic; every bomb hits either way.
  VerboseLogging            default false

If you want something between the two modes - guidance that helps but can still be beaten
by a hard turn - stay in AimAndGuidance and lower BombGuidanceAuthority (try 4) and
TorpedoTurnRate (try 3). The ordnance then corrects a sloppy release but cannot fully
chase a target that dodges.


HOW IT WAS CHECKED
------------------

Two harnesses run as part of building the mod, and both must pass before it packages.

  sim\    compiles the actual shipped guidance source - not a copy of it - and flies it
          at a manoeuvring ship, scoring hits against the real hull rectangle. Level
          bombing and the aerial torpedo run are reproduced from the game's own release
          logic: the lead times, standoff distances, release radii and 0.1 s AI tick are
          the literal values in EngagementAI and UnitAIAir. 400 samples per case.

            LEVEL BOMBING from 150 m           vanilla   AimOnly   AimAndGuidance
              light cruiser 180x18, 30 kn turn    18%      hit          hit
              destroyer     110x11, 33 kn turn     8%      miss         hit
              battleship    250x33, 27 kn turn    36%      hit          hit

            AERIAL TORPEDO                     vanilla   AimOnly   AimAndGuidance
              light cruiser 180x18, 25 kn steady 100%      hit          hit
              light cruiser 180x18, 30 kn turn     0%      miss         hit
              destroyer     110x11, 33 kn turn     0%      miss         hit

          AimOnly is deterministic - one geometry, so it either hits or it does not, and
          that column is not a rate. Read it together with the residual it leaves: about
          10-20 m short and a few metres across, coming from the game's own 2-unit (20 m)
          release radius and its straight-line lead. That is comfortably inside a cruiser
          or a battleship and comparable to a destroyer's beam, which is why destroyers
          stay marginal in AimOnly.

          For aerial torpedoes, removing the aim error barely matters: a straight-running
          fish is beaten by the course change, not by the aim error. Guidance is what
          fixes torpedoes.

          The harness also measures guidance authority - how bad a release it can still
          turn into a hit. At the default setting: over 600 m from a 150 m release, 240 m
          from a 60 m release. The game's own aim error is at most about 80 m, so there is
          roughly an order of magnitude of margin.

          Dive bombing is deliberately absent from those tables. Its release depends on
          Config.diveBombManeuvreRate and the aircraft flight model, neither of which can
          be read out of the assembly, so no vanilla or AimOnly baseline is claimed for
          it. Guidance is measured for it the same way as everything else.

  verify\ loads the real Assembly-CSharp.dll and asserts that every method and field the
          mod patches or reads exists with the expected signature. Harmony resolves its
          targets by name at runtime, so without this a signature that had drifted would
          compile cleanly and only fail once the game was running.

What neither harness can judge is how it looks on screen. The physics is verified; the
feel of a guided bomb's last second of flight is not. If a correction looks unnatural,
lower BombGuidanceAuthority.


KNOWN LIMITATION
----------------

Which round is guided at which ship is tracked in memory and is not written into save
games. If you save a battle with ordnance already in the air and reload it, those
particular rounds resume unguided and finish their flight ballistically. Everything
released after the reload is guided normally. This does not affect AimOnly, which has
nothing to remember.


REQUIREMENTS
------------

War on the Sea v1.09a, and BepInEx 5.4.23.2 x64.

WoTSAceAviators.zip       the mod alone, for a game that already has BepInEx.
                          Extract into the game folder.
WoTSAceAviators-Pack.zip  BepInEx plus the mod, self-contained. Extract into the game
                          folder and you are done. See INSTALL.txt inside.

This mod is independent of the War on the Sea Trainer and the two can be installed
together.


UNINSTALL
---------

Delete BepInEx\plugins\WoTSAceAviators.dll. Nothing in the game's own files is modified,
and saves made with the mod installed remain readable without it.
