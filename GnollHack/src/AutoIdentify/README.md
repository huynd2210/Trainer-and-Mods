# GnollHack auto-identify

A cheat for your own play. Press **Ctrl+I** in the game and everything you are carrying
is identified on the spot, as though you had read a blessed scroll of identify that
always gets every item. That covers item types (they go into your discoveries), plus
blessed/uncursed/cursed status, enchantment, charges and erosion-proofing. No game time
passes.

For the Steam version of GnollHack on Windows (tested on 4.3, October 2026 build).

## Install

Extract `AutoIdentify.zip` into the GnollHack game folder. To find it, right-click
GnollHack in Steam, then Manage, then Browse local files. You should end up with
`GnollHack\AutoIdentify\` next to `GnollHackM.exe`. Nothing in the game itself is changed,
so to uninstall, delete that folder.

## Use

1. Close GnollHack if it is open.
2. Double-click `Play GnollHack (mods).cmd`. The game must be started this way,
   because the mod is loaded at startup. Launching from Steam starts the game without it.
   The launcher also loads any other mod installed the same way, such as Reveal Secrets.
3. Play as usual. Whenever you want, at the normal command prompt (no menu open), press
   Ctrl+I.

The game identifies each item with its usual message and sound. If everything is already
known, nothing happens. Ctrl+I does nothing while a menu, question or text box is open.

## Notes

- **The change is permanent.** Identified items stay identified in your save file.
- Only items you carry are identified, not the contents of bags or boxes. Take items
  out first if you want them identified.
- Scripts and macro pads can trigger the same action without the key:
  `[System.Threading.EventWaitHandle]::OpenExisting('Local\GnollHackAutoIdentify').Set()`
- The mod logs to `autoidentify.log` next to this file.

## How it works

The engine already has an "identify everything" command (`^I`), but only in wizard
(debug) mode. The mod switches debug mode on only while that command runs, picks "all of
them" in its menu, and switches debug mode off again once the game is waiting for input.
The identification itself is done by the game's own code.

The debug-mode flag is not exported by the engine. The mod finds it through two exported
engine functions that read the same settings struct, and checks the struct layout before
it writes anything. If a game update changes the layout, the mod writes nothing and says
so in the log.

Build it with the .NET 8 SDK: `dotnet build -c Release` in this folder.
