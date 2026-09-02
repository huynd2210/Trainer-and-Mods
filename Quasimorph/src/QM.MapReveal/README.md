# Quasimorph Map Reveal

Shows the way out and the mission objective on the map, without having to walk into the dark to
find them.

Open the map as usual (default `M`). Elevators and inter-floor ladders are drawn wherever they are
on the floor, whether or not you have explored that corner, and hovering one names it — "Elevator",
"Ladder up", "Ladder down". Mission objectives are marked too.

## What it actually changes

**Exits.** Normally the map hides an elevator or ladder until you have explored its tile, unless you
pay a Scanner station to scan for exits. This mod switches that same scan on every time you open the
map, so exits are always drawn. The icons are the game's own — nothing new is drawn.

**Objectives.** The unmodded game already marks most mission objectives regardless of exploration,
so on many missions this half changes nothing; that is the "if applicable". What it adds is the
objectives placed with their map marker deliberately switched off, which otherwise stay invisible
for the entire raid.

Scripted story triggers are deliberately left alone. Those marks are turned on and off by mission
scripts as a story progresses, so forcing them would put a marker over every trigger zone on the map
and give away staged reveals.

## What it does not change

Nothing about how a mission plays. The objective flag this mod overrides is read in exactly two
places in the whole game — the map image and the map's hover label. It feeds no win condition, no
enemy AI and no scoring.

It also reveals no more than exits and objectives: enemies, loot and unexplored rooms stay hidden,
so the map still has something to tell you.

Nothing is written to your save. The exit scan clears itself when you close the map, and the
objective flag is restored the instant the map image finishes drawing.

## Settings

`BepInEx/config/com.claude.quasimorph.mapreveal.cfg`, created after the first launch. Also editable
in-game with ConfigurationManager if you have it.

| Setting | Default | Meaning |
| --- | --- | --- |
| `Enabled` | `true` | Master switch. Off = stock behaviour. |
| `ToggleKey` | `F10` | Turns the mod on and off mid-game. Reopen the map to see the change. |
| `ShowExits` | `true` | Always draw elevators and ladders. |
| `ShowObjectives` | `true` | Also reveal objectives whose marker was switched off. |
| `VerboseLogging` | `true` | Log what was revealed each time you open the map. |

Turn `VerboseLogging` off once you are happy the mod works.

## Install

Extract into your Quasimorph folder — the one with `Quasimorph.exe` — so the DLL lands in
`BepInEx/plugins/`. If you do not have BepInEx yet, use `QM.MapReveal-Pack.zip` instead, which
includes it. To uninstall, delete `BepInEx/plugins/QM.MapReveal.dll`.

## If it does not work

Check `BepInEx/LogOutput.log` for lines tagged `[diag]`. The mod reports which Harmony it bound to
and how many methods it patched; it expects **3**. A different number means a game update moved the
map code and the mod needs rebuilding.
