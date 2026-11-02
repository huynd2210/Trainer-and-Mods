# In-game seed aids

Installed directly into Golden Krone Hotel's `package.nw/index.html` with one additional
script, `package.nw/js/seed-aids.js`. Launch the usual Steam game / `gkh.exe`.
Aids activate automatically in a living, non-tutorial run. The panel stays to the
left of the potion sidebar, including when collapsed.

Restart an already open game to load the current death guard and bridge tools.
Re-evaluating an older version-2 script in the console does not install the new
hooks. Read `LLM-SEED-SOLVE-GUIDE.md` for direct LLM control of the harness.

- **Ctrl+Z**: undo; **Ctrl+Y** or **Ctrl+Shift+Z**: redo. Buttons work too.
- **Foresight**: the nine-button compass runs the actual next movement/attack or
  wait from the current RNG state, restores the position, and reports player HP
  changes, outgoing damage, kills, misses, noticed enemies, or a pending choice.
  Damage/notice warnings also appear over the corresponding game tiles. Map labels
  use H/K for hit/kill damage and red numbers for player HP loss. Click a compass
  button to perform that action. Fatal forecast buttons are disabled and checked
  again before playing; ordinary keyboard inputs remain unrestricted.
  Diagonal forecasts require the game's
  diagonal movement option. **Ctrl+Shift+F** toggles foresight.
- **Omniscience**: a full current-floor map shows real terrain, items and creatures.
  Unseen tiles are dim. Hover to inspect tile/item names and creature HP; carried
  potion identities are listed below. This does not identify potions in the game
  or change player/enemy visibility. **Ctrl+Shift+O** toggles omniscience.
- **− / +** collapses or expands the panel. Scroll inside it for the map/footer.

Undo restores the world, inventory, statuses, monsters and RNG, including across
floors and death. Menus/targeting choices belong to the same action; undo can cancel
an unfinished choice. Redo refuses to restore a recorded death.
History keeps the last **48 positions in this session**, limiting retained snapshot
memory during long runs.
Hypothetical monster spawns dispose their native canvases and textures after
the preview, without adding them to the live game's graphics ownership.
The panel repaints only when its state changes. Collapsed or background panels
do not compute hidden display forecasts. Movement foresight and simple move/wait
death checks restore the active
floor, shared live objects, floor discovery headers and affected destination
floors; special floor-changing encounters, arbitrary guarded inputs and undo/redo
retain full-world restoration.
Playing a different action after undo replaces the redo tail. Closing or reloading
the game discards history; ordinary saves preserve the RNG and aid activation.
Older saves have no saved RNG position; forecasts start from their loaded state.
Foresight covers one movement/attack or wait, not arbitrary multi-step plans.
Use undo to try spells, potions, stairs or other actions. A confirmation is shown
as a pending choice, not guessed past the choice.

Achievements, morgue exports, normal save behavior and permanent progression
remain enabled for real actions. Only speculative foresight simulations suppress
these side effects. Earned permanent progression is retained when undoing gameplay.

The approach follows the local BrogueCE `tools/seed-lab` reference: real simulation
plus snapshots, with an observation-only map. This implementation uses JavaScript
object graphs, shared snapshot records, and ROT RNG state rather than C heap pages.
It adds no separate service, game window, or launcher.

Verification used the shipped engine in an isolated headless Chromium browser:
unchanged world/FOV/RNG after previews; deterministic undo/replay/redo; floor
transitions; death recovery and cancellation of delayed death callbacks; unfinished
menu cancellation; combat forecast versus actual damage; save/load; map
hover, keyboard toggles, panel collapse and scroll reachability. An independent
agent inspected the actual rendered screenshots. Live-update checks also exercise
the game's console, preserve the existing run and verify real achievement calls
and progression writes while suppressing those calls during previews.
Live NW.js/Steam gameplay remains
for the user to assess; the automated tests do not take over the desktop.

## Remove the modification

Close the game, then remove the line `<script src="js/seed-aids.js"></script>` from
`package.nw/index.html`. The unused script can remain on disk. Alternatively restore
`mod-backup/index.html` if you have made no other changes to the HTML since installation.
The original HTML was backed up before editing. Steam updates/file verification
may replace the modified HTML, requiring the script line to be re-added.

## Repeat automated verification

From this game directory, with Python and Playwright available:

```powershell
python -m http.server 8765 --bind 127.0.0.1 --directory package.nw
```

In a second terminal:

```powershell
python mod-tests/verify.py
```

The browser uses its own isolated local storage and does not load or write your
NW.js save directory, cloud storage or Steam account.
