# Boat Height Unlocker

Makes Cat Mail Co.'s boat departure height limit configurable, leaving every other storage area unchanged. The default of 1000 game units is unreachable, which removes the restriction; a smaller value reinstates a limit at the height you choose.

## How it behaves

Stacking is never restricted. Parcels can always be piled onto the boat, exactly as in the unmodded game, and the boat withholds departure while the stack is over the limit — with the game's own on-boat indicator.

The mod writes the two boat ceilings differently, which is what preserves that split:

- `StorageMaximumHeight`, the parcel-placement limit, is set high so placement is never blocked.
- `StorageMaximumApprovedHeight`, the departure limit, is set to the configured value.

`IsHeightApproved` is deliberately left alone. The game's own `UpdateApprovedHeight` compares the live height against the approved ceiling and drives the indicator.

Two earlier versions got this wrong, in opposite directions. 1.0.0 forced `IsHeightApproved` to `true` on every update, so the boat could always depart and no configured limit had any effect. 1.1.x wrote the configured value to the placement ceiling as well, so a low limit blocked parcels from being set down at all.

## Configuration

`BepInEx/config/com.catmailco.boatheightunlocker.cfg`, created after the first launch:

- `Boat / MaximumHeight` (default `1000`): the height at which the boat refuses to depart, in game units. Clamped to a floor of 0.01. Boat heights in this build sit well under 1.0, so useful limits are fractional — 1000, 50 and 15 are all equally unreachable.
- `Debug / LogPlacements` (default `false`): log every boat placement attempt with the live heights, to `BepInEx/LogOutput.log`. Diagnostic only.

## Build

Run `build.ps1` from this directory. The compiled plugin is installed to `BepInEx/plugins/BoatHeightUnlocker/BoatHeightUnlocker.dll`.
