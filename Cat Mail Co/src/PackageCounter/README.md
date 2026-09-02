# Package Counter

Tracks successfully processed packages in Cat Mail Co. and displays the total in a small in-game overlay. It uses the game's own accepted-package recap values for customer requests, outgoing boat deliveries, and home deliveries. Returned or invalid packages are excluded.

The overall total and the customer, boat, and home-delivery breakdown are persistent. `This shift` resets between shifts. Totals recorded by versions before 1.1.0 remain in `All time` and appear as `Earlier unsplit`, because those versions did not save the route breakdown.

The counter is stored separately from the game save in the BepInEx configuration directory.

## Controls

- `F8`: show or hide the counter
- `Left Shift + F8` twice within five seconds: reset the all-time counter

## Build

Run `build.ps1` from this directory after BepInEx has generated the IL2CPP interop assemblies.

The installed plugin is `BepInEx/plugins/PackageCounter/PackageCounter.dll`. Settings and the persistent total are stored in `BepInEx/config/com.catmailco.packagecounter.cfg`.
