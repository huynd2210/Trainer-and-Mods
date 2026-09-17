FIRE CONTROL - War on the Sea (v1.09a)
======================================

Your gun crews stop guessing. Two independent halves, both player-side only, both
toggleable on their own.

  ANTI-AIRCRAFT   close-in AA stops spraying and stops shooting at stale aim points.
  NAVAL GUNNERY   the fire-control solution converges immediately instead of being
                  walked up over a minute of shooting.

No guided munitions anywhere in this mod. Nothing homes. Rounds go exactly where the
mount points them - the point is that the mount now points correctly.


ANTI-AIRCRAFT
-------------

Close-in AA in this game is not a shot that hits or misses. WeaponAAA points a mount at
an exact intercept point - Utilities.GetInterceptPoint, which has no randomness in it at
all - and emits a particle stream. A hit is a particle physically intersecting the
aircraft. So a "random miss" is exactly one thing: particles that leave inside the
emitter's cone and do not intersect.

The obvious fix is to collapse that cone. On its own it makes AA WORSE, and this is
worth understanding before you touch the settings.

The cone is quietly covering for a second error. A mount re-aims only once per
WeaponAAA.interval - 0.5 s on hull batteries, 0.2 s on directed mounts - and snaps there
with LookAt. Between updates every round is fired at a point that is going stale at the
aircraft's own speed. A tight cone on a stale aim point is a laser pointed at where the
aircraft used to be.

Measured, at 220 kn and 150 m, fraction of rounds that connect:

                  aim every 0.5s   0.2s   0.1s
    cone 0 deg           62%       100%   100%
    cone 0.25 deg        65%       100%   100%
    cone 2 deg           33%        50%    51%
    cone 4 deg           13%        15%    11%

And against a jinking aircraft at 260 kn and 200 m:

                  aim every 0.5s   0.2s   0.1s
    cone 0 deg           44%        94%   100%
    cone 0.25 deg        43%        90%   100%
    cone 2 deg           25%        26%    25%

So the defaults are a 0.25 degree cone AND a 0.1 s aim interval, which is where both
scenarios reach 100 percent. Tightening one without the other is the trap.

Enemy AA is untouched. Aircraft armour absorption is also untouched - a round that
connects can still be shrugged off, exactly as before.


NAVAL GUNNERY
-------------

Director.CalculateTMAAgainstTarget is the entire gunnery solution in one method. It runs
once a second per tracked target and takes the two numbers this mod changes: how much
solution is gained per tick, and the ceiling that is climbing toward.

Everything downstream follows from the result:

  * the solution sets the aiming error. The director reports a RANGE to the guns, and
    perturbs it by (1 - solution) scaled by tmaMaxRangeError. At a maxed 0.99 solution
    that error is effectively gone.
  * WeaponMount reads the same solution to choose the salvo spread - a maxed solution
    picks the tightest one.
  * it also decides whether the ship needs to walk spotting salvos onto the target
    first. At a maxed solution it does not.

So one override covers all of it: the solution snaps to its ceiling on the first tick,
the range error collapses, the tightest spread is chosen, and no ranging shots are
needed.

    gain/tick     solution after 1s    5s    15s    60s
    0.02 (slow)          0.02        0.10   0.30   0.99
    0.10                 0.10        0.50   0.99   0.99
    1.00 (default)       0.99        0.99   0.99   0.99

WHAT THIS DOES NOT DO: remove shell dispersion. Your guns still scatter. They simply
stop aiming at a guess. Making naval shells stop dispersing is a different change and it
is not safe to do naively - ammunition objects are pooled and handed back after impact,
so a shell permanently stripped of its dispersion follows that pooled object into an
enemy ship's guns on its next use. It is not in this mod.

Enemy directors are untouched.


CONTROLS
--------

  F8   toggle the mod on and off

A small "FIRE CONTROL" line in the top-left shows when it is on and what it is doing. It
sits below the Trainer's line, Ace Aviators' and More Targets', so all four can be
installed together.

Throwing the switch mid-battle walks every player ship afloat and applies or restores the
AA settings, so it takes effect immediately rather than on the next ship to spawn. Each
mount's stock cone and aim rate are recorded before they are first changed, so toggling
off puts them back exactly.


CONFIGURATION
-------------

First run writes BepInEx\config\com.wots.firecontrol.cfg.

  [Controls]
  ToggleKey                default F8

  [General]
  EnabledOnStart           default true
  ShowStatusLine           default true

  [AntiAir]
  Enabled                  default true
  ConeDegrees              default 0.25   emitter cone half-angle
  MuzzleSpreadScale        default 0.25   fraction of the stock muzzle radius; 1.0 leaves
                                          it alone. Minor next to the cone angle.
  AimIntervalSeconds       default 0.1    seconds between mount re-aims. Read the table
                                          above before raising ConeDegrees or this one in
                                          isolation - they only work together.

  [Gunnery]
  Enabled                  default true
  SolutionGainPerTick      default 1.0    the director ticks once a second and the
                                          solution caps at 0.99, so 1.0 is a full
                                          solution on the first tick. 0.1 takes about ten
                                          seconds, if you want fast but not instant.
  ForceMaximumSolution     default true   also raise the ceiling, so range, weather, smoke
                                          and manoeuvring stop capping how good the
                                          solution can get. Turn OFF to keep those
                                          conditions meaningful and only remove the time
                                          it takes to acquire.


HOW IT WAS CHECKED
------------------

Two harnesses run as part of building the mod, and both must pass before it packages.

  logic\  models the AA geometry from the game's own numbers - particle speed 80 u/s read
          off InitialiseAAA, the exact intercept solve, the mount's re-aim interval - and
          measures the hit fraction across cone angles and aim rates. That is where the
          defaults above come from, and it is what caught the trap that collapsing the
          cone alone makes AA worse. It also prints the solution convergence table.

          Two things it cannot know: the stock cone angle lives in the particle prefab,
          and tmaMaxRangeError comes from campaign JSON. Neither is readable from the
          assembly, so no vanilla baseline is claimed for AA and the gunnery error is
          quoted as a fraction of tmaMaxRangeError rather than in metres. The comparisons
          between settings are unaffected; the absolute "before" numbers are simply not
          available without instrumenting a live battle.

  verify\ loads the real Assembly-CSharp.dll and UnityEngine.dll and asserts that every
          member the mod patches, reads or writes exists with the expected signature. Two
          checks there are load-bearing beyond mere existence: the gunnery patch binds
          CalculateTMAAgainstTarget's baseRate and maxSolution parameters BY NAME, so a
          rename would leave the patch attached and doing nothing; and the whole AA half
          writes through ParticleSystem.ShapeModule, whose write-through behaviour is
          Unity-version dependent.

What neither harness can judge is how this feels in a real battle - whether AA is now
oppressive, or whether instant solutions make gunnery duels trivial. That needs playing.
Both halves have their own Enabled switch so you can try one at a time.


REQUIREMENTS
------------

War on the Sea v1.09a, and BepInEx 5.4.23.2 x64.

WoTSFireControl.zip       the mod alone, for a game that already has BepInEx.
                          Extract into the game folder.
WoTSFireControl-Pack.zip  BepInEx plus the mod, self-contained. Extract into the game
                          folder and you are done. See INSTALL.txt inside.

Independent of the War on the Sea Trainer, Ace Aviators and More Targets; all four can be
installed together.


UNINSTALL
---------

Delete BepInEx\plugins\WoTSFireControl.dll. Nothing in the game's own files is modified
and nothing is written to a save.
