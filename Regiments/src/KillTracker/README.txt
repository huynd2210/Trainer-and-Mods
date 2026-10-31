Kill Tracker v1.0.0 - Regiments (v1.5.0)
========================================
Writes a text file for every battle: each kill your units score and each
loss they take, as it happens, plus a summary when the battle ends.
Allied AI commanders' kills and losses are left out.

Files go to the KillTracker folder next to Regiments.exe, one per battle:
  KillTracker\2026-10-31 14-32 <map name>.txt

What a file looks like:
  [12:41] KILL M1A1 Abrams [2-1] destroyed T-72B [3-1]
  [12:58] KILL M2A2 Bradley [1-2] killed 4 soldiers of Motor Rifles [3-2]
  [13:05] LOSS 2 soldiers of Infantry [1-3] killed by BMP-2 [3-3]
  [14:10] LOSS M113 [1-4] WIPED OUT by Tac strike
  ...
  YOUR KILLS:   23 vehicles, 2 helicopters, 187 soldiers  |  6 platoons wiped out
    By type / By your platoon ...
  YOUR LOSSES:  ...

Notes
  - Soldier kills come in bursts; hits by the same shooter on the same
    target within a few seconds share one line.
  - "Vehicles" includes towed guns. "Tac strike" is off-map support
    (artillery and air strikes you called in).
  - You hitting an allied unit is listed as FF and not counted as a kill.
  - The file is written live, so you can open it during a battle.
  - The game's own after-battle report is unchanged. It credits a killer only
    for vehicles; this log also credits each soldier killed.

Requires BepInEx 5 (x64). Extract this zip into the game folder (the one
with Regiments.exe); it adds BepInEx\plugins\KillTracker.dll.

CONFIG (BepInEx\config\com.regiments.killtracker.cfg, created on first launch)
  Enabled = true          turn logging off without uninstalling
  Folder  = KillTracker   where the files go (relative to the game folder,
                          or a full path)
