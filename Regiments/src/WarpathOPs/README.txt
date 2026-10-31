Warpath OPs v1.1.0 - Regiments (v1.5.0)
=======================================
Effectively unlimited Operation Points in Warpath mode. Your OP balance is
held at 99,999 whenever the regiment manager reads it, so point packs,
task forces, upgrades and platoon restores between battles never run out.
New Warpath runs also start at 99,999. (The counter shows five digits, so
99,999 is the most it can display.)

Requires BepInEx 5 (x64). Extract this zip into the game folder (the one
with Regiments.exe); it adds BepInEx\plugins\WarpathOPs.dll.

CONFIG (BepInEx\config\com.regiments.warpathops.cfg, created on first launch)
  Enabled      = true     turn the mod off without uninstalling
  TargetOPs    = 99999    the amount you are held at (max 99999)
  AlsoCampaign = false    also apply to the regular scripted campaigns

Existing Warpath saves are set to the target the next time you open the
regiment manager; a run saved by v1.0.0 at 999,999 drops to 99,999 so the
counter no longer wraps. The saved run keeps its balance if the mod is removed.
