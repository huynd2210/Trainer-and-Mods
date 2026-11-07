# Stoneshard mods

## Mercenaries 0.1.0

**Hire a tavern mercenary who travels with you and fights at your side.** Talk to a tavern
mercenary and pick *"Looking for work?"*: 500 crowns to sign, then 50 a day, paid at midnight.
Unpaid, they leave; killed, they're gone for good and never return to their tavern.

- Follows you through doors, stairs, area edges and save/load, in their own look.
- Right-click them: *Wait here* / *Follow me*. Left-click them: swap places.
- One mercenary at a time. They never turn on you.

**Install:** needs [ModShardLauncher](https://github.com/ModShardTeam/ModShardLauncher) 0.13.2 and
Stoneshard 0.9.4.24. Extract `dist\Mercenaries.zip` into the ModShardLauncher folder
(`Mods\Mercenaries.sml` next to `ModShardLauncher.exe`). Back up the game's `data.win`, then in
ModShardLauncher open it, tick **Mercenaries** and save.

**Source:** `src\Mercenaries\` (C# patch + GML), plus `src\_tools\mslpack\`, a headless packer.
`dist\Mercenaries-Source.zip` holds the same files; `package.ps1` rebuilds the `.sml` and the zips.
