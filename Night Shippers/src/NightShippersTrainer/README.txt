Night Shippers Trainer
======================

Press F10 during a shift to get +1000 gold.

Built for Night Shippers (Unreal Engine 5.5.4, ProjectSH-Win64-Shipping.exe).
Runs on UE4SS, which is Unreal's equivalent of BepInEx.


Install
-------

You need UE4SS installed first. If you do not have it, use
NightShippersTrainer-Pack.zip instead of this file - it contains both.

With UE4SS already installed, extract this zip into your Night Shippers folder,
the one holding ProjectSH.exe. For a Steam install that is usually:

    C:\Program Files (x86)\Steam\steamapps\common\Night Shippers

The files land in:

    ProjectSH\Binaries\Win64\ue4ss\Mods\NightShippersTrainer\

Start the game. Nothing else to switch on - the mod ships an enabled.txt, so
UE4SS picks it up on its own.


Using it
--------

Load into a shift, then press F10. The gold counter on the HUD goes up by 1000.

The counter is the confirmation. Unreal strips its on-screen debug text out of
release builds, so the mod cannot draw its own message - it writes to the log
instead:

    ProjectSH\Binaries\Win64\ue4ss\UE4SS.log

Look for lines tagged [NightShippersTrainer].

Press it in the main menu or the lobby and nothing happens: there is no shift
running yet, so there is no gold total to add to. The log says so.


Multiplayer
-----------

The grant goes through the game's own server RPC
(BP_PC_Coop_C "Add gold to game state"), the same call the game makes when you
earn gold normally. So it works whether you host or join, and the gold is real
to everyone in the run - it is the shared team total, not a local display trick.

Which also means: this adds gold to your whole lobby. Do not use it on strangers
who did not ask for it.


Changing the amount or the key
------------------------------

Edit Scripts\config.lua, save, restart the game:

    GoldAmount   = 1000        -- gold per press
    GiveGoldKey  = "F10"       -- "F5", "ADD" (numpad +), "INS", "HOME", "G", ...
    ModifierKeys = {}          -- e.g. { "CONTROL" } to need Ctrl+F10

Key names are Microsoft virtual-key names. If you mistype one, the mod falls
back to F10 and records the mistake in UE4SS.log.


Uninstall
---------

Delete ProjectSH\Binaries\Win64\ue4ss\Mods\NightShippersTrainer\.
Nothing in the game itself is modified - no game file is patched or replaced,
so verifying the install through Steam changes nothing about the mod.


How it works
------------

Night Shippers keeps the live gold on its replicated coop GameState,
BP_GameState_Coop_C.Gold. The game's own path for granting it is:

    BP_PC_Coop_C : Add gold to game state(Value)
      -> Server_Add Gold to Gamestate(Value)          [server RPC]
        -> BP_GameState_Coop_C : Add Gold Only(Add Value)
          -> Gold += Value, replicates, OnRep_Gold refreshes the HUD

The mod calls the top of that chain. If the player controller is unreachable it
falls back to calling Add Gold Only on the GameState directly, and failing that
writes Gold and runs OnRep_Gold by hand so the HUD still updates.
