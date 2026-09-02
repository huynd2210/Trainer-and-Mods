# Skull Horde Trainer

A standalone progression trainer for the Windows version of Skull Horde. It reads and writes the game's native Defold serialized-table save format.

## Features

- Unlock every known reward, achievement, bestiary entry, progression gate, and all five playable characters (`base`, `tank`, `low_tier`, `yorick`, and `zombie`).
- Give all characters level 60 and effectively unlimited skill points.
- Set any skill to level 0–10,000, bypassing prerequisites, exclusive branches, and the normal per-skill cap.
- Edit skill points and XP for every character.
- Use global live-run hotkeys through the game's own developer console.
- Mark tutorials complete.
- Edit any boolean, number, or string through the advanced tree editor.
- Automatically back up the original save before every write.
- Restore the latest backup, with a second safety backup before restoration.
- Refuse to write while Skull Horde is running.

## Live hotkeys

Keep the trainer running while playing:

- **F2**: toggle automatic +999 XP (continuous level-ups)
- **F3**: toggle automatic +999 ducats
- **F5**: toggle the built-in developer console
- **F6**: +999 XP once
- **F7**: +999 ducats once
- **F8**: enable invincibility
- **F9**: reveal the full map
- **F10**: drop a legendary item
- **F11**: grant massive combat power
- **F12**: advance to the next floor

The trainer temporarily focuses the game to type verified built-in console commands. Unmodified F-keys are registered globally while the trainer is open and are released when it closes.

## Use

1. Close Skull Horde.
2. Run `Skull Horde Trainer.exe`.
3. Make changes and click **Save Changes**.
4. Launch the game. Leave the trainer open if you want live hotkeys.

Backups are stored in:

`%APPDATA%\8bitskull_skull_horde\trainer_backups`

Run ducats and XP are not persistent save fields, so F2/F3/F6/F7 provide them live. Above-cap skill levels are represented as repeated native skill entries and applied individually when a run starts. Many stat upgrades stack linearly, but extreme levels and one-time/cooldown/cost effects can behave strangely; increase them gradually.
