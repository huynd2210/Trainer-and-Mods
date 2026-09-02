CULTIC Minimap v1.4.0
=====================
A BepInEx plugin that draws a corner minimap built on the game's own automap
system - with NO fog of war (the whole map is revealed on every level load)
and live blips for enemies, items and key items.

The game builds its base automap from the AI navigation mesh, which omits some
player-only passages. v1.3 learns those gaps from real movement: whenever the
local player is grounded on a floor that has no matching baked map triangle, a
small floor cell is added. Learned cells are saved per level and loaded on later
runs, so the map improves permanently as missing areas are visited.

v1.3.1 also disables the native automap shader's center peephole while rendering
the corner minimap, flat 2D map, and the native map reached through this mod's
map cycle, preventing a large empty patch in the middle. Hide the panel with F8
to use the completely vanilla native-map mask behavior.

v1.4 renders learned movement-path cells with a dedicated unlit, double-sided
material. They no longer depend on the native shader accepting non-baked mesh
data and remain visible when the 3D map is tumbled above or below the floor.

The native rotatable 3D map can also show colored markers for living enemies,
ordinary pickups, and key/objective pickups. Each category has its own config
toggle; these markers exist only on the automap render layer and never appear
in normal gameplay.

What you see:
  - the full level map (the game's own automap geometry, fully revealed),
    viewed from a small camera hovering above your player
  - red dots   : living enemies (bigger/darker for bosses)
  - cyan dots  : pickups (weapons, ammo, health, armor...)
  - gold dots  : key items / objective pickups
  - white dot  : you (panel center)
  Blips outside the visible area clamp to the panel edge so you still get
  the direction.

Install (this game copy already has BepInEx):
  Copy MinimapPlugin.dll into  CULTIC\BepInEx\plugins\
  Restart CULTIC.

Use:
  F8 shows/hides the minimap panel (minimap is the default view).
  M cycles map views while the panel is on:
      1st press -> 2D full-map overlay (whole level, centered panel)
      2nd press -> the game's own rotatable map screen
      3rd press -> back to the minimap
  ESC immediately closes whichever overlay is open and returns to the
  minimap (it does not open the pause menu while an overlay is up).

  Note: while the minimap panel is visible, the mod owns the game's native
  map key, so M never opens the vanilla map on its own. Hide the panel
  (F8) to hand M and ESC back to the game; the vanilla "hold M for the FPS
  map" variant is replaced by the cycle's 2nd stage.
  The real map screen otherwise works exactly as before; this mod never
  touches gameplay state, it only renders.

Configure (BepInEx\config\local.codex.culticminimap.cfg, created on first
launch):
  [General]  EnabledOnStart   - visible on launch (default true)
  [Hotkeys]  ToggleMinimap    - toggle key (default F8)
             ToggleFullMap    - cycle key (default M)
             BackToMinimap    - close-overlay key (default Escape)
  [Panel]    SizePx           - panel edge in pixels (default 280)
             MarginPx         - distance from bottom-right edges (default 16)
  [Camera]   ViewWidth        - world units across the minimap / zoom
                                (default 34; lower = zoomed in)
             Height           - hover height of the minimap camera (default 160)
             RotateWithPlayer - rotate map so up = facing direction
                                (default false = north-up)
  [Reveal]   RevealFullMap    - no fog of war (default true)
  [Blips]    ShowEnemies      - default true
             ShowItems        - default true
             ShowKeyItems     - default true
             RefreshInterval  - seconds between enemy/pickup rescans (0.75)
  [3D Map Blips]
             ShowEnemies      - native 3D-map enemy markers (default true)
             ShowItems        - native 3D-map ordinary pickups (default true)
             ShowKeyItems     - native 3D-map key/objective pickups (default true)

Notes:
  - Works per-scene: the map is re-revealed and markers refreshed whenever a
    level loads (including special cases like the moving train level).
  - In online co-op it follows YOUR player character only.
  - If a custom map's automap layers differ, the minimap may show only part
    of the overlay; check BepInEx\LogOutput.log for "Found automap" /
    warning lines from this plugin.
  - Learned map patches are stored in
    BepInEx\config\CulticMinimapMaps\<scene>.cmm. Saves are versioned and
    written atomically on scene changes, game exit, and shortly after learning.
  - package.ps1 automatically includes accumulated .cmm files in both release
    zips under BepInEx\plugins\CulticMinimapMaps, so a completed set can be
    distributed without requiring other players to retrace the same gaps.

Build:
  .\package.ps1            - rebuild + install + zip packages into release\
  .\package.ps1 -SkipBuild - only repackage zips
