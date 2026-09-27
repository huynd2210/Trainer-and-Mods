# Deadzoned Dice Mod

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

## What you'll see

- The hit % shown when you hover an enemy follows the setting: **100%** on `always`. Crit % (Firmity Analyser) and the hack % on terminals follow theirs too.
- `never` on your hits shows 1%, not 0%. The game treats a 0% target as unattackable, so a true 0 would stop you swinging at all.
- Impossible attacks stay impossible. A melee target out of reach still can't be attacked; `always` guarantees the roll, not the reach.
- `always` on your procs is strong. With Mortal Arcana or Decapitation it means instant kills, and with Vampyre Highborn every melee kill gives Max Health and a random stat.
- When two settings disagree, your dodge wins. Enemy hits on `always` plus your dodges on `always` means you dodge every attack.

## Install

See `INSTALL.txt`. In short: close the game and run `install.ps1`. It backs up the `data.win` you have now and patches it, so other Deadzoned mods such as the Morgue stay installed. It fetches UndertaleModTool 0.9.2.0 (checksum-pinned) and includes no game files.

To uninstall, run `uninstall.ps1`. If another mod was installed after this one, uninstall refuses rather than silently removing that mod too; use Steam's *Verify integrity of game files* (back to vanilla), then reinstall the mods you want. A game update also reverts the patch; run `install.ps1` again after one.

## How it works

Deadzoned is a GameMaker game: `data.win` holds its code as bytecode. The patch adds one script (`src/dzm.gml`) and edits seven existing code entries in place:

- `HeroAction` alarm 1 (your attack) and `EnemyAction` alarm 1 (theirs) apply the override right after the game makes each roll. Every vanilla roll still runs, so on `normal` the random stream is exactly the game's own.
- `sHero_HitChance`, `sHeroAction_GetCritChance` and `sHackTerminal_Chance` make the displayed percentages match. `sHackTerminal` applies the hack result.
- `EscapeMenu`'s step, begin step and draw add the `dice rolls` page, reusing the layout and colours of the Video and Audio pages.

The game's shared `rd()` roll helper is left alone. Level generation and loot use it too, so the patch changes only the combat and hacking rolls.

Every edit is matched against exact vanilla code with a required occurrence count. If a game update moves that code, the patcher stops with an error and writes nothing, so it can't half-patch.

Tested in game on the current Steam build (26 automated checks, 200 attacks per case): each mode gives the expected outcome on every attack, and `normal` gives the game's usual mix.
