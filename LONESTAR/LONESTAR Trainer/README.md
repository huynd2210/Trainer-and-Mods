# LONESTAR Trainer

A cheat mod for **LONESTAR** (v1.0.09, Unity/Mono).

LONESTAR ships its own C# mod loader with Harmony built in, so this is a normal
game mod — **no BepInEx, no patched executable, nothing to install into the game
folder.** Drop a folder into the game's `Mods\Local` directory and enable it from
the in-game Mods menu.

## Install

1. Copy the `LonestarTrainer` folder into:

   `%USERPROFILE%\AppData\LocalLow\Shuxi\LONESTAR\Mods\Local\`

   so you end up with `...\Mods\Local\LonestarTrainer\mod.json`.
2. Start the game, open **Mods**, and switch **LONESTAR Trainer** on.
3. Restart the game. Mods are patched in at startup.

The gear icon on the mod's row opens its settings, drawn by the game's own
settings panel.

## Hotkeys

| Key | Does |
|-----|------|
| `F1` | God Mode — your ship takes no damage in battle |
| `F2` | One-Hit Kill — any hit destroys the enemy ship |
| `F3` | Infinite Star Coins — refills to the cap whenever you run low |
| `F4` | No Hull Loss Outside Battle — events and hazards can't cost HP |
| `F5` | Repair hull to full |
| `F6` | Win the current battle |
| `F7` | +1 cargo slot |
| `F8` | +250 Star Coins |
| `F9` | +1 move |
| `F11` | Show/hide the status overlay |

`F1`–`F4` are toggles and persist to `config.json`. `F5`–`F9` fire once.
Hotkeys are ignored while the developer console has the keyboard, and can be
turned off entirely in the mod's settings.

The overlay sits bottom-left and lists only what is currently active, plus the
key hints. It uses the game's own accent colours.

## Settings-only options

- **Bonus Energy (first turn / every turn)** — extra energy in battle. These take
  effect from the *next* battle, because the game copies its energy budget once at
  battle start.
- **Enable Developer Console** — see below. Needs a game restart.

## Developer console

The game has a complete GM console built in, disabled by a flag. Turning this
option on flips that flag; press `Enter` in game to open it, `Esc` to close,
arrow keys for history.

Useful commands: `heal 50`, `addcoin 500`, `addtreasure <id>`, `additem <id> <lv>`,
`win`, `adddays 3`, `addlimit`, `setlv 10`, `exp 100`, `alltreasure`, `warp`,
`timescale 2`, `rare`, `enemy <id>`, `addtalent <id>`, `addclue 3`.

> **These commands write to your permanent save.** `unlock`, `resetachi`,
> `resettutorial`, `clearsteamachi`, `resettreasureunlock` and `recoverdata` all
> rewrite or wipe progression, and there is no undo. Back up
> `%USERPROFILE%\AppData\LocalLow\Shuxi\LONESTAR\Save` before using them.

## Notes and known limits

- **Damage previews ignore One-Hit Kill.** The preview numbers shown before you
  commit a turn are computed on a separate code path; the real hit still kills.
- God Mode shows a `0` damage popup rather than suppressing it — that is the
  damage actually applied.
- Turning a toggle off restores normal rules immediately; nothing is left behind
  in the save.
- The trainer never writes the game's shared `DefineValue` flags. Effects like
  *Credit Card* and *No Hull Loss* are owned by real treasures, so those are
  implemented as patches instead and cannot fight with your run.

## Building

Needs the .NET SDK. The project references the game's own assemblies:

```
dotnet build -c Release src/LonestarTrainer/LonestarTrainer.csproj
```

Point it elsewhere if the game is not at the default path:

```
dotnet build -c Release src/LonestarTrainer/LonestarTrainer.csproj -p:GameManaged="D:\LONESTAR\LONESTAR_Data\Managed"
```

`package.ps1` builds and writes the distributable zips to `dist\`.
