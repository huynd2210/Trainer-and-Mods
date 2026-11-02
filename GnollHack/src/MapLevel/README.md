# GnollHack Map Level

A cheat for your own play. Press **Ctrl+F** in the game and the whole current level is
drawn on your map: rooms, corridors, doors, stairs, fountains, altars and every trap.
It is a scroll of magic mapping that always works, plus trap detection. No game time
passes.

For the Steam version of GnollHack on Windows (October 2026 build).

## Install

Extract `MapLevel.zip` into the GnollHack game folder. To find it, right-click GnollHack
in Steam, then Manage, then Browse local files. You should end up with
`GnollHack\MapLevel\` next to `GnollHackM.exe`. Nothing in the game itself is changed,
so to uninstall, delete that folder.

## Use

1. Close GnollHack if it is open.
2. Double-click `Play GnollHack (mods).cmd`. The game must be started this way,
   because the mod is loaded at startup. Launching from Steam starts the game without it.
   The launcher also loads any other mod installed the same way, such as Auto-Identify
   or Reveal Secrets.
3. On each new level, at the normal command prompt (no menu open), press Ctrl+F.

## Notes

- It covers the level you are on. Press Ctrl+F again on each new level.
- It shows the map, not what is on it: monsters and items stay hidden until you see them.
- Hidden corridors appear, as with magic mapping. Secret doors still look like walls;
  press Ctrl+E with Reveal Secrets to find them.
- Scripts and macro pads can trigger the same action without the key:
  `[System.Threading.EventWaitHandle]::OpenExisting('Local\GnollHackMapLevel').Set()`
- The mod logs to `maplevel.log` next to this file.

## How it works

The engine already has a "map the level" command (`^F`), but only in wizard (debug)
mode. The mod switches debug mode on only while that command runs, and switches it off
again once the game is waiting for input. The mapping itself is done by the game's own
code.

The debug-mode flag is not exported by the engine. The mod finds it through two exported
engine functions that read the same settings struct, and checks the struct layout before
it writes anything. If a game update changes the layout, the mod writes nothing and says
so in the log.

Build it with the .NET 8 SDK: `dotnet build -c Release` in this folder, or run
`package.ps1` to build the zips.
