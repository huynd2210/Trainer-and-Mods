# Breachway Trainer (BepInEx)

A hotkey trainer for **Breachway** (GOG / Windows). Loads through BepInEx; no game
files are modified. **Version 1.4.5.**

---

## Installation (30 seconds)

1. **Close the game.**
2. **Extract this zip into your Breachway game folder** — the one that contains
   `Breachway.exe` (e.g. `C:\Games\Breachway`). Make sure `BepInEx\` and
   `winhttp.dll` land right next to the exe. Overwrite if asked.
3. **Launch `Breachway.exe`.** The **first** launch takes 1–2 minutes while BepInEx
   generates its interop — that's normal, leave it. After that it starts fast.

That's it. Every hotkey plays a short UI sound so you know it registered.

> Already have BepInEx 6 installed? Just copy `BepInEx\plugins\BreachwayTrainer.dll`
> into your existing `BepInEx\plugins\` folder.

---

## Hotkeys

| Key | Effect |
|-----|--------|
| **F1** | +1000 money |
| **F2** | +100 fuel |
| **F3** | Toggle **god mode** (hull locked at max — blocks damage/heat) |
| **F4** | Toggle **infinite crew energy** (crew actions are free) |
| **F5** | Repair hull to full |
| **F6** | +1 Solarii relation (**Shift+F6** = −1) |
| **F7** | +1 Numen relation (**Shift+F7** = −1) |
| **F8** | +1 Union relation (**Shift+F8** = −1) |
| **F9** | +1 Starkin relation (**Shift+F9** = −1) |
| **F10** | +1 FreeRoamers relation (**Shift+F10** = −1) |
| **F11** | +1 Deadweights relation (**Shift+F11** = −1) |
| **F12** | Toggle **bigger hand** (+3 card slots) |
| **Home** | **Unlock all** ships, equipment, and perks |

---

### Toggles (F3, F4, F12)

On/off toggles — press once to enable, again to disable. They **remember their
state between launches** (stored in the config file).

### Bigger hand (F12)

Adds `HandSizeBonus` (default **3**) card slots to *your* hand. Enemies are
unaffected. Turn it on **before combat or at the start of your turn** — the extra
cards draw from the next draw.

### Unlock all (Home)

Unlocks all **6 ships**, all **equipment**, and all **general perks** + perk slots.
It does **not** touch the Ascension/difficulty quirks (so the game doesn't get
harder) and does **not** modify your statistics. Known limitation: each ship's
unique perk is gated by the game's own per-ship progression and stays locked.

### Faction relations (F6–F11)

Relations range **−6 to +6** and persist in your save. The game only shows
**"Allied" at exactly 6**; values 0–5 show "Neutral".

---

## Configuration

Edit `BepInEx\config\com.breachway.trainer.cfg` to change keys and amounts:

| Setting | Default | What it does |
|---------|---------|--------------|
| `MoneyAmount` | 1000 | Money added per F1 press |
| `FuelAmount` | 100 | Fuel added per F2 press |
| `FactionDelta` | 1 | Relation points per faction-key press |
| `HandSizeBonus` | 3 | Extra hand slots (F12) |
| `GodMode` / `InfiniteCrewEnergy` / `BiggerHand` | false | Start these toggles enabled? |
| `AddMoneyKey`, `GodModeKey`, `UnlockAllKey`, … | F1, F3, Home, … | Rebind any hotkey |

Example: set `MoneyAmount = 5000`, or `UnlockAllKey = F9` if you prefer.

---

## Troubleshooting

- **A hotkey does nothing** → check `BepInEx\LogOutput.log` (it logs every action).
  Some cheats need a ship in play, so be in a run, not the main menu.
- **F12 didn't add cards this turn** → it applies from the next draw.
- **"Allied" won't show** → the faction needs to be exactly 6.
- **Can't replace a file** → the game is running; close it first.
- **Game updated / trainer stopped working** → delete `BepInEx\interop\` (it
  regenerates on the next launch) and reinstall the plugin.

---

## Uninstall

Close the game, then delete these from the game folder: `BepInEx\`, `winhttp.dll`,
`doorstop_config.ini`, `.doorstop_version`, `dotnet\`. No original game files were
ever modified.

---

## Notes

- The trainer targets the GOG/DRM-free Windows build (Unity 2022.3, IL2CPP).
- Requires **BepInEx 6.0.0-be.785** (bundled in this package) — the released
  BepInEx 6.0.0-pre.2 cannot load this game.
