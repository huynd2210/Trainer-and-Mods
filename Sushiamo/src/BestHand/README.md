# Best Hand (Sushiamo mod)

Shows which cards in your hand make the highest score, and what that score is.

- **Gold numbers above cards**: the best hand. The number is the card's position on the plate
  (left to right). If the numbers are out of order, drag the cards so they are, or press Ctrl+B.
- **Line above the hand**: `Best 12,345 (wins)` is the best score and whether it clears the day's
  target. `Selected 9,876` is what your current selection would score.
- **Ctrl+B** selects the best hand (and reorders it if order matters). **Ctrl+H** hides/shows the hints.

The score is exact: the mod runs the game's own scoring code on copies of your cards, with the same
dice rolls the real Make will get, then puts everything back. Every card set is tried; reorderings are
tried for the top 3 sets (order matters for cards such as Eel or Squid).

## Install

Copy the mod's zip into `<game folder>\mods\` and start the game. The game has a mod
loader built in; no other install is needed. Remove the zip (or rename it to `.zip.disabled`) to turn
it off.

## Checking it

After each Make, the mod writes the predicted and the actual score to
`%APPDATA%\Sushiamo\logs\modloader.log` (`BestHand: Make scored X, as predicted.`). A line saying
`but Y was predicted` means a card combination it gets wrong; please report the cards and tools held.
