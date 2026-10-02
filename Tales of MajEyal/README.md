# ToME Trainer

A hotkey trainer for **Tales of Maj'Eyal 1.7.6**, built as a native ToME addon. It needs no mod loader.

| Key | Function |
|---|---|
| **F9**  | Heal to full HP |
| **F10** | Toggle: a hit that would kill you leaves you at 1 HP instead |
| **F11** | Add 1000 EXP, levelling up if it is enough |
| **F12** | Toggle: your melee and ranged attacks never miss |

Each key press is confirmed in the message log (`[Trainer] ...`) and as floating text over your
character. To change the keys, open **Game Options > Key Bindings > Trainer**.

## Install

Extract `ToMETrainer.zip` into the ToME install folder, for example
`C:\Program Files (x86)\Steam\steamapps\common\TalesMajEyal`. This places
`game\addons\tome-trainer.teaa`. You can also copy `tome-trainer.teaa` into `game\addons` by hand.

The addon is on by default. It is listed as **Trainer** under **Addons** in the new-game screen.

### Existing characters

ToME only loads the addons that were active when a character was created. A character made
before you installed the trainer will not load it. To enable it for such a character, edit
`%USERPROFILE%\T-Engine\4.0\tome\save\<character>\desc.lua` and add `'trainer'` to its
`addons` list:

```lua
addons = {'items-vault', 'possessors', 'trainer'}
```

Back up the save folder before you edit it. Characters created after you install the trainer
pick it up without any edit.

## Details

- **Toggles start OFF on every launch.** They are not saved with the character.
- **Fatal hit leaves 1 HP** protects the character you control and your main character. It
  covers ordinary damage and instant-kill effects. Deaths you choose in the story still happen:
  the endgame sacrifice options, and asking the Eidolon to let you die.
- **Never miss** removes your accuracy roll, the penalty for attacking something you cannot see,
  and the target's evasion, for both melee and archery. Enemy talents that block or parry a blow
  still work: Repel, Blade Ward, and shield deflection.
- **+1000 EXP** gives exactly 1000 EXP, whatever the birth EXP multiplier is set to. With the
  Possessors DLC, EXP gained while you possess a body is held until the possession ends, as the
  game normally does.

## Build

Run `package.ps1`. It writes `dist\tome-trainer.teaa`, `dist\ToMETrainer.zip` and
`dist\ToMETrainer-Source.zip`.
