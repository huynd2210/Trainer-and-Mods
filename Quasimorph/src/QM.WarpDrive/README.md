# Quasimorph Warp Drive

Get to your destination sooner. Two separate things make a trip feel slow, and this mod lets you
turn down each of them independently.

**How long you sit and watch** is the flight animation — real seconds of your life spent looking at
the ship crawl across the map. **What the trip costs you** is in-game hours on the calendar, which
is what makes contracts expire and the galaxy move on while you fly. They are unrelated: making the
animation twice as fast does not give you back a single in-game hour. So there are two sliders, and
you probably want both.

## Controls

| | |
|---|---|
| **F9** | Toggle the mod on/off mid-game |

A small readout in the bottom-left corner shows the current state while you are in space.

## Settings

Edit `BepInEx/config/com.claude.quasimorph.warpdrive.cfg`, or use
[ConfigurationManager](https://github.com/BepInEx/BepInEx.ConfigurationManager) to change these
live without restarting.

| Setting | Default | What it does |
|---|---|---|
| `Enabled` | `true` | Master switch. Off = the game behaves exactly as unmodded. |
| `ToggleKey` | `F9` | Rebind the toggle. |
| `ShowStatusOverlay` | `true` | The corner readout. |
| `InstantTravel` | `false` | Skip the flight animation entirely — you arrive within a few frames. Overrides `RealTimeSpeedMultiplier`. |
| `RealTimeSpeedMultiplier` | `5` | Used only when `InstantTravel` is off. Real seconds the flight animation runs, divided by this. Range 1–50, but see the floor below. |
| `InGameTimeMultiplier` | `0` | In-game hours a trip costs, multiplied by this. `0.0` = the calendar does not move while you travel, `1.0` = unmodded. Range 0–1. |
| `AffectShippingDeliveries` | `false` | See below. |

### How instant travel works

The game's travel state machine is gated on elapsed time, not distance: leaving orbit takes a fixed
5 seconds, and flight progress is `(elapsed - 5) / (flightTime - 5)` — so simply setting a tiny
flight time divides by zero and the ship never arrives. Instead the mod winds the travel timer
forward to just short of arrival. Every stage still runs in the right order (path building, orbit
exit, arrival, the random space-event roll); it just doesn't wait. You arrive in a few frames.

With `InstantTravel` off, the mod falls back to dividing the animation length by
`RealTimeSpeedMultiplier`, floored at 6 seconds for the reason above.

### The 6-second floor, in practice

Vanilla flight lengths are **20 seconds** to a planet and **10 seconds** to a satellite. The floor
is 6 seconds, so with the animation kept:

| Trip | Vanilla | Fastest possible | Multiplier needed |
|---|---|---|---|
| To a planet | 20s | 6s | 3.4 or more |
| To a satellite | 10s | 6s | 1.7 or more |

Anything above ~3.4 makes no further difference — the default of 5 already gets you the shortest
visible flight there is. Five of those six seconds are the fixed orbit-departure phase, which can
only be removed by turning `InstantTravel` on.

### The shipping side effect

The game reuses the same distance-to-hours function to decide when goods shipped *between stations*
will be delivered. That is not your ship travelling, so by default the mod leaves it alone. Set
`AffectShippingDeliveries = true` if you also want station deliveries to arrive proportionally
sooner.

## Notes

- Save-safe: it changes only in-flight values, writes nothing to your save, and removing the DLL
  returns the game to normal.
- Setting `InGameTimeMultiplier` very low is a significant difficulty change — time pressure is a
  core part of the game's economy. `0.1` is already generous; try `0.5` for a lighter touch.
