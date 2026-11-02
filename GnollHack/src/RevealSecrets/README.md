# GnollHack Reveal Secrets

A cheat for your own play. Press **Ctrl+E** in the game and every secret door and hidden
passage on the current level is found at once, wherever it is. Each one turns into a
normal door or corridor and shows up on your map, just as if you had found it by
searching. The message log says how many were found. No game time passes.

For the Steam version of GnollHack on Windows (October 2026 build).

## Install

Extract `RevealSecrets.zip` into the GnollHack game folder. To find it, right-click
GnollHack in Steam, then Manage, then Browse local files. You should end up with
`GnollHack\RevealSecrets\` next to `GnollHackM.exe`. Nothing in the game itself is changed,
so to uninstall, delete that folder.

## Use

1. Close GnollHack if it is open.
2. Double-click `Play GnollHack (mods).cmd`. The game must be started this way,
   because the mod is loaded at startup. Launching from Steam starts the game without it.
   The launcher also loads any other mod installed the same way, such as Auto-Identify.
3. On each new level, at the normal command prompt (no menu open), press Ctrl+E.

## Notes

- It covers the level you are on. Press Ctrl+E again on each new level.
- Found doors are real doors: they can be closed or locked, as with any door you find
  by searching.
- Vaults are not covered. A vault is a closed room with no door at all, so there is
  nothing to find.
- Scripts and macro pads can trigger the same action without the key:
  `[System.Threading.EventWaitHandle]::OpenExisting('Local\GnollHackRevealSecrets').Set()`
- The mod logs to `revealsecrets.log` next to this file.

## How it works

The search itself is the game's own code: `findone()`, the routine a wand of secret
door detection runs on each square it reaches. The mod runs it on every secret square of
the level instead of only the ones nearby.

That code has to run on the game's engine thread, between turns. For one key press, the
mod points the wizard-mode command `^E` at its own function and turns wizard mode on, so
the game accepts `^E` and calls that function. Both changes are undone as soon as the
command finishes.

None of these engine internals are exported. The mod finds them from two exported engine
functions by following the code, and it checks every instruction and constant it relies
on. If a game update changes any of them, the mod writes nothing and says why in the log.

Build it with the .NET 8 SDK: `dotnet build -c Release` in this folder, or run
`package.ps1` to build the zips.
