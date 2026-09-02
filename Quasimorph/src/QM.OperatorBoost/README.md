# Quasimorph Operator Boost

Make your operators stronger by a fixed amount you choose. Every setting is a **flat bonus added
on top of** what the operator already has — their profile stats, class, perks, implants,
augmentations, wounds and buffs all still apply and still matter. Nothing is replaced or
overwritten, and no operator ends up with the same numbers as any other.

**Everything starts at 0**, so installing the mod changes nothing until you decide what to raise.

## Controls

| | |
|---|---|
| **F10** | Toggle the mod on/off mid-game |

A readout in the bottom-left corner lists the boosts currently in effect.

## Settings

Edit `BepInEx/config/com.claude.quasimorph.operatorboost.cfg`, or use
[ConfigurationManager](https://github.com/BepInEx/BepInEx.ConfigurationManager) to change them live
without restarting — the new values take effect within a second, on operators you already own.

### General

| Setting | Default | What it does |
|---|---|---|
| `Enabled` | `true` | Master switch. Off = your operators have exactly their unmodded stats. |
| `ToggleKey` | `F10` | Rebind the toggle. |
| `ShowStatusOverlay` | `true` | The corner readout. |
| `OnlyOperatorInRaid` | `false` | On = only the operator you deployed is boosted. Off = your whole roster. |
| `GrantHealthOnMaxIncrease` | `true` | When `MaxHealth` raises the ceiling, hand over the extra hit points too. Off = you get the headroom but have to heal into it. |
| `VerboseLogging` | `false` | Log what the mod is doing, into `BepInEx/LogOutput.log`. |

### Survivability

| Setting | Unit | What it does |
|---|---|---|
| `MaxHealth` | hit points | Added to maximum health, on top of the operator's own health and implants. |
| `AllResists` | resist points | Added to every damage type, on top of armour and perks. These are the points the operator screen shows per type — the game still runs them through its own diminishing-returns curve, so this is not a flat damage reduction. |
| `DodgeChance` | percentage points | `10` = +10% dodge on top of the operator's own. |
| `HealthRegenPerTurn` | hit points | Regenerated at the start of each of your turns. Raid only. |
| `PainRegen` | points | Extra pain threshold recovered per turn. |

### Offence

| Setting | Unit | What it does |
|---|---|---|
| `MeleeAccuracy` | percentage points | Added after the weapon, wound and movement penalties are worked out. |
| `RangeAccuracy` | percentage points | Same, for ranged weapons. |
| `CritChance` | percentage points | Melee, ranged and thrown. The game still caps the final chance at 100%. |
| `CritDamage` | percentage points | Added to the operator's own crit damage bonus. |
| `ArmorPenetration` | percentage points | Added to whatever the weapon and its ammunition already penetrate. |
| `MeleeDamage` | percentage points | The same additive bonus perks grant. `25` = +25% melee damage **added to** your perk bonuses, not a separate multiplier stacked on the result. |
| `RangeDamage` | percentage points | Same, for ranged weapons. |
| `MeleeFlatDamage` | damage | Added to every melee hit before resistances. |

### Utility

| Setting | Unit | What it does |
|---|---|---|
| `ActionPoints` | AP | Added to every movement stance. `1` or `2` is already a large change. |
| `SightRange` | tiles | Added to line of sight. |
| `FirearmRange` | tiles | Added to a firearm's effective range, before damage falloff is worked out. |

### Inventory

| Setting | Unit | What it does |
|---|---|---|
| `BackpackWidth` | columns | Added to the equipped backpack's grid, or to the operator's bare-backed default when they have none. |
| `BackpackHeight` | rows | Same, vertically. |
| `VestSlots` | slots | Added to the equipped vest, on top of any perk or wound-effect bonuses. |

Swapping backpacks keeps the bonus: a 4×3 backpack with `BackpackWidth = 2` becomes 6×3, and the
next backpack you find is widened by 2 as well.

**Inventory size is the one boost that is written into your save**, because a grid's dimensions and
the item positions inside it are saved state — there is nothing to recompute on the fly. Read the
note below before lowering these.

Negative values are allowed everywhere if you want to make a run harder instead.

## What "boost, not replace" means here

Each stat is read through a method the game calls every time it needs the value, and the mod adds
its bonus to the answer on the way out. So:

- The operator keeps everything that made them individual. A high-accuracy specialist with
  `RangeAccuracy = 15` is still 15 points better than a bruiser with the same setting.
- Wounds, drunkenness, suppression and the run-accuracy penalty still bite, because the game
  applies them before the bonus is added.
- **Nothing is written into your save.** Clear a setting and the stat is back to stock the next
  time it is read. Uninstalling the mod leaves no trace on your operators. (The Inventory section
  is the exception — see above.)

Two deliberate exceptions, both so the mod cannot make things nonsensical:

- **Accuracy is left alone when the game returned zero.** That is how effects like Broken Focus say
  "you cannot aim at all", and a flat bonus must not quietly undo it.
- **Sight range is left alone while blinded**, for the same reason.

### Maximum health is the one stat that needs upkeep

Health is the exception to "read every time": the game caches your maximum in the save and only
recomputes it when something structural changes, like fitting an augmentation. So the mod
recomputes it once a second using the game's own formula, `BaseHealth + wound penalty + bonus`.

Your operator's real `BaseHealth` is never touched, and because the formula reads the bonus — which
reports zero whenever the mod is off — the same pass is what puts your maximum health back to stock
when you lower the setting, press F10, or disable the mod. If you uninstall the mod outright while
a boost is set, load the save once with the mod still installed and `MaxHealth = 0` first, and it
will tidy up on its own.

### Shrinking a grid never costs you items

The game's own resize hands anything that no longer fits to the floor, or deletes it outright when
there is no floor to hand it to — that is normal when you swap to a smaller backpack in a raid and
watch the overflow drop at your feet, but it is not acceptable as the silent result of editing a
config file.

So this mod only ever **grows** a grid on its own. If you lower `BackpackWidth`, `BackpackHeight`
or `VestSlots`, or press F10, or disable the mod, the grid stays as it is until that storage is
empty, and the overlay tells you which operator is waiting:

> `Ferrus's backpack is waiting to shrink to 4x3; empty it first`

Move the items out and it resizes on its own within a second. Nothing is ever dropped or destroyed
to make room. The one exception is the game's own path: swapping backpacks in a raid still spills
overflow onto the floor exactly as it does unmodded.

If you uninstall the mod outright while a size boost is set, the grid keeps its size until you next
equip or remove a backpack or vest, at which point the game resizes it to stock and handles any
overflow the way it always does.

## Requirements

BepInEx 5.4.23.2 (x64) and Quasimorph v1.0. Drop `QM.OperatorBoost.dll` into `BepInEx/plugins/`,
or use the `-Pack` zip which brings BepInEx with it.
