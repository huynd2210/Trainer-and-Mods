# Auto-missile Defense

BepInEx 5 mod for Nuclear Option 0.33.4. Installed plugin:
`BepInEx/plugins/AutoMissileDefense/NuclearOption.AutoMissileDefense.dll`.

## Use

Carry an IR missile such as MMR-S3. Press **F8** while flying, or click the
**AUTO-MISSILE DEFENSE** label on the cockpit HUD when the cursor is available.
The control clones the game's HUDOptions toggle prefab and uses its native font
and ON/OFF colors. ON is green; OFF is grey. It starts OFF each time you enter
a different aircraft unless configured otherwise.

While ON, an active SARH or ARH missile targeting your aircraft triggers one
defensive IR missile when it is strictly within the configured range and in the
front hemisphere of your aircraft. The nose direction defines forward, not the
camera or velocity vector. The exact 90-degree dividing plane is excluded.
Nearest eligible threats are handled first, at most one per frame, using a ready
onboard IR missile station. All native IR missile types are eligible, including
MMR-S3. Missiles targeting someone else and incoming IR missiles do not trigger it.

Launch is requested on the first eligible frame; normal station firing intervals,
reloads, landing-gear/ground safety, bay doors, and rail delays still apply.
One defensive round is allocated per incoming missile, even across OFF/ON toggles;
there is no automatic retry if that shot misses. Your selected weapon and manual
target list are preserved. No ammunition is created. Native IR guidance, line of
sight, target heat signature, maneuverability and damage determine whether the
intercept succeeds. The defense does not guarantee a kill.

## Configuration

Edit `BepInEx/config/nuclearoption.automissiledefense.cfg` with the game closed:

```ini
[Controls]
ToggleKey = F8

[Defense]
EngagementRangeMetres = 5000
EnabledOnEnteringAircraft = false
```

Range is in metres, between 1 and 100000. Restart after editing. BepInEx shortcut
syntax also supports modifiers, e.g. `F8 + LeftControl`.

## Manual test

1. Start a single-player mission carrying MMR-S3, take off and retract the gear.
2. Verify the cockpit shows OFF, press F8 and verify it changes to ON. Check that
   the label is readable and does not overlap other instruments at your resolution.
3. With an enemy SARH/ARH missile targeting you inside 5 km, point your aircraft
   nose into its hemisphere. Expect one automatic launch and one less IR missile.
4. Keep the same threat ahead: it should not repeatedly drain ammunition. A new
   threat should receive its own shot once a station is ready.
5. Repeat with defense OFF, with the threat behind you, and outside the configured
   range: expect no automatic launch. Look sideways without turning the aircraft:
   camera direction should have no effect.
6. Check that your manually selected weapon and targets remain selected. Test the
   clickable label when the game's cursor is available. Exit/enter an aircraft and
   check that the configured initial state returns.

Report aircraft/loadout, ON/OFF state, missile type, approximate range/bearing,
whether a defensive missile launched, and whether it tracked. If there is a
problem, include `BepInEx/LogOutput.log`; successful requests log `Defensive shot:`.

## Validation and limits

- Built against the installed game's assemblies; 633 deterministic eligibility
  checks pass, including angular/range boundaries and exclusion conditions.
- BepInEx loaded the plugin and installed its Harmony hooks in an offline run.
- Offline asset inspection confirmed MMR-S3 and other native IR loadouts. The
  actual native control was rendered in both states; live cockpit placement,
  input and interception have not been verified. Further visual testing was
  deferred to the user as requested.
- Multiplayer uses native tracking and launch commands and is **untested**.
  Host-authoritative IR guidance and target replication may affect behavior;
  do not assume a successful local launch proves a server-side intercept.
- Build emits an assembly-version warning for the game's Mirage dependency on
  System.IO.Compression (4.2 versus the SDK's 4.1.3 reference). It compiles, and
  plugin loading succeeded under the game's Mono runtime.

## Rebuild / uninstall

Run `./build.ps1 -Install` from this folder with Nuclear Option closed. The build
runs the rule tests before installing. Standard builds exclude the offline QA
driver. Its source remains available via `-p:IncludeOfflineQA=true` for development.

To uninstall, remove only `BepInEx/plugins/AutoMissileDefense`. The config may be
kept for a later reinstall. This mod does not overwrite game assemblies or assets.
