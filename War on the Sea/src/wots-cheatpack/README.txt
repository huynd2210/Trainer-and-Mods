WAR ON THE SEA - CHEAT PACK
===========================
For War on the Sea v1.09a (x64 Mono build). BepInEx 5.4.23.2 x64.

Four mods, one install. Each is independent, each has its own hotkey and its own
config file, and any of them can be deleted without affecting the others.

  F1 / F2   TRAINER         infinite command points; reveal all fog of war
  F5 / F6   ACE AVIATORS    your bombers aim true, and optionally cannot miss
  F7        MORE TARGETS    the enemy AI never runs out of command points
  F8        FIRE CONTROL    AA stops spraying; gunnery solutions acquire instantly

Every key is a default only. All four write a config file to BepInEx\config\ on
first run and read their hotkeys from it, so rebind anything that clashes.

Everything here is player-side. No mod in this pack touches enemy aim, enemy AA
or enemy fire control; More Targets is the only one that changes anything on the
enemy's side and all it does is stop their treasury running dry.


WHAT EACH ONE DOES
------------------

TRAINER  (F1, F2)  -  config: com.wots.trainer.cfg
    F1  Infinite Command Points. Campaign purchases never fail and your pool
        never depletes. Deliberately does NOT inflate the shared points array,
        because the enemy AI reads the same slot - see More Targets.
    F2  Reveal All. Campaign map: every enemy unit visible, always spotted, with
        exact composition in contact tooltips. Tactical battles: every enemy
        detected and identified, so map icons, targeting and the 3D camera all
        work on them.

ACE AVIATORS  (F5 toggle, F6 mode)  -  config: com.wots.aceaviators.cfg
    Removes the random aim error the game rolls into every air attack run - a
    radius of 70 m for level bombing, 80 m for an aerial torpedo, and for a dive
    30 m plus the target's speed and rate of turn.
      AimOnly (default)  stops there. Your crews stop missing for no reason, but
                         a destroyer that puts the helm over still gets away.
      AimAndGuidance     also steers released ordnance onto the target for the
                         rest of its flight, and suppresses duds. Nothing gets
                         away.
    Measured: level bombing a turning light cruiser goes from an 18 percent hit
    rate to a certain hit. Full numbers in docs\WoTSAceAviators-README.txt.

MORE TARGETS  (F7)  -  config: com.wots.moretargets.cfg
    The enemy campaign AI never sits out a strategic decision for lack of funds,
    and always raises the largest force tier it knows. Your own command points
    are untouched - it overrides one enemy-side reading of the budget, not the
    shared pool the campaign UI shows you.
    Campaign only. Skirmish does not use command points.
    Read its README before expecting more frequent sorties: the sortie RATE is
    set by two other throttles, left at stock on purpose and exposed in config.

FIRE CONTROL  (F8)  -  config: com.wots.firecontrol.cfg
    Anti-aircraft: close-in AA stops spraying and stops firing at stale aim
    points. Both halves of that are needed - tightening the cone alone makes AA
    WORSE, and its README has the measurements showing why.
    Naval gunnery: the fire-control solution converges on the first director tick
    instead of being walked up over a minute of shooting. Shell dispersion is NOT
    removed; your guns still scatter, they just stop aiming at a guess.
    The two halves have separate switches, so you can try one at a time.


INSTALL
-------

Two zips, pick one.

  WoTSCheatPack-Pack.zip   BepInEx plus all four mods, self-contained. Extract
                           into the game folder (the one with WarOnTheSea.exe)
                           and you are done. See INSTALL.txt inside.

  WoTSCheatPack.zip        the four plugins alone, for a game that already runs
                           BepInEx 5. Extract into the game folder; it drops
                           four DLLs into BepInEx\plugins.

To install only some of them, take the DLLs you want out of BepInEx\plugins and
delete the rest. Nothing depends on anything else.


UNINSTALL
---------

Delete the DLLs from game\BepInEx\plugins. No game file is modified by any of
these mods, and none of them writes anything into a save - a save made with all
four installed still loads with none of them.

To remove BepInEx as well, delete winhttp.dll, doorstop_config.ini and the
BepInEx folder.


ON-SCREEN STATUS
----------------

Each mod draws a one-line status in the top-left while it is active, stacked so
they do not overlap: Trainer, then Ace Aviators, then More Targets, then Fire
Control. Every mod can turn its own line off with ShowStatusLine in its config.


HOW THESE WERE BUILT AND CHECKED
--------------------------------

Every mod ships with two harnesses that must pass before it packages:

  a signature harness that loads the real Assembly-CSharp.dll and asserts that
  every method and field the mod patches exists with the expected signature.
  Harmony resolves its targets by name at runtime, so without this a signature
  that had drifted would compile cleanly and only fail once the game was running.

  a behaviour harness that compiles the mod's real logic - not a copy of it - and
  measures what it does. Ace Aviators flies its actual guidance code at a
  manoeuvring ship and scores hits against real hull dimensions; More Targets
  replays the campaign AI's decision sequence; Fire Control models the AA
  geometry from the game's own constants, which is how its defaults were chosen.

What none of them can judge is how any of this feels to play. That part is
unverified.

Source for all four, including both harnesses for each, is in
WoTSCheatPack-Source.zip. See BUILDING.txt in there.
