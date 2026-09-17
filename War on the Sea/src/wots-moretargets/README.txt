MORE TARGETS - War on the Sea (v1.09a)
======================================

The enemy campaign AI never runs out of command points. It stops sitting out its turns for
lack of funds, and every force it does raise is the largest tier it knows how to build.

Campaign only. Skirmish does not use command points at all.


WHY THIS IS THE RIGHT LEVER
---------------------------

In campaign the enemy's building budget is

    commandPoints[0] + commandBonusPoints[0] - commandPointSpent[1]

read through one private method, CampaignAI.GetForceAvailable(). That single number does two
jobs at once: the AI's decision sequence treats a negative result as "stop", and every branch
that raises a force passes the same number along as the budget that picks how big that force
is (the thresholds are 10 / 50 / 100). Run the enemy into deficit and it simply stops sending
anything until its weekly income recovers.

This mod overrides that one reading. Nothing else in the economy is touched.

In particular your own pool is safe. It is a different index of the same arrays -
commandPointSpent[0] rather than [1] - and it is what the campaign UI displays. The obvious
approach of inflating commandPoints[0] would have handed the AI the same money by accident,
since both sides read that same slot; going through GetForceAvailable is what keeps this
strictly enemy-side.


WHAT YOU WILL ACTUALLY SEE
--------------------------

Bigger enemy task forces, and no quiet stretches where nothing sails.

What you will NOT see by default is a higher rate of enemy sorties, because the rate is not
governed by money. Two other throttles set it, and both are left alone unless you ask:

  * One force per strategic decision. The game raises a force and immediately marks the
    decision spent. Money never enters into it.
  * A per-mission cooldown, in days, before the next strategic decision happens at all.

Both are exposed in the config as ForcesPerDecision and DecisionDelayPercent. They are pacing
rather than budget, so they are not part of "infinite command points" and do not turn
themselves on. If what you want is more enemy groups at sea rather than bigger ones, those are
the two knobs.


THE REAL CEILING: THE ENEMY ONLY HAS SO MANY SHIPS
--------------------------------------------------

Worth knowing before you go hunting. Money is not the enemy's binding constraint for long.

Every enemy ship comes from a finite historical order of battle: a class stays available only
while the number of its hulls that are sunk, already at sea, or otherwise unavailable is below
the number of named ships in that class. Sunk hulls never come back. Once you have sunk enough
of them, the AI starts failing to raise forces no matter how many command points it has - and
that looks exactly like this mod not working.

So if your aircrews have stopped missing, expect the enemy navy to become the limit rather
than the enemy treasury.

The Supply section has ReplenishSunkHulls for that, off by default. It lets the enemy raise
classes it has already lost. It is off because it changes what a campaign is: sinking the
enemy's order of battle permanently is normally how you win one. It does not edit your save or
your records - the campaign summary still lists every ship you put on the bottom - and turning
it back off restores stock behaviour immediately.


CONTROLS
--------

  F7   toggle the mod on and off

A small "MORE TARGETS" line in the top-left corner shows when it is on and what it is doing.
It sits below the War on the Sea Trainer's line and Ace Aviators', so all three can be
installed together.

Toggling off restores the game's own behaviour completely and immediately. Every patch is
gated on the switch; nothing is left behind in the save.


CONFIGURATION
-------------

First run writes BepInEx\config\com.wots.moretargets.cfg.

  [Controls]
  ToggleKey               default F7

  [General]
  EnabledOnStart          default true
  ShowStatusLine          default true

  [Budget]
  EnemyCommandPoints      default 100000
      The budget the enemy AI is told it has whenever it asks. This is a gate, not a wallet:
      the game compares it against thresholds of 10, 50 and 100 to choose a force size, and
      never subtracts from it. Anything above 100 means the enemy always raises the largest
      force tier and never sits out a decision for lack of points.

  [Pacing]
  ForcesPerDecision       default 1
      How many task forces the enemy may raise in one strategic decision. 1 is the game's own
      behaviour. The decision sequence offers about ten openings, so that is the ceiling
      however high you set this.

  DecisionDelayPercent    default 100
      Scales the cooldown each mission imposes before the enemy's next strategic decision.
      100 is stock pacing, 50 is twice as often, 0 removes the cooldown.

  [Supply]
  ReplenishSunkHulls      default false
      Let the enemy raise ship classes it has already lost. See above.


HOW IT WAS CHECKED
------------------

Two harnesses run as part of building the mod, and both must pass before it packages.

  logic\  compiles the actual shipped decision logic - not a copy of it - and drives it
          through a replica of the game's strategic decision sequence. There is no physics
          here, but there is one rule that would fail silently if it were wrong: a creation
          the game refuses (no mission, no hulls left, unsafe route) must not consume the
          decision's allowance. Get that backwards and the enemy goes quiet for exactly the
          opposite of the intended reason, which looks identical to the mod not being
          installed. The harness asserts that the default reproduces the game's own one force
          per decision, that refusals never eat the allowance, and that the cooldown scaling
          cannot round below zero.

  verify\ loads the real Assembly-CSharp.dll and asserts that every method and field the mod
          patches or depends on exists with the expected signature. Harmony resolves its
          targets by name at runtime, so without this a signature that had drifted would
          compile cleanly and only fail once the game was running. It also pins the shape of
          the command-point arrays, because the claim that this mod is enemy-only rests on
          those being one slot per faction.

What neither harness can check is campaign feel over many in-game weeks - whether the enemy
now sends enough, too much, or runs its navy dry. That needs a real campaign. Start at the
defaults and reach for the Pacing section only if the sortie rate still feels thin.


REQUIREMENTS
------------

War on the Sea v1.09a, and BepInEx 5.4.23.2 x64.

WoTSMoreTargets.zip       the mod alone, for a game that already has BepInEx.
                          Extract into the game folder.
WoTSMoreTargets-Pack.zip  BepInEx plus the mod, self-contained. Extract into the game folder
                          and you are done. See INSTALL.txt inside.

Independent of the War on the Sea Trainer and of Ace Aviators; all three can be installed
together.


UNINSTALL
---------

Delete BepInEx\plugins\WoTSMoreTargets.dll. Nothing in the game's own files is modified, and
saves made with the mod installed remain readable without it - the mod never writes anything
to a save, it only changes an answer the AI is given while it is running.
