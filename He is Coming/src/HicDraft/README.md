# He is Coming - Equipment Draft

Press a hotkey to open the game's own Item Compendium as a **draft menu**: click any item and it goes
straight onto your character in the current run.

The menu is not a plugin-drawn overlay. It is `ItemCompendium`, the browser the game already ships -
so every item keeps its real icon, its real description, its rarity/type/tag/status filters, its hover
tooltip, and full controller navigation.

## Use

| Action | Input |
| --- | --- |
| Open / close the draft menu | `F7` (configurable) |
| Draft an item | Click it |
| Leave | The compendium's own back button, or `F7` again |

The header line reads `DRAFT - CLICK AN ITEM TO EQUIP IT` while drafting is live, and reports what
happened after each pick. Items go where they belong:

| Item type | Where it lands |
| --- | --- |
| Weapon | Weapon slot |
| Bag | Backpack slot |
| Edge | Applied to the equipped weapon |
| Inventory item | Lowest free inventory slot |
| Set | Added as a set bonus |

Drafting needs a run in progress. Opened from the main menu the compendium still browses normally;
picks report that there is nothing to equip.

## Configuration

`BepInEx/config/dev.heiscoming.draft.cfg`, written on first launch.

| Setting | Default | Meaning |
| --- | --- | --- |
| `OpenDraft` | `F7` | Hotkey. Unity Input System key names - `F7`, `Backquote`, `Insert`, `P`. |
| `CloseAfterDraft` | `false` | Close the menu after a successful pick instead of staying open for a whole loadout. |
| `CloneDraftedItem` | `true` | Hand the run a fresh copy rather than the pooled instance. Turn off only if drafted items misbehave. |

## Install

See `INSTALL.txt`. `HicDraft.zip` is the mod alone, for a game that already has BepInEx;
`HicDraft-Pack.zip` is BepInEx plus the mod, for a clean game folder.

## Requirements

He is Coming v0.9.22 (Unity 6000.0.74f1, IL2CPP) with BepInEx 6 bleeding-edge, IL2CPP, win-x64.
