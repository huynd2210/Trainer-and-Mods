CULTIC Lighter Brighter v1.1.0
==============================
A BepInEx plugin that makes the off-hand lighter actually usable as a light
source: brighter flame, much bigger radius, same warm color and flicker.
Also strengthens the pocket-lamp/flashlight beam. v1.1 targets the game's real
playerFlashlight component; earlier builds accidentally boosted the separate
playerLight while the lamp was active, so the visible beam did not improve.

Install (this game copy already has BepInEx):
  Copy LighterBrighterPlugin.dll into  CULTIC\BepInEx\plugins\
  Done - it loads automatically on next game start.

Use:
  F6 toggles the boost on/off (a small status line appears top-left).

Configure (BepInEx\config\local.codex.culticlighterbrighter.cfg,
created on first launch):
  [General]   EnabledOnStart        - boost active on launch (default true)
  [Hotkeys]   ToggleBoost           - toggle key (default F6)
  [Lighter]   IntensityMultiplier   - x vanilla flame intensity (default 3.5)
              RangeMultiplier       - x vanilla radius of 14 (default 2)
  [Flashlight] Enabled              - pocket-lamp beam boost (default true)
               IntensityTarget      - minimum on-state intensity (default 1.2)
               RangeTarget          - minimum beam range (default 20)

Notes:
  - Works in online co-op too: every player's lighter/pocket lamp brightens.
  - Turning it off (or stowing the lighter) fades smoothly back to vanilla -
    no light popping.

Build:
  .\package.ps1            - rebuild + install + zip packages into release\
  .\package.ps1 -SkipBuild - only repackage zips
