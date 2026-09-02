# Break Room Key

Fixes a soft-lock in Cat Mail Co. where the break room can never be opened.

## The bug

The break room door expects a key from the prefab `p_Entity_Carryable_UnlockKey_Key_BreakRoom`. The game scene references that prefab and a spawn point for it (`sp_Unlock_KeyBreakRoom`), but the prefab was **never included in the addressables build**. The key's spawn silently fails, the break room stays locked, and progression can stall.

## What the mod does

When a game starts (no button press):

1. Finds the break room door (named `p_BreakRoom`).
2. **Drops a replacement key** somewhere easy to find (the game's own key spawn point if present, otherwise right at the player), and wires the door to accept it.
3. If the door is still **locked**, it also unlocks it.

The real break room key model was never included in the game build, so the
replacement uses an existing key model (the Sun Room key).

The key is spawned on **every** game start regardless of save state, so the break
room is always unlockable. On a fresh save the break room is walled off by the
parcel pile anyway, so an early key doesn't skip anything.

## Configuration

Settings are in `BepInEx/config/com.catmailco.breakroomkey.cfg`:

- `Behavior / AutoUnlockDoor` (default `true`): unlock the door directly. Set to `false` if you want to try opening it with the spawned key instead.
- `Behavior / SpawnKey` (default `true`): spawn the replacement key.

## Build

Run `build.ps1` from this directory after BepInEx has generated the IL2CPP interop assemblies.

The installed plugin is `BepInEx/plugins/BreakRoomKey/BreakRoomKey.dll`.
