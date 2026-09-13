# Night Shippers

Unreal Engine 5.5 (`ProjectSH-Win64-Shipping.exe`). Mods run on
**UE4SS**, not BepInEx.

## Night Shippers Trainer

Press **F10** during a shift for **+1000 gold**. The amount and the key are
configurable in `Scripts/config.lua`.

The grant goes through the game's own server RPC, the same call the game makes
when you earn gold normally:

```
BP_PC_Coop_C : Add gold to game state(Value)
  -> Server_Add Gold to Gamestate(Value)          [server RPC]
    -> BP_GameState_Coop_C : Add Gold Only(Add Value)
      -> Gold += Value, replicates, OnRep_Gold refreshes the HUD
```

So it works whether you host or join, and the gold is real to everyone in the
run — it is the shared team total, not a local display trick. Which also means
it adds gold to your whole lobby.

There is no on-screen confirmation beyond the HUD counter:
`UKismetSystemLibrary::PrintString` is compiled out of Shipping builds. The mod
logs to `ProjectSH\Binaries\Win64\ue4ss\UE4SS.log`, tagged
`[NightShippersTrainer]`.

## Contents

| Path | What |
|---|---|
| `dist/NightShippersTrainer.zip` | the mod, ready to extract into the game folder |
| `dist/NightShippersTrainer-Source.zip` | full source release, including the reference dumps |
| `src/NightShippersTrainer/` | the mod itself, as it is installed |
| `src/docs/` | the `INSTALL.txt` written into each zip, plus `BUILD.txt` |
| `src/package.ps1` | builds the zips |

`dist/NightShippersTrainer.zip` needs UE4SS already installed. See
`src/docs/BUILD.txt` for the build, the edit loop, and why the UE4SS version
matters.

## You need UE4SS first, and it has to be the right build

Night Shippers is UE 5.5. **The tagged UE4SS releases predate 5.5 and will not
load in this game** — only the experimental/CI builds work. The build this was
developed against reports `v3.0.1 Beta (Git SHA 2bfa839f)`.

No UE4SS loader is bundled here; see `dist/LOADER-NOT-BUNDLED.md`.

Install UE4SS into `ProjectSH\Binaries\Win64\`, then extract
`NightShippersTrainer.zip` into the folder holding `ProjectSH.exe`. The mod
ships an `enabled.txt`, so UE4SS picks it up without an entry in `mods.txt`.

## Uninstall

Delete `ProjectSH\Binaries\Win64\ue4ss\Mods\NightShippersTrainer\`. No game file
is patched or replaced, so verifying through Steam changes nothing about the mod.
