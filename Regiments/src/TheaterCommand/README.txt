Theater Command v1.0.0 - Regiments (v1.5.0)
============================================
A WARNO Army General-style campaign for War Paths. You move NATO formations,
drawn as NATO unit counters, across a grid map with terrain, towns, rivers
and roads, against a Warsaw Pact AI. Every battle that breaks out on the map
is either fought as a normal Regiments battle or auto-resolved.

START
  War Paths > pick an empty slot > set Mode to "Theater Command" > Accept.
  The length option sets the number of turns (8, 12 or 16).

ON THE MAP
  Hover a unit       the cells it can reach light up; the strip at the
                     bottom shows strength, moves left, dug-in and supply
  Rest on a unit     it opens up beside the counter: every platoon with
                     how many are left (gold = under strength, red = gone)
                     and its veterancy. On an enemy: the platoon types its
                     battlegroup fields, most common first (the game picks
                     the actual mix when the battle starts)
  Drag a unit        move it; drag onto an enemy to attack (your win
                     chance is shown on each enemy while you drag)
  Double-click unit  open it in the game's regiment screen: reinforce
                     platoons, upgrade and buy task forces with its
                     Operational Authority; BACK TO MAP (or Esc) returns
                     and the formation keeps the changes
  Right-click a unit refit: it stays put and regains strength
  Ctrl+Z             undo a move
  Enter / NEXT TURN  the Pact moves (arrows show where), then the battles
                     on the front are marked with your win chance
  Click a battle     FIGHT (the regiment screen opens with that formation's
                     real roster, on the nearest town's battle map) or
                     AUTO-RESOLVE; Shift+click auto-resolves; Enter
                     auto-resolves all that are left
  Esc                back to the War Path slots

  The land changes hands as the front moves. Units cut off from their HQ
  are out of supply. Win by taking the Pact HQ, destroying every Pact
  formation, or holding more victory points (towns) when time runs out.

SAVES
  The campaign is stored beside the War Path save, in
  TheaterCommand_<slot>.json in the game's save folder. Ordinary War Path
  runs in other slots are unaffected.

Requires BepInEx 5 (x64). Extract this zip into the game folder (the one
with Regiments.exe); it adds BepInEx\plugins\TheaterCommand.dll. To remove
it, delete that file.
