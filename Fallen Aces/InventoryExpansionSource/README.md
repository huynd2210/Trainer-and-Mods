# Fallen Aces Expanded Inventory

Installed for the game in this directory. Requires the existing BepInEx 5 installation.

- Fifteen general-purpose inventory slots by default, plus any earned ammo-pouch/lunchbox slots.
- Mouse wheel cycles through every slot in either direction, wrapping at the ends.
- Existing slot bindings continue to work. Keys 6, 7, 8, 9 and 0 select slots 6–10. Use the wheel for higher slots.
- A compact text list on the right shows items, stacks, gadget restrictions and selected slot. The original HUD remains available.
- Additional capacity is applied after level loading and native save restoration finish. Saved extra slots are retained; reducing the setting does not remove slots or items from an existing save.

Configuration: `BepInEx/config/local.codex.fallenacesexpandedinventory.cfg`. Set `NormalSlots` from 3 to 20 and restart. `ShowInventoryList` hides or shows the added list.

## Verification

Compilation against this installation's managed assemblies passed. `verify.ps1` checks forward/backward traversal, wrapping, holstering, zero input and empty inventories. A hidden headless game launch passed native slot creation, gadget-index preservation, repeated expansion, non-shrinking capacity, native empty-slot save serialization/deserialization and Harmony initialization alongside Kill Tracker and SuperHot.

No hands-on gameplay or visual assessment was performed. Empty-slot serialization does not verify persistence of actual carried items.

## In-game acceptance test

1. Launch normally and load a level. Keep your prior save; use a separate manual save for this test.
2. Confirm the list contains 15 general slots, plus any gadget slots. Collect at least four distinct non-stacking items and then fill the extra general slots.
3. Scroll both directions through all slots; select, use and drop an item from a slot above 3. Check keys 6–8 and holster/unholster.
4. Save into the separate slot, reload it and confirm the extra items and gadget types remain correct.
5. If you have a lunchbox or ammo pouch, remove/re-equip it and confirm the remaining items and selection stay correct. If possible, check a level transition too.

Report any missing items, unreachable slots, overlapping HUD text, or errors, and the step where it happened. Logs: `BepInEx/LogOutput.log`.

## Build and removal

Run `./InventoryExpansionSource/build.ps1` from PowerShell to compile and install; `-NoInstall` only builds. Run `./InventoryExpansionSource/verify.ps1` for scroll tests. .NET SDK required for the plugin build.

To uninstall, close the game and remove only `BepInEx/plugins/FallenAces.ExpandedInventory.dll`. Resume a save made before using the mod: expanded saves retain their slot count, while the unmodified controls/HUD do not fully support those slots.

Game assemblies are not modified. The distribution includes only this plugin and its source; the inspection directory contains local decompiler output and is not part of the package.

