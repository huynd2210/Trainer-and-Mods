# Perf Probe

**Find out which mod makes the game lag.** Install it, play normally for a while (including the
moments that feel laggy), then read the newest log in:

    %USERPROFILE%\AppData\LocalLow\Questing Goose Studio\Probably Stolen\ModData\PerfProbe\

Every minute it appends a summary:

- **Time per mod**: milliseconds of each mod's own code per second of play. At 60 fps a frame
  has about 16 ms, so a mod using several ms per second is noticeable; tens of ms is lag.
- **Top code**: the individual patches and event handlers, with calls,
  average and worst time, and memory allocated where the runtime can measure it (heavy
  allocation leads to garbage-collection hitches).
- **Slow frames**: every frame over 50 ms, with what ran in it and whether a garbage collection
  happened. A slow frame with little mod time and no GC is the game itself (or the GPU).

`[whole]` rows time a game method together with all mod patches on it, so they overlap the mod
rows. They catch transpilers (patches that rewrite the game's code), which have no method of their
own to time.

## Notes

- Diagnostic only. Timing adds a little overhead, most visible on tiny methods called thousands of
  times a frame. Remove the mod when you're done.
- Code that mods run on other threads isn't timed, and neither are mods' own per-frame
  `Update` methods (finding them needs a capability the game's mod scanner refuses).
- Disabling it in the mod list stops the log; the timing patches stay until the game restarts.

## Build

```
dotnet build -c Release -p:GameDir="C:\Program Files (x86)\Steam\steamapps\common\Probably Stolen"
```
