# Sushiamo mods

Two mods for Sushiamo 1.0.0 (Godot 4.6). The game has a mod loader built in: copy a mod's zip from
`dist\` into `<game folder>\mods\` and start the game. Remove the zip to turn it off.

- **Best Hand** (`BestHand.zip`): gold numbers over the cards that score highest, in plate order, with
  the exact score and whether it beats the day's target. Ctrl+B selects that hand, Ctrl+H hides the hints.
  The score comes from the game's own scoring code, run on copies with the same dice rolls.
- **Unlimited Slots** (`UnlimitedSlots.zip`): hold any number of tools and sauces. Counters show
  held / normal capacity (e.g. `9/5`). Zen Garden, Sake, Fukubukuro and the fill-sauce-slots reward still use
  the normal capacity, so they don't scale with the lifted cap.

`src\<Mod>\` holds each mod's source and its `package.py` (builds both zips; `--install` copies the mod
into the game). Each mod's own README has the details.
