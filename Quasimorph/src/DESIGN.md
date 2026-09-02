# Quasimorph BepInEx mods — shared design contract

Game: Quasimorph v1.0, Unity 2022.3.62f2, Mono, x64.
Game namespace: `MGSC` (in `Assembly-CSharp.dll`).
Loader: BepInEx 5.4.23.2 x64, already installed at the game root.

Decompiled game source (read-only reference, grep this before guessing at any API):
`C:\Users\Admin\AppData\Local\Temp\claude\C--Games-Quasimorph-v1-0-Quasimorph\185a23b5-7b19-4157-a891-adc4884ec3a0\scratchpad\decomp\MGSC\`

## Build

`ModSource/Directory.Build.props` is shared and **already verified working** — do not modify it.
It compiles against the game's own Mono BCL + Unity + `Assembly-CSharp` (no NuGet needed).
Each mod is a plain SDK project with essentially only an `AssemblyName`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><AssemblyName>QM.WarpDrive</AssemblyName></PropertyGroup>
</Project>
```

Build with `dotnet build <proj>.csproj -c Release`. Output lands in `bin/Release/`.

## Verified game API facts

These were confirmed by decompiling and by a compile smoke-test. Trust them.

**Reaching game state.** The DI container is a `private static State` field on `MGSC.Bootstrap`:

```csharp
var state = (MGSC.State)HarmonyLib.AccessTools.Field(typeof(MGSC.Bootstrap), "_state").GetValue(null);
var cargo = state.Get<MGSC.MagnumCargo>();   // null before a run is loaded
var time  = state.Get<MGSC.SpaceTime>();
```

`State.Get<T>()` returns `null` when `T` is not registered, so always null-check — these
objects only exist once a campaign is loaded.

**Item catalogue.** `MGSC.Data.Items.Records` is an `IEnumerable<BasePickupItemRecord>`
(so `.Count()` needs `using System.Linq`), each with a string `.Id`.

**Creating an item.** `SingletonMonoBehaviour<ItemFactory>.Instance.CreateForInventory(string itemId, bool randomizeConditionAndCapacity = false, bool isSingleItem = false)`

**Putting it in ship cargo.** `MagnumCargoSystem.AddCargo(MagnumCargo, SpaceTime, BasePickupItem, ItemStorage specificStorage = null, bool splittedItem = false, bool tabFilter = false)`

**Travel model** (`MGSC.TravelSystem`, `MGSC.TravelMetadata`, `MGSC.SpaceTimeSystem`):

- `TravelSystem.GetTravelHoursBetweenPoints(TravelMetadata, SpaceObjects, string startId, string endId)`
  returns the **in-game hours** a trip costs (distance × `Data.Global.DistanceToHours`).
  `StartSpaceshipTravel` stores it in `travelData.TravelHoursDuration` and then feeds it to
  `OrbitRingsSystem.UpdateRings(hours * 3600, ...)`, so patching this one method keeps the
  world simulation, the clock, and the UI estimate consistent with each other.
- `travelData.FlightTime` is the **real-time seconds** the flight animation runs, set in
  `StartSpaceshipTravel` from `Data.Global.FlightTimeToSatelite` / `FlightTimeToPlanet`.
- Travel progress is `(travelData.TravelTimer.Time - 5f) / (travelData.FlightTime - 5f)`, and
  there is a separate `ExitingOrbit` state governed by `EXIT_ORBIT_TRAVEL_TIME = 5f`.
  **Therefore `FlightTime` must stay meaningfully above 5.** Clamp it; never let it reach 5.
- While travelling, `SpaceTime.TimeScale` is forced to `TimeScale.DynamicTravel` and the clock is
  driven by `travelData.TravelStartTime.AddHours(TravelHoursDuration * progress)` — i.e. in-game
  time consumed is a pure function of `TravelHoursDuration`, independent of `FlightTime`.

These two knobs are genuinely independent and both are wanted:
*real seconds you sit watching the ship fly* (`FlightTime`) vs
*in-game hours the trip costs you* (`TravelHoursDuration`).

## CRITICAL: the game destroys BepInEx plugin components

Verified by instrumenting `OnDestroy` and watching a live run: **Quasimorph destroys the
`BaseUnityPlugin` component shortly after startup.** The log shows

```
[diag] plugin component was DESTROYED.
```

for both mods, every run. Two consequences, and both of them silently produce a mod that loads
successfully and then does nothing:

1. **`Update` and `OnGUI` on the plugin class stop running.** Hotkeys and IMGUI windows die.
   Put them on your own `GameObject` with `DontDestroyOnLoad` instead (see `WarpDriveHost` /
   `SpawnerHost`). Those keep ticking — confirmed by a 30-second heartbeat surviving the whole run.

2. **Never let runtime code test `plugin != null`.** That is `UnityEngine.Object`'s overloaded
   equality, which reports *true for null* once the component is destroyed. A guard like
   `Active => Instance != null && Instance.Enabled.Value` therefore turns itself off permanently
   and every Harmony patch becomes a no-op while Harmony still reports the methods as patched.
   Hold config entries and the logger in a plain static class (`Cfg`) with no Unity types in the
   path; `ConfigEntry` and `ManualLogSource` stay valid after the component dies.

This cost a full debugging round trip. Check it first if a mod loads but has no effect.

## Diagnosing "the mod loads but nothing happens"

Log `harmony.GetPatchedMethods()` at startup — it distinguishes "the patch never attached" from
"the patch attached but the guard is false", which need completely different fixes. Also log the
`typeof(Harmony).Assembly` location, because the game ships its own `0Harmony` 2.3.3 in
`Quasimorph_Data/Managed` next to BepInEx's 2.9.0. (In practice BepInEx's copy wins — verified.)

Note that BepInEx's disk log is buffered, so a log tail taken after force-killing the process is
not evidence of what did or did not happen. Read it while the game is still running.

**`Harmony.PatchAll(Type)` does not descend into nested types.** If the patch classes are nested
inside a container class (as in `QM.OperatorBoost`'s `StatPatches`), `PatchAll(typeof(StatPatches))`
patches *nothing* and reports no error. Walk them yourself:

```csharp
foreach (Type t in typeof(StatPatches).GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
    harmony.CreateClassProcessor(t).Patch();
```

Then assert the patched count equals the number of patch classes, not merely that it is non-zero —
that catches one broken target among many, which the zero check does not.

## Conventions for both mods

- Namespace `QM.<ModName>`, plugin GUID `com.claude.quasimorph.<modname>`.
- `[BepInPlugin(GUID, "Human Name", "1.0.0")]`, Harmony id = GUID.
- Wrap every patch body and every `OnGUI` body in try/catch and log via the plugin's `Logger`.
  A mod must never be able to hard-crash or soft-lock the game.
- All tunables go through BepInEx `Config.Bind` so they are editable in
  `BepInEx/config/<GUID>.cfg` and via ConfigurationManager.
- Hotkeys use `BepInEx.Configuration.KeyboardShortcut` config entries, not hardcoded `KeyCode`s.
- No new visual style invention: IMGUI windows use the default `GUI.skin` and stock controls.
- Never edit files under `Quasimorph_Data/` or `GameData/`.
