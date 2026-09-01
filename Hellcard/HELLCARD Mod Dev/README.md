# HELLCARD Overpowered Hero local mod

This builds the editable mod copy into `C:\Games\HELLCARD\ccg_mod` and mirrors
the runtime overrides into `C:\Games\HELLCARD\ccg`.

The normal HELLCARD executable mounts the `ccg` loose folder after `ccg.pac`.
The `ccg_mod` folder is the workspace used by the separate Modding Tools
executable and is not mounted by the normal game in this installation.

Changes:

- Warrior, Rogue, Mage, Tinkerer, and Bruja start with 9000 HP.
- Glowing Crystal becomes a free, persistent starter artifact. The normal game
  retains its original **Glowing Crystal** label because mod language overlays
  are handled through Workshop metadata.
- God Mode Crystal supplies 1000 mana every turn through the game's built-in
  `BCCGAddManaInfluence`.

Rebuild:

```powershell
python "C:\Games\HELLCARD\HELLCARD Mod Dev\build_overpowered_mod.py"
```

Install into the packed archive used by this non-Workshop build:

```powershell
python "C:\Games\HELLCARD\HELLCARD Mod Dev\patch_ccg_pack.py"
```

This creates `C:\Games\HELLCARD\ccg.pac.original` before changing the archive.
Restore the untouched archive with:

```powershell
python "C:\Games\HELLCARD\HELLCARD Mod Dev\restore_original_ccg_pack.py"
```

Remove the runtime override by moving or deleting `C:\Games\HELLCARD\ccg` while
the game is closed. The `ccg_mod` folder can remain as source files.
