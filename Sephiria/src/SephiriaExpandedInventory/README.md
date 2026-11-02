# Sephiria expanded inventory (v1.0.33)

Adds four rows of six slots (24 slots) to each player inventory. The existing inventory panel stays the same size; its contents clip to the viewport and scroll vertically with the mouse wheel or controller selection. Change `ExtraRows` in `SephiriaExpandedInventory.cfg` from 1 to 16 before starting the game to choose a different amount.

The mod is installed in this game copy. To reinstall after changing game binaries, close the game and run `install.ps1`. To remove the mod, close the game and run `uninstall.ps1`. The patcher removes only its own calls from `Assembly-CSharp.dll` and preserves the other installed mods. It keeps a recovery copy of the immediately previous assembly at `Assembly-CSharp.dll.expandedinventory-last`.

Extra slots are added when a player avatar starts on the server. In multiplayer, the host needs this mod for inventories to expand. Removing the mod during a saved run with items in extra slots may make those items inaccessible; finish the run or empty the extra rows first.
