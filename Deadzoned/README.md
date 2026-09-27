# Deadzoned mods

Two GameMaker `data.win` patches. They stack: install either or both, in any order.

| Mod | Zip | What it does |
|---|---|---|
| Dice Mod | `dist/DeadzonedDiceMod.zip` | normal / always / never for your and enemies' hits, crits, dodges, your talent procs, and hacking |
| Morgue | `dist/DeadzonedMorgue.zip` | writes a trad-roguelike character dump (.txt) at the end of every run |

Source for each is in `src/<mod>/`.

## Deadzoned Dice Mod

Choose how the dice fall in Deadzoned: your hits, crits and dodges, the enemy's, your talent procs, and terminal hacks.

Open it from **Escape → settings → dice rolls**. It is also on the title screen's settings, so you can set it up before a run. Every roll has three modes:

| Mode | Effect |
|---|---|
| `normal` | the game's own roll |
| `always` | the roll succeeds |
| `never` | the roll fails |

| Row | What it decides |
|---|---|
| your hits | whether your attacks land |
| your crits | whether a hit that lands is critical |
| your dodges | whether you evade an enemy attack |
| your procs | your talent, item and weapon-trait chances in combat: Trick Shot, Swift Strike, Decapitation, Lucky Shot, Mortal Arcana, Stun, Knockback, Shred, Rapid Fire, Load Lover, Sixth Sense, Hand Parry, Lucky Miss, Displacement Field, Herbalist, and the rest |
| enemy hits | whether enemy attacks land (including grazes) |
| enemy crits | whether an enemy hit is critical |
| enemy dodges | whether enemies evade your attacks |
| hacking | whether terminal hacks succeed |

A common setup is your hits, crits and dodges on `always`, with enemy hits and enemy dodges on `never`.

Settings apply immediately and are saved to `dicemod.ini` in the game's save folder (`%LOCALAPPDATA%\Deadzoned`).

### What you'll see

- The hit % shown when you hover an enemy follows the setting: **100%** on `always`. Crit % (Firmity Analyser) and the hack % on terminals follow theirs too.
- `never` on your hits shows 1%, not 0%. The game treats a 0% target as unattackable, so a true 0 would stop you swinging at all.
- Impossible attacks stay impossible. A melee target out of reach still can't be attacked; `always` guarantees the roll, not the reach.
- `always` on your procs is strong. With Mortal Arcana or Decapitation it means instant kills, and with Vampyre Highborn every melee kill gives Max Health and a random stat.
- When two settings disagree, your dodge wins. Enemy hits on `always` plus your dodges on `always` means you dodge every attack.

### Install

See `INSTALL.txt`. In short: close the game and run `install.ps1`. It backs up the `data.win` you have now and patches it, so other Deadzoned mods such as the Morgue stay installed. It fetches UndertaleModTool 0.9.2.0 (checksum-pinned) and includes no game files.

To uninstall, run `uninstall.ps1`. If another mod was installed after this one, uninstall refuses rather than silently removing that mod too; use Steam's *Verify integrity of game files* (back to vanilla), then reinstall the mods you want. A game update also reverts the patch; run `install.ps1` again after one.

### How it works

Deadzoned is a GameMaker game: `data.win` holds its code as bytecode. The patch adds one script (`src/dzm.gml`) and edits seven existing code entries in place:

- `HeroAction` alarm 1 (your attack) and `EnemyAction` alarm 1 (theirs) apply the override right after the game makes each roll. Every vanilla roll still runs, so on `normal` the random stream is exactly the game's own.
- `sHero_HitChance`, `sHeroAction_GetCritChance` and `sHackTerminal_Chance` make the displayed percentages match. `sHackTerminal` applies the hack result.
- `EscapeMenu`'s step, begin step and draw add the `dice rolls` page, reusing the layout and colours of the Video and Audio pages.

The game's shared `rd()` roll helper is left alone. Level generation and loot use it too, so the patch changes only the combat and hacking rolls.

Every edit is matched against exact vanilla code with a required occurrence count. If a game update moves that code, the patcher stops with an error and writes nothing, so it can't half-patch.

Tested in game on the current Steam build (26 automated checks, 200 attacks per case): each mode gives the expected outcome on every attack, and `normal` gives the game's usual mix.


## Deadzoned Morgue

A morgue file for every run, like Angband and other trad roguelikes. When a run ends, a plain-text character dump is written to:

    %LOCALAPPDATA%\Deadzoned\morgue\<date>_<time>_<class>_<result>.txt

Each run also adds one line to `morgue\runs.txt`, a running history of all your runs.

There is no in-game UI. Install it and play.

### When a file is written

| Result | When |
|---|---|
| `died` | an enemy, an explosion or poison kills you |
| `seppuku` | you pick *commit seppuku* from the quit menu |
| `won` | you beat the run. If you then carry on in endless mode, the eventual death gets its own file too |
| `abandoned` | you pick *start new run* from the quit menu mid-run |

*Quit to title* doesn't end the run, so it writes nothing. The mod's own record of the run (kills, notes, turns, play time) is saved inside the game's run save, so a continued run picks up where it left off. The tutorial is skipped.

### What's in it

```
  [Deadzoned morgue file]                              2026-09-27 12:41

  Hunter, character level 1
  killed by a Spindle Spider on floor 1

  Mode       The Outzone Bust Up               Score      180
  Target     Vagrant                           Floor      1
  Seed       6257846048                        Turns      0
  Play time  0:00:02                           Kills      2

  Health     0 / 35               Skill    20
  XP level   1                    Power    15
  XP         18 / 8               Tech     10
  Dmg taken  2                    Luck     10
  Status     poisoned (2)

  [Loadout]
    1  Knife                 melee  dmg 7   durability 72
          traits: Pierce
  * 2  Pistoller             pistol dmg 14  range 8   ammo 2/4    durability 65
    3  Shotgun               rifle  dmg 18  range 9   ammo 1/1    durability 71
          traits: Knockback

  [Ammo]
    8mm Bullets  x18
    Shotty Shells  x12

  [Talents]
    Bounty Hunter

  [Kills]  2 total
      1  Rag Scab
      1  Blood Bat

  [Notes]
  0:00:00  floor 1  Started a Hunter run of The Outzone Bust Up
  0:00:02  floor 1  Killed by a Spindle Spider (2 damage)

  [Last messages]
    You claim a bounty!
    You killed a Rag Scab gaining +16XP and +160 Score.
    You killed a Blood Bat gaining +2XP and +20 Score.
    You've been afflicted with Poison!
```

(A real file from testing, hence the 2-second run.)

- **Stats** show the effective value, with `(base N)` added when gear or talents change it.
- **Loadout** marks the equipped weapon with `*`. Other items are grouped by the game's own item types (Gear, Arcana, Action, Ammo and so on).
- **Kills** are sorted most-killed first.
- **Notes** is the run's timeline: floors reached, character levels, talents learned or upgraded (shown as `II`), bosses slain, and how the run ended.
- **Play time** counts only time on a floor, not menus or the pause screen.

### Install

See `INSTALL.txt`. In short: close the game and run `install.ps1`. It patches the `data.win` you have now, so other Deadzoned mods such as the Dice Mod stay installed. It also backs the file up first.

To remove it, run `uninstall.ps1`. If another mod was installed after this one, uninstall refuses rather than silently removing that mod too. In that case use Steam's *Verify integrity of game files*, then reinstall the mods you want. A game update also removes the mod; run `install.ps1` again after one.

### How it works

Deadzoned is a GameMaker game. The patch adds one script (`src/mrg.gml`) and hooks it into the game's own code:

| Game code | Hook |
|---|---|
| `sNewGame` / `sLoadRun` / `sSaveRun` | start, restore and save the tracker |
| `sHurtHero` | what hurt you and for how much |
| `sGiveHeroXPKill` | every kill you're credited with |
| `sConsole` | the last 20 messages |
| `sTurnEnd` | turn count; labels poison damage |
| `Con_GameOver`, `Con_Won`, the escape menu's *start new run* | write the file |

Every edit is matched against exact vanilla code with a required occurrence count. If a game update moves that code, the patcher stops with an error and writes nothing.

Tested in game on the current Steam build: turns, kills and a save/load round-trip, then all four endings through the game's real paths. That means the real menu keys for abandon and seppuku, and a real enemy attack for death.
