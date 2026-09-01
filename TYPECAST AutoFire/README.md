# TYPECAST AutoFire

AutoFire automatically fires the correct input whenever a targetable enemy enters the active player's attack range.

## Behavior

- Enabled by default whenever a Player instance is created.
- Press **F6** during play to toggle AutoFire on or off.
- Chooses the closest eligible enemy's required keyboard or gamepad input.
- Preserves the game's normal multi-target behavior when several enemies share that input.
- Respects `in_range`, `targetable`, and multiplayer player ownership checks.
- Keeps at most one player projectile in flight toward each enemy, preventing projectile floods.
- Automatically clears a weapon jam if one was caused while AutoFire was disabled.
- When disabled, the original keyboard and gamepad input code runs unchanged.

## Install

From PowerShell in this directory:

```powershell
.\Install.ps1
```

The scripts automatically use the standard Steam path. For another installation location, pass it explicitly:

```powershell
.\Install.ps1 -GameDir 'D:\SteamLibrary\steamapps\common\TYPECAST'
```

The installer downloads UndertaleModTool CLI v0.9.2.0 when needed, saves the original archive as `AutoFireMod\data.win.original.backup` inside the game directory, compiles the mod into a staged file, and replaces the game's `data.win` only after compilation succeeds.

Running the installer again on the tested patched archive is safe; it reports that AutoFire is already installed and exits without rewriting the game.

## Uninstall

```powershell
.\Uninstall.ps1
```

Use the same `-GameDir` argument when TYPECAST is installed outside the standard Steam location.

This restores the retained original backup. It refuses to restore that backup over an unrecognized archive, protecting a newer Steam update from being replaced by an older game build. Steam's **Verify integrity of game files** can also restore the stock archive.

## Compatibility

Built and compile-tested against the TYPECAST Windows data archive with SHA-256:

`8D1C83D68BF6EB673D5A1973DC7AD2A6EAF3E04FBE31DF522C134B2E327126C9`

The installer may continue to work with later game builds when the relevant Player events retain the same structure, but those builds are not validated.
