# Sephiria mods (game v1.0.33)

Three independent mods. Each patches `Sephiria_Data\Managed\Assembly-CSharp.dll` with a small
Mono.Cecil patcher and drops its hook DLL beside it. No mod loader is involved.

| Mod | What it does | Keys |
|---|---|---|
| **SephiriaTrainer** | +1,000 saphires (profile) / +10,000 gold (current run) per press | F6 / F7 |
| **SephiriaStatBuff** | Adds configurable stat bonuses (damage, attack speed, damage reduction, regen, luck, …) from `SephiriaStatBuff.cfg` | F8 toggle, F9 reload config |
| **SephiriaExpandedInventory** | 1–16 extra scrollable inventory rows of 6 slots (default 4), set in `SephiriaExpandedInventory.cfg` | — |

## Install

Requires the .NET 8 runtime. Pick only the mods you want from `dist\`:

- `<Mod>.zip` — the mod, prebuilt. Extract into the game folder (you get `<Mod>\` beside
  `Sephiria_Data\`), close the game, and run
  `powershell -NoProfile -ExecutionPolicy Bypass -File .\<Mod>\install.ps1`.
  Each zip's README has the details.
- `<Mod>-Source.zip` — that mod's source, same as `src\<Mod>`.

The mods stack. Uninstall with each mod's `uninstall.ps1`, removing the trainer **last**: its
uninstaller restores the assembly it backed up at install time, which also drops the hooks of any
mod installed after it. Empty the extra inventory rows before removing ExpandedInventory.

## Building from source

Extract a source zip into the game folder, beside `Sephiria_Data\` — the projects reference the
game's assemblies at `..\..\Sephiria_Data\Managed`. Its `install.ps1` builds both projects with
the .NET 8 SDK and installs.
