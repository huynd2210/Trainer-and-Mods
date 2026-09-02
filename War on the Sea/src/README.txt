War on the Sea Trainer v1.3.0
=============================
A BepInEx trainer for War on the Sea v1.09a.

FEATURES
--------
F1  Infinite Command Points  (toggle)
    Campaign purchases (task forces, ships, etc.) never fail and the
    command-points pool never depletes. Player-only - the enemy AI budget
    is unaffected.

F2  Reveal All / no fog of war  (toggle)
    Removes fog of war EVERYWHERE:
      - Campaign map: all enemy units permanently visible (full opacity,
        always spotted), with the EXACT enemy composition in tooltips
        (true ship counts, not the game's fuzzed estimate).
      - Tactical battles: all enemy units detected and identified, so they
        show on the tactical map, can be targeted, and their classes are
        known - from the moment the battle starts.
    Player-side only - it does not make your units more visible to the AI.
    Toggle off to let the fog return.

A small on-screen status line (top-left) shows the active cheats.
Notifications are also written to BepInEx's log (game\BepInEx\LogOutput.log).

INSTALL (for users who ALREADY run BepInEx 5 on this game)
----------------------------------------------------------
1. Copy this zip's BepInEx folder into the game folder, next to WarOnTheSea.exe.
2. Launch WarOnTheSea.exe.
3. Open/load a campaign, then press F1 and/or F2.

No existing game files are modified or overwritten.
To remove: delete game\BepInEx\plugins\WoTSTrainer.dll.
