# Dead Weight Trainer 1.1

Built for Dead Weight 1.0.1.10 / Godot 4.4.1.

## Start

Double-click `Launch Dead Weight Trainer.cmd`. It backs up the current `settings` and `saves*` data, then starts `Dead_weight.trainer.exe` from this folder with the main game directory as its working directory.

The trainer uses the same saves as the normal game. Backups are stored in `Trainer Save Backups` beside the game.

Keep the `Dead Weight Trainer` folder directly inside the main `Dead Weight` game directory. The three DLL files in this folder are hard links to the game's required Steam and analytics runtime DLLs, so they do not consume duplicate disk space. Deleting the trainer folder removes only these extra links, not the original game DLLs.

## Hotkeys

| Key | Action |
|---|---|
| `F1` | Heal the active player hero to full |
| `F2` | Restore the active player hero's action points |
| `F3` | Heal all living party members and restore their action points |
| `F4` | Toggle unlimited AP / free actions |
| `F5` | Toggle player immortality |
| `F6` | Toggle instant skill cooldowns |
| `F7` | Toggle ignoring status effects |
| `F8` | Toggle infinite fuel |
| `F9` | Toggle unlimited turn reversal |
| `F10` | Toggle unlimited skill-tree purchases |
| `` ` `` | Open or close the game's full developer panel |

The backtick key is normally immediately left of `1`. Trainer actions show a short confirmation in-game.

Full-heal and AP actions work during battle and affect living player-controlled units. If AP has already reached zero, press `F2` before enabling `F4`.

When `F10` is on, the skill tree displays `999999`, allows purchases with no available points, and does not consume or alter the character's real skill-point balance. Press `F10` again to return to normal spending.

## Developer panel

The shipped developer panel includes additional controls for money, fuel, items, sidekicks, abilities, cooldowns, fog of war, battle victory, animation speed, skill points, buildings, characters, and tile-based heal/hit/kill actions.

Some panel actions permanently change the active save. Use the supplied launcher so a backup exists first.

## Compatibility and removal

This trainer is tied to the current `Dead_weight.exe` build. If the game is updated or replaced, rebuild the trainer before using it.

Original executable SHA-256:

`AD271FCA21AEA025879CF857D2B2AEC521209338DD4ED162929E23332BFB1CB7`

Trainer executable SHA-256:

`56F31EB263E146459ABF2AEFCF4BB5732072E5370F50D2B0A57F9A58F333ECA7`

The original `Dead_weight.exe`, `Dead_weight.console.exe`, and game DLLs were not modified. To remove everything trainer-related, delete the complete `Dead Weight Trainer` folder.
