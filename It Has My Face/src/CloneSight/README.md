# CloneSight

A BepInEx 5 mod for **It Has My Face**. Press **F3** to show your clone through walls, as a coloured outline
with a faint tint over its body. Press F3 again to hide it.

- Your clone is outlined in pink. If it turns into a tentacle monster, the outline moves to the monster.
- The hostile "other clones" that some difficulties add get an amber outline.
- The chapter 5 story clone is outlined too.
- The outline is drawn on top of everything, so walls, crowds and props don't hide it. The HUD stays on top.
- It does nothing in multiplayer. There, the "clone" is another player.

## Install

**No BepInEx yet:** extract `CloneSight-Pack.zip` into the game folder (the one with `It Has My Face.exe`).

**BepInEx 5 already installed:** extract `CloneSight.zip` into the game folder.

## Settings

The first launch creates `BepInEx\config\com.ithasmyface.clonesight.cfg`:

| Setting | Default | Meaning |
|---|---|---|
| `Key` | `F3` | The show/hide key. Names come from Unity's Input System (`F5`, `Backquote`, `Numpad0`, ...). |
| `HoldToShow` | `false` | `true`: the outline shows only while you hold the key. |
| `ShowOtherClones` | `true` | Also outline the hostile other clones. |
| `CloneColor` / `OtherCloneColor` | pink / amber | Outline colours, as `RRGGBBAA` hex. |
| `OutlinePixels` | `3` | Outline thickness at 1080p. It scales with resolution. |
| `FillOpacity` | `0.2` | Tint over the body. `0` draws the outline only. |

## Uninstall

Delete `BepInEx\plugins\CloneSight.dll`. If the Pack installed BepInEx, also delete `winhttp.dll`,
`doorstop_config.ini`, `.doorstop_version`, `changelog.txt` and the `BepInEx` folder.

## Building

Run `package.ps1`. It needs the .NET SDK and expects the game in `..\game` and BepInEx 5.4.23.5 in `vendor\`.
