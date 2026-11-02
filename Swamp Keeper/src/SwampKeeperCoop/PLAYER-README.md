# Swamp Keeper Online Co-op

Two players on two PCs play one run together. Each has their own character, arrows, skills, items and kills. The host's game runs the world; the guest's game shows it and sends the guest's controls.

For **Swamp Keeper v1.05** only. Both players need the same package.

## Install

- **SwampKeeperCoop-Pack.zip**: BepInEx 6 plus the mods. Extract into the game folder (the one with `Swamp Keeper.exe`). Use this if you have never installed BepInEx for Swamp Keeper.
- **SwampKeeperCoop.zip**: the mods only, for a game that already has BepInEx 6.0.0-be.789 (IL2CPP x86).

Both zips contain Swamp Keeper Mods 1.2.0 (Kill Tracker + Item Prepare), which the co-op plugin requires to load. It replaces 1.1.0 and keeps your kill history. Its kill tracker is not tested in co-op.

The first launch after installing BepInEx takes a few minutes. Later launches are quick.

## Start a session

1. Both players launch the game and stay on the **title screen**.
2. Both press **F7** to open ONLINE CO-OP.
3. **Host**: send the guest your address and the **session code** shown in the menu, then click **Host**.
   - Same home network: your PC's LAN address (for example 192.168.x.x).
   - Over the internet: use a VPN such as Radmin VPN (see `SwampKeeper-Radmin-Guide.html`), or forward TCP port **27856** to your PC.
4. **Guest**: enter the host's address, paste the host's session code over the code in the box, and click **Join**.

If Windows Firewall asks, allow Swamp Keeper. The host's PC must accept TCP port 27856.

## Before the run

Both games enter the preparation area. Each player presses **F7** (the guest can also use F6 or the Inventory key):

- Click item icons to pick any of the 40 items. Hover an icon to read it.
- Use the **Skill 1** and **Skill 2** tabs to choose your two skills.
- Click **Ready**. Changing a choice un-readies you.

When both players are ready, start the run normally: take the ladder for a normal run, or the Well. Either player can walk to it. The difficulty popup appears on the host's screen, and the host confirms it.

## During the run

- Either player can open a stage exit, talk to the merchant after a boss, or start a Well trial.
- Enemies and bosses go after the nearer living player. Kills count for whoever landed the killing hit.
- **Death**: if one player falls, the other keeps playing, and the fallen player returns at the next stage. If both fall, the run ends on the host's end screen and the host restarts it for both.
- F7 shows your inventory during a run. Equipment can be changed before the next run.
- **Leaving as the guest**: the guest's game has no pause menu of its own during co-op. Press **Esc** to open the co-op menu and click **Quit to title**. The game closes and restarts at the title screen. The host's run pauses as if the connection had dropped, and you can rejoin it from F7.

## If the connection drops

The host's run pauses when the guest drops, or when nothing has arrived for 8 seconds. The guest's F7 menu opens with the address and code already filled in. Click **Join** to continue with the same character, items and kills. Only the original guest can rejoin a run.

Tested at about 200–260 ms round trip with no problems. The host uploads about 350 KiB/s (roughly 3 Mbit/s) during play, so the host needs an upload speed of at least 4 Mbit/s for internet play.

## Good to know

- The guest's game shows the host's world as it happens. The guest's own input therefore lags by the round-trip time.
- Gameplay sound effects play only on the host's PC.
- Shop, merchant and difficulty screens appear on the host's screen.
- Only the host's game saves progress (unlocks, records). The guest's own save is not changed while playing as a guest.
- Both games need the same game version and the same mod build, or the connection is refused.

## Uninstall

Delete `BepInEx\plugins\SwampKeeperCoop` to remove co-op. Delete `BepInEx\plugins\SwampKeeperMods` to remove Kill Tracker + Item Prepare. Your kill history stays in `BepInEx\config\SwampKeeperMods`.
