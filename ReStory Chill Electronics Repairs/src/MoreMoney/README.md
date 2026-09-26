# MoreMoney - ReStory: Chill Electronics Repairs

Press **F8** in game to add **¥10,000** to your wallet. The cash register and PC
wallet displays update immediately, and the money is saved with your game.

The added money is not counted as earned income, so your statistics and the
earnings achievements are unaffected.

## Install

Needs BepInEx 5 (x64). If you don't have it, use `MoreMoney-Pack.zip`, which
includes it.

- **MoreMoney.zip**: extract into the game folder (the one with `Restory.exe`).
  This puts `MoreMoney.dll` in `BepInEx\plugins`.
- **MoreMoney-Pack.zip**: BepInEx 5.4.23.5 plus the mod. Extract into the game
  folder, then start the game.

## Settings

After the first launch, edit `BepInEx\config\com.restory.moremoney.cfg`:

| Setting       | Default | Meaning                        |
|---------------|---------|--------------------------------|
| `AddMoneyKey` | `F8`    | Hotkey. Modifiers work, e.g. `F8 + LeftShift`. |
| `Amount`      | `10000` | Yen added per press (1 to 100,000,000). |

The wallet stops at ¥2,147,483,647, the most the game can store.

## Uninstall

Delete `BepInEx\plugins\MoreMoney.dll`. Money you already added stays in your save.
