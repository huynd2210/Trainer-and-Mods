# Shared Storage

Every save gets a **Shared Storage Bay**: the storage bay with the cyan lid. Whatever you
put in it is shared by all your saves and carries over into new runs. Stash something
before a run ends, and it is waiting in the bay when you start the next one.

- It is one bay seen from every save, not a copy machine. Take an item out in one save
  and it is gone from the bay in all the others.
- The contents sync each time the game saves (at the end of each day). Quit without
  saving and the bay is back to how it was at the last save.
- It is a normal 10x10 storage bay otherwise, and takes what a storage bay takes.
- Sell or trash the bay and what was inside goes with it. The next time you load, a new,
  empty bay is waiting in your inventory.
- Restoring an older backup of a save does not change the bay, but it can bring back an
  item you had since moved into it.

The contents are kept outside your save slots, in the game's data folder:
`%USERPROFILE%\AppData\LocalLow\Questing Goose Studio\Probably Stolen\ModData\SharedStorage\shared-storage.json`, with
the version from one save earlier next to it. If that file can't be read, the bay says so
in its tooltip and nothing gets overwritten.

## Install

1. Extract the zip into the game folder (the one with `Probably Stolen.exe`), so you get
   `Mods\SharedStorage\SharedStorage.dll` and `manifest.xml`.
2. Start the game, open **Mods** from the main menu, enable **Shared Storage**, restart.

Uses the game's built-in mod loader; nothing else is needed. Made for v0.46D.
