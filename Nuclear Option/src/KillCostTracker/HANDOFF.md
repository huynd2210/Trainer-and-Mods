# Kill and Cost Tracker — per-weapon JSON/TXT persistence installed

Last updated: 2026-09-01 (Asia/Bangkok)

## Status

**Version 1.4.0 is installed. The overlay, F7, kill tracking, aggregate weapon costs, v1→v2 migration, JSON persistence, TXT mirror, and v2 reload are validated. A fresh in-mission shot is still required to populate and visually inspect the new per-weapon records.**

The root cause was confirmed from file timestamps and then directly logged by 1.3.4: Nuclear Option destroys `BepInEx_Manager` during its initial scene load. Earlier builds handled the plugin component's `OnDestroy` as final shutdown, which unpatched Harmony, deleted the standalone runtime and Canvas, and cleared the singleton. Version 1.2.x also used Unity's overloaded null comparison, so its surviving runtime regarded the destroyed plugin component as null and did no work.

Version 1.3.4 kept the managed tracker controller and Harmony patches alive after this premature destruction, drove updates from a separate `DontDestroyOnLoad` runtime object plus game-owned update fallbacks, and created the Canvas only on the first active game frame. Version 1.3.5 also changes every gameplay Harmony patch to use `ReferenceEquals`: those patches still used Unity's overloaded `plugin != null`, so they skipped every event after the plugin host was destroyed.

## Requested behavior

The user requested a persistent kill and cost tracker with three top-level categories:

1. Direct local-player kills and the total value of what the player killed.
2. One category for each faction, tracking the faction's kills and destroyed value.
3. Weapon expenditure included in costs: missiles fired, bombs dropped, and other fired rounds.

Totals must persist across missions and game restarts. F7 is intended to show/hide the overlay. The tracker should be visible by default in both the main menu and missions.

## Environment

- Game root: `C:\Games\Nuclear.Option.v0.33.4\game`
- Nuclear Option: 0.33.4
- Unity: 2022.3.62f2 (log reports `2022.3.62.7762112`)
- Runtime: Mono / CLR 4.0.30319.42000
- BepInEx: 5.4.23.5
- Display observed in the user's screenshot: 3840x2160
- `BepInEx\config\BepInEx.cfg`: `HideManagerGameObject = false`
- No other BepInEx plugins are currently loaded; the log says `1 plugin to load`.

## Current files

- Source: `ModSource\KillCostTracker\KillCostTrackerPlugin.cs`
- Classic build script: `ModSource\KillCostTracker\build-classic.ps1`
- Project file (older SDK-style build metadata): `ModSource\KillCostTracker\KillCostTracker.csproj`
- Installed DLL: `BepInEx\plugins\KillCostTracker\NuclearOption.KillCostTracker.dll`
- Config: `BepInEx\config\nuclearoption.killcosttracker.cfg`
- Persistent stats: `BepInEx\config\nuclearoption.killcosttracker.stats.json`
- Current installed version: 1.4.0
- Installed DLL SHA-256: `106C61FC57233696F465817B0C3C708775F6CD806C1D15B406BE6F1BF05AA02A`
- Preserved prior DLL: `BepInEx\plugins\KillCostTracker\NuclearOption.KillCostTracker.1.3.3.dll.bak`
- Preserved prior DLL: `BepInEx\plugins\KillCostTracker\NuclearOption.KillCostTracker.1.3.4.dll.bak`
- Preserved prior DLL: `BepInEx\plugins\KillCostTracker\NuclearOption.KillCostTracker.1.3.5.dll.bak`

The stats file is valid version-1 JSON and currently contains zero totals. It was preserved through all UI attempts.

## Previous failing evidence (1.3.3)

The latest `BepInEx\LogOutput.log`, written after the user's normal visible test, contains:

```text
[Info   :   BepInEx] Loading [Kill and Cost Tracker 1.3.3]
[Info   :Kill and Cost Tracker] Loaded persistent totals: 0 direct player kills across 0 faction records.
[Info   :Kill and Cost Tracker] Main-thread tracker scheduler attached through UnityEngine.UnitySynchronizationContext.
[Info   :Kill and Cost Tracker] Tracker Canvas created during plugin startup at 298x184.
[Info   :Kill and Cost Tracker] Kill and Cost Tracker 1.3.3 loaded. Press F7 to toggle.
[Message:   BepInEx] Chainloader startup complete
```

Missing from that log:

```text
Tracker runtime update is active.
Tracker OnGUI callback is active ...
Tracker shown/hidden via F7 ...
```

The `298x184` startup size is notable. The game later switches to its configured 3840x2160 resolution. The Canvas uses `CanvasScaler.ScaleWithScreenSize`, but creating it before the game's resolution/UI initialization may still be part of the visibility failure.

## Latest fix evidence (1.3.4)

A 15-second hidden/headless launch and a separate 12-second hidden normal-client launch on 2026-09-01 produced:

```text
[Info   :   BepInEx] Loading [Kill and Cost Tracker 1.3.4]
[Info   :Kill and Cost Tracker] Installed update driver patch: MainMenu.Update.
[Info   :Kill and Cost Tracker] Installed update driver patch: MissionManager.Update.
[Info   :Kill and Cost Tracker] Installed update driver patch: SteamManager.Update.
[Info   :Kill and Cost Tracker] Installed update driver patch: HUDAppManager.Update.
[Warning:Kill and Cost Tracker] BepInEx plugin host was destroyed during startup; the standalone tracker runtime will continue across scenes.
[Info   :Kill and Cost Tracker] Tracker runtime update is active.
[Info   :Kill and Cost Tracker] Tracker Canvas created from an active game frame at 3840x2160.
```

This is the first build to demonstrate a post-`Awake` update and post-startup Canvas construction. The normal-client launch reached the user's configured 3840x2160 resolution and logged no plugin exception. Because the window was hidden, it does not prove that the Canvas is visibly composed, that F7 toggles it, or that kill/munition events are accounted correctly in a real mission.

## Current tracking implementation (not gameplay-validated)

The current source uses Harmony patches for:

- `MessageManager.UserCode_RpcKillMessage_635947223`: resolves killer/killed persistent units, adds the destroyed unit's definition value to the killer's faction, and also to direct-player totals when `GameManager.IsLocalPlayer(killer.player)` is true.
- `MissileLauncher.Fire`: compares ammunition before/after firing and charges `WeaponInfo.costPerRound` for the local player's fired rounds.
- `MountedMissile.Fire`: detects the fired-state transition and charges one round.
- `Gun.SpawnBullet`: charges rounds for the local player's gun/projectile fire, accounting for muzzle count when appropriate.

Faction weapon expenditure is not patched directly. It is sampled every 0.5 seconds from `hq.missionStatsTracker.value.total.spent`, and positive deltas are accumulated. Because the periodic update path is part of the unresolved callback failure, faction weapon expenditure should be considered unverified.

Persistence uses `DataContractJsonSerializer` and writes:

- primary JSON: `BepInEx\config\nuclearoption.killcosttracker.stats.json`
- temporary file during replacement: `.tmp`
- rolling backup: `.bak`
- corrupt-input recovery copies: `.corrupt-YYYYMMDD-HHMMSS.json`

Autosave is scheduled three seconds after dirty data. `OnDestroy` also forces a save, but forced termination does not exercise `OnDestroy`. No actual in-mission kill/weapon test has confirmed the Harmony accounting or autosave end-to-end.

## Attempts and results

### Versions 1.1.3–1.1.5

- Iterated on a direct BepInEx `BaseUnityPlugin` IMGUI overlay and F7 polling.
- Added visibility-on-startup/mission behavior, multiple input paths, logging, and persistence-related changes.
- BepInEx loaded each version without exceptions.
- User repeatedly reported a blank result and no F7 response.
- Logs show only plugin load messages; no first `Update` or `OnGUI` callback message.

### Version 1.2.0

- Added a dedicated persistent `MonoBehaviour` runtime object to drive `Update`/`OnGUI`.
- Added a UGUI Canvas-based overlay as an alternative to IMGUI.
- Plugin loaded without exceptions.
- User still saw nothing; no runtime-update or Canvas-creation callback appeared in the log.

### Version 1.2.1

- Added explicit Harmony postfix drivers for:
  - `MainMenu.Update`
  - `MissionManager.Update`
  - `SteamManager.Update`
  - `HUDAppManager.Update`
- Startup log confirms all four patches were installed.
- No driver ever produced the `Tracker runtime update is active` message in the observed run, and the user still saw nothing.

### Comparison with `C:\Games\Cat.Mail.Co\game`

- Its working PackageCounter is a BepInEx 6 IL2CPP plugin.
- It derives from `BasePlugin`, calls `AddComponent<PackageCounterOverlay>()`, polls `Keyboard.current.f8Key.wasPressedThisFrame`, and draws with `OnGUI`.
- This supported the component/IMGUI pattern but is not directly transferable because Nuclear Option is BepInEx 5 Mono.

### Comparison with `C:\Games\CULTIC.v2026.01.10`

- Its working KillTracker is the closest analogue: BepInEx 5 Mono.
- It derives directly from `BaseUnityPlugin`, uses private `Awake`, `Update`, and `OnGUI`, polls `ConfigEntry<KeyboardShortcut>.Value.IsDown()`, sets `GUI.depth = -500`, and scales `GUI.matrix` using screen height.
- CULTIC compiles with `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`, not an SDK `netstandard2.1` build.

### Version 1.3.0

- Rebuilt Nuclear Option's plugin using the same classic .NET Framework compiler approach as CULTIC.
- Restored direct private `Update` and `OnGUI` wrappers, set `GUI.depth = -500`, and added 4K-aware IMGUI scaling.
- Both a headless dedicated-server startup and a hidden graphical client startup loaded cleanly.
- Neither produced a first-update or first-OnGUI log.
- Therefore the earlier hypothesis that `netstandard2.1` compilation alone caused the missing callbacks was **refuted**.

### Version 1.3.1

- Created the UGUI Canvas directly during the confirmed plugin `Awake`, rather than waiting for `Update`.
- Subscribed to `UnityEngine.InputSystem.InputSystem.onAfterUpdate`.
- Startup log confirms Canvas construction, but at the pre-resolution size of 298x184.
- No runtime-update message followed. A likely explanation was that the Input System initializes after BepInEx plugin `Awake` and replaces/clears the early static subscription.

### Version 1.3.2

- Added `Application.onBeforeRender` and `Canvas.willRenderCanvases` callbacks.
- Hidden-client automation still produced no runtime callback. A hidden window may suppress rendering, so that test alone was inconclusive.

### Version 1.3.3

- Captured `UnityEngine.UnitySynchronizationContext` during `Awake`.
- Added a `System.Threading.Timer` that posts a guarded update to that main-thread context every 50 ms.
- Kept Input System, render callbacks, direct Unity messages, and Canvas creation as layered fallbacks.
- Automated hidden-client startup logged successful scheduler attachment and Canvas creation, but no posted runtime update.
- The user's subsequent normal visible test also showed no overlay and the latest log still has no runtime callback. This strongly suggests the synchronization context captured this early is later replaced or is otherwise not serviced.

### Version 1.3.4

- Confirmed the BepInEx plugin host is destroyed during initial startup.
- Restored a dedicated persistent runtime object and changed its owner test to `ReferenceEquals`, avoiding Unity's destroyed-object/null behavior.
- Treats premature plugin `OnDestroy` as a host transition instead of final shutdown, leaving Harmony patches and managed tracker state alive.
- Adds game-owned `Update` postfixes as a redundant driver.
- Delays Canvas construction until the first active game frame.
- Hidden/headless validation reached both `RuntimeUpdate` and delayed Canvas creation without a plugin exception.

### Version 1.3.5

- Fresh user evidence showed the overlay and F7 worked in menu and single-player, and faction munition costs persisted (`Boscali` 16.66001, `Primeva` 10.1569529), but all kill totals and direct-player weapon totals stayed at zero.
- Confirmed all four gameplay patches (`KillMessage`, `MissileLauncher.Fire`, `MountedMissile.Fire`, and `Gun.SpawnBullet`) still used Unity's overloaded null comparison on the destroyed plugin component and therefore skipped their bodies.
- Changed all four guards to `ReferenceEquals`, matching the working persistent runtime driver.
- Added one-shot logs for the first accepted kill and first accepted local-player weapon event.
- Classic compilation succeeded, the installed/source DLL hashes match, and a hidden normal client loaded 1.3.5, preserved both faction records, survived host destruction, ran updates, and created the Canvas at 3840x2160 without a plugin error.

### Version 1.4.0 (currently installed)

- User gameplay validated 1.3.5 tracking: the pre-migration v1 save contained 4 direct-player kills, 8 player missiles, 27 Boscali kills, and 19 Primeva kills with aggregate costs.
- Save format is now v2. Each category persists a `weapons` array containing internal weapon ID, display name, kind, fired-round count, and cumulative cost.
- The player category records every accepted local-player weapon. Faction weapon detail records all authoritative host/server fire and the local player's faction fire on a non-host client.
- Faction aggregate cost remains sourced from the game's authoritative `MissionStatsTracker.spent`; detail collection does not double this total.
- Writes both `nuclearoption.killcosttracker.stats.json` and `nuclearoption.killcosttracker.stats.txt`, with rolling backups for both.
- A real migration preserved all existing v1 totals and created both outputs. A second normal-client launch loaded and rewrote v2 successfully at 3840x2160 with no plugin error.
- Old aggregate counts remain intact, but weapon names for shots fired before 1.4.0 cannot be reconstructed, so the migrated weapon arrays correctly start empty.

## What is known versus unknown

Known:

- Doorstop/BepInEx injection works.
- BepInEx discovers exactly one plugin.
- The plugin's `Awake` runs.
- Harmony `PatchAll` completes without a startup exception.
- Config and stats JSON load.
- Nuclear Option destroys the BepInEx plugin host during its first scene load.
- Version 1.4.0's independent driver survives that destruction and logs its first update.
- Version 1.4.0 constructs the Canvas on an active post-startup frame without throwing.
- F7 input toggles the tracker runtime in both menu and single-player states.
- Faction munition costs update and persist for Boscali and Primeva.
- Kill and direct-player weapon tracking update and persist from real gameplay.
- JSON v2 migration and reload preserve existing player/faction totals.
- The TXT report mirrors player and both faction totals and detail sections.
- Decompiled `MissionStatsTracker.MunitionCost` shows `value.total.spent` is incremented only for fired munitions, not unit purchases.

Unknown/unverified:

- Whether a fresh 1.4.0 shot produces the expected per-weapon ID/name/count/cost entry in both output files.
- Non-host multiplayer clients cannot observe exact per-weapon details for remote faction fire; aggregate replicated faction cost remains available.

## Required next verification

1. Enter a mission and fire one known missile/bomb/gun; wait at least three seconds.
2. Confirm its name, count, kind, and cumulative cost appear under `weapons` in JSON and `Weapons fired` in TXT.
3. Quit normally, relaunch, and confirm the weapon detail persists.
4. If any step fails, preserve `BepInEx\LogOutput.log`, `Player.log`, JSON, and TXT from that run.

## Useful artifacts and backups

Old BepInEx logs, metadata-cache files, and the 1.2.1 DLL were moved (not deleted) to:

`%TEMP%\NuclearOption-KillCostTracker-Verification`

That directory contains prior logs, metadata caches, the 1.2.1 DLL, and the preserved pre-1.4.0 v1 JSON. The current 1.4.0 log remains at `BepInEx\LogOutput.log`.

## Build/install notes

Run from `ModSource\KillCostTracker`:

```powershell
.\build-classic.ps1
```

Then copy `NuclearOption.KillCostTracker.dll` to:

```text
BepInEx\plugins\KillCostTracker\NuclearOption.KillCostTracker.dll
```

BepInEx type metadata is cached in `BepInEx\cache\chainloader_typeloader.dat`. During development, prior attempts invalidated it by moving the exact file to the verification directory before launching. Preserve the stats JSON when replacing builds.
