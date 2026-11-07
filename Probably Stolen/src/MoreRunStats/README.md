# More Run Stats

Adds a **Shady Deals** tab to the summary you get when a run ends (won or lost):

- **Contraband Sold**, split the way the game's security record splits it: drugs,
  weapons, access cards and other contraband, each with credits earned and item count.
- **Scam Profit**: how much more you got for mislabelled goods than they were really
  worth. Sell a counterfeit cigarette labelled genuine for 140 when it would fetch 100
  labelled counterfeit, and that's 40. Also the number of mislabelled items sold and
  your biggest single scam.
- **Stolen Goods Sold**: credits earned and item count.

Counter sales, the vending machine, the water fountain and request deliveries all count.
A request pays the same whatever the label says, so mislabelled deliveries add no scam
profit. Items you sell inside a container are counted one by one.

The totals are stored in your save. Sales made before you enabled the mod aren't known,
so an existing run starts counting from zero.

## Install

1. Extract the zip into the game folder (the one with `Probably Stolen.exe`), so you get
   `Mods\MoreRunStats\MoreRunStats.dll` and `manifest.xml`.
2. Start the game, open **Mods** from the main menu, enable **More Run Stats**, restart.

Uses the game's built-in mod loader; nothing else is needed. Made for v0.46D.
