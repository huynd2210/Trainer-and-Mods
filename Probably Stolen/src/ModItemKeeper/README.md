# Mod Item Keeper

**Remove a mod, keep its stuff.** Without this, loading a save with a mod removed turns that mod's
items into empty "unknown" placeholders and throws away whatever was inside them. With it, the
contents are kept with the save and come back when the mod does.

What you see:

- **Mod removed:** its items show as unknown placeholders, as before. Anything you had stored
  *inside* them (e.g. vanilla items in a modded crate) is kept safe, out of sight.
- **Mod reinstalled:** the placeholders turn back into the real items (the game already does
  that), and the kept contents go back inside them, where they were, with their features.
- **Container gone meanwhile** (you sold or trashed the placeholder): the kept contents land in
  your inventory instead. If there is no room, the rest are tried again on the next load.
- The morning report says when items were kept, put back or put into your inventory.

## Install

Copy `Mods\ModItemKeeper` into the game's `Mods` folder. Keep it enabled **before** you remove
another mod and while that mod is out: it only protects loads it is present for.

## Limits

- Items of the removed mod that sit loose (not inside anything) already survive as placeholders
  and come back on their own; this mod handles what was *inside* them.
- Uninstalling Mod Item Keeper while it is holding items leaves them in the save, unused, until it
  is installed again.
- If you quit without saving after a load, nothing changes: the kept items and the save are
  written together.

## Build

```
dotnet build -c Release -p:GameDir="C:\Program Files (x86)\Steam\steamapps\common\Probably Stolen"
```

Offline test of the save surgery against real saves (reads saves only):

```
dotnet run -c Release --project Tests -- "<path>\save_0.es3"
```
