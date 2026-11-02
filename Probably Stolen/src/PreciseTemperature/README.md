# Precise Temperature Control

Set the Heating Plate to an exact temperature; it heats up to it, stops, and holds it.

- The **-** and **+** next to *Target temperature* are always there, even without Chemical
  Equipment Modules. A click changes the target by **1 °C**, a **Shift**-click by **10 °C**.
  (Vanilla locks the target at 99 °C with no modules and 160 °C with one, and only allows
  5 °C steps from two modules.)
- The burner stops at the target and **holds it exactly**: the liquid no longer sags a
  degree or so below it from cooling. Lower the target and the burner idles until the liquid
  has cooled down to it.
- Still set by the modules: the highest target (**99 °C** without modules, **160 °C** with
  one or more) and how fast it heats (Low/High, faster with 3 and 4 modules).
- Heating plates you already own get it too, and the target is kept when you save.

Handy targets: 45 °C for Dream/Nightmare extraction, 52 °C for Nomore, 75 °C for caffeine,
125 °C for Phanta (needs a module, since it's above 99 °C).

## Install

1. Extract the zip into the game folder (the one with `Probably Stolen.exe`), so you get
   `Mods\PreciseTemperature\PreciseTemperature.dll` and `manifest.xml`.
2. Start the game, open **Mods** from the main menu, enable **Precise Temperature
   Control**, restart.

Uses the game's built-in mod loader; nothing else is needed. Made for v0.46D. It replaces
how the Heating Plate is built, so another mod that changes the Heating Plate may clash.
