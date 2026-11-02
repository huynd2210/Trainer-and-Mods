# Instant Evaporator

The Evaporator does a whole night's work every second instead of once a night.

- Every second (one game tick) it runs one night's evaporation: about **300 ml**, more with
  performance modules. A typical batch is dry within a few seconds.
- **Power per litre is unchanged.** Each second it works costs one night of Energy Cell
  power. An empty or already dry evaporator uses nothing, so you can leave the cell in.
- Distillate and the **Solvent Recovery Module** work as before, collecting the same share
  each second they would collect each night.
- It also keeps working overnight like the vanilla machine.
- Evaporators you already own get it too; nothing new to buy.

Module effects that trigger "on use" fire once per second of work instead of once per
night, in line with the work done.

## Install

1. Extract the zip into the game folder (the one with `Probably Stolen.exe`), so you get
   `Mods\InstantEvaporator\InstantEvaporator.dll` and `manifest.xml`.
2. Start the game, open **Mods** from the main menu, enable **Instant Evaporator**, restart.

Uses the game's built-in mod loader; nothing else is needed. Made for v0.46D.
