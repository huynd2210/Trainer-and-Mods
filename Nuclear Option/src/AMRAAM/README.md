# AIM-120 AMRAAM — Nuclear Option 0.33.4

Version **1.1.0**: the loadout chooser now shows one AMRAAM option per missile count on each hardpoint. The duplicate names in 1.0.0 were variants copied from both stock ARH families. All old mount keys remain registered for save compatibility.

Adds a separate **AIM-120 AMRAAM** air-to-air missile to aircraft hardpoints that accept mounted ARH missiles. Existing weapons remain available. The mod uses the supplied `amraam_inspired.blend` geometry and a baked texture from its procedural painted materials.

## Gameplay

- Active radar homing, with the game's datalink, terminal seeker, chaff/jamming response, launch sequence, proximity fuze and damage system.
- **$1,000,000 per missile**, **140 km nominal engagement range**, **Mach 4 airspeed ceiling**. Rack costs are additional, as for stock weapons.
- 161.5 kg loaded mass, approximately 3.65 m length, 20 kg gameplay blast yield and 40 G steering limit.
- Up to **20 km horizontal launch distance**: normal direct/lead homing.
- Beyond **20 km**: a parabolic altitude profile with a gradual climb and descent. Rise scales with launch distance, up to 18 km above the straight launch-to-target altitude profile.
- Within **12 km slant range**: commit to normal ARH terminal interception. The missile cannot re-enter its loft after this transition.
- Guidance uses the stock seeker's known track. If fired without a target, or if its target is lost/destroyed, the missile flies forward and performs an active radar scan every 0.2 seconds after its guidance delay. It acquires the **first detectable enemy aircraft or missile** inside the seeker's forward cone and radar range. Simultaneous detections use the game's unit-list order, not closest-target priority. Friendly and unassigned-faction units are excluded. Terrain, radar horizon, radar return strength and ECM still matter.
- A self-acquired target uses direct terminal homing; the missile keeps that target while its track is valid. Search continues after motor burnout while useful speed remains, with a 240-second total search lifetime. Normal terrain collision and low-speed cleanup still apply.

The boost/sustain motor is tuned for the requested game envelope. It does not reproduce the real AMRAAM motor. The game computes the Mach cap from altitude; Mach 4 is about 1,360 m/s at sea level and 1,160 m/s high up. The nominal metadata uses the linked specification's 1,372 m/s figure. Range is an engagement envelope, not a guarantee against every target aspect, altitude or countermeasure.

Source reference: [AIM-120 AMRAAM specifications](https://en.wikipedia.org/wiki/AIM-120_AMRAAM). The requested $1 million and 140 km values take precedence over variant-specific figures. The supplied asset has AIM-120B markings, retained as supplied.

## Installed files

The mod is installed in `game/BepInEx/plugins/AMRAAM/`:

- `NuclearOption.AMRAAM.dll`
- `amraam.meshbin`
- `amraam-paint.png`

Restart the game normally. Choose **AIM-120 AMRAAM** in a hardpoint that offers an ARH missile. Rack variants keep their stock rail/bay layout and round count. Defaults do not automatically replace previously saved loadouts.

Verified compatible aircraft definitions: **T/A-30 Compass, FS-12 Revoker, FS-20 Vortex, KR-67 Ifrit, and Alkyon AB-4**.

Settings are generated in `game/BepInEx/config/nuclearoption.amraam.cfg`. Edit while the game is closed:

```ini
[Trajectory]
LoftBeyondMetres = 20000
MaximumRiseMetres = 18000
TerminalRangeMetres = 12000
```

To uninstall, close the game and move the AMRAAM plugin folder outside `BepInEx/plugins`. Saved loadouts containing this custom weapon should be changed back to stock weapons first. Network players must have the same mod/version and trajectory settings; multiplayer compatibility has not been playtested.

## Verification and gameplay handoff

The plugin was compiled against this installation and loaded in an automatic hidden game run. Registration produced 14 mount variants across 36 hardpoint options. The game verified price, mass, range metadata and model rendering. The corrected rendered model passed independent visual review. The trajectory executable passed 46 assertions covering the direct/loft threshold, bounded height, altitude endpoints, non-reversing progress and close/long approach paths.

These are **registration, rendering and kinematic checks**, not proof of successful live missile interception. The game's real aerodynamics, hit probability, aircraft/bay fit, launch clearance, exhaust attachment and multiplayer behavior need gameplay assessment.

1. Start a single-player mission and select an ARH-capable aircraft. Confirm AMRAAM is offered, its displayed cost is $1.00m per round, and it fits the selected hardpoint.
2. Fire at an airborne target about 5–10 km away. Expect direct homing without an intentional loft.
3. Fire at a tracked airborne target about 50–80 km away with sufficient altitude and visibility. Observe the missile climb well above the direct path, crest, then descend toward the target.
4. Check the terminal transition, hit/miss, smoke origin, rearming and whether stock ARH weapons still work.
5. For a long-range assessment, test a 120–140 km tracked target from altitude; report launch/target altitude, target direction, peak missile altitude and whether it keeps its track.
6. Confirm each hardpoint shows only one AMRAAM entry for each count (single, x2, etc.). Fire without a target selected toward an enemy aircraft. It should fly forward, acquire the first enemy the seeker detects, then home. Repeat with only a friendly ahead, and with an enemy outside its forward cone or behind terrain: it should keep searching.

Report the aircraft/hardpoint, approximate ranges and what happened. Diagnostics are in `game/BepInEx/LogOutput.log`; look for `AMRAAM registered`, `AMRAAM launch: DIRECT`, `AMRAAM launch: LOFT` and `AMRAAM terminal`.

## Rebuild

Run `build.ps1` from this source folder with .NET 8 installed. Add `-Install` to copy the resulting plugin and assets into the current game. Close the game before installing. `package.ps1` builds, tests and writes `AMRAAM.zip`, `AMRAAM-Pack.zip` (with this install's BepInEx) and `AMRAAM-Source.zip` to `release`.

The project expects to sit at `<game>\ModSource\AMRAAM`: it references the game and BepInEx assemblies by relative path. `AMRAAM-Source.zip` leaves out `assets\` to save space. Restore `amraam.meshbin` and `amraam-paint.png` from `AMRAAM.zip` (`BepInEx\plugins\AMRAAM\`), or regenerate them with the exporter below.

The model exporter uses Blender 5.1 in background mode. It reads the original file without overwriting it, applies evaluated mesh geometry, converts to Unity axes, bakes the procedural diffuse paint to a 2048-pixel atlas and exports the runtime mesh. To regenerate:

```powershell
& 'C:\Program Files\Blender Foundation\Blender 5.1\blender.exe' -b -t 6 --python .\export_model.py
```

`tests/TrajectoryTests.csproj` shares the production `LoftProfile.cs`. The optional `-amraam-smoke` game argument enables a one-shot hidden-run QA capture and automatically quits after the main menu finishes loading. It is inactive during normal launches. Evidence is under this source folder's `evidence` directory. The build emits one existing game-reference `System.IO.Compression` version-resolution warning; the runtime loader check passed.
