LONESTAR Tracker
================

Keeps a permanent record of every battle you fight: who you killed, what they flew,
how dangerous they were, how long it took and what it cost you - plus a line per
voyage with the outcome.

The game's own statistics screen counts kills. This one names them.


Install
-------
1. Close the game.
2. Copy the LonestarTracker folder into
      %USERPROFILE%\AppData\LocalLow\Shuxi\LONESTAR\Mods\Local\
   so that you end up with ...\Mods\Local\LonestarTracker\LonestarTracker.dll
3. Start the game, open Mods, enable "LONESTAR Tracker".
4. Restart the game.


Using it
--------
F10                 open and close the tracker panel
Left / Right        switch between Overview, This voyage, Battles and Voyages
Up / Down, wheel    scroll
Page Up / Down      scroll a page
Home / End          jump to the top or the bottom
F                   on Battles: cycle the filter (all / kills / minions / elites /
                    bosses / defeats)
S                   on Battles: cycle the order (newest, oldest, toughest tier,
                    longest fight, closest call, enemy name)
Esc                 close

While you play, a one-line readout in the corner shows the voyage's kill count.

The panel has nothing to click on purpose: a click it did not swallow would land on
your ship underneath it. Everything is a key or the wheel.

Change the hotkey, the readout corner, the text size and what gets logged with the
gear icon on the mod's row in the Mods menu.


The log files
-------------
    %USERPROFILE%\AppData\LocalLow\Shuxi\LONESTAR\Tracker\battles.csv
    %USERPROFILE%\AppData\LocalLow\Shuxi\LONESTAR\Tracker\runs.csv
    %USERPROFILE%\AppData\LocalLow\Shuxi\LONESTAR\Tracker\checkpoint.csv

They sit next to your saves, not inside the mod folder, so updating or reinstalling
the mod cannot take your history with it. Open either in Excel, LibreOffice or any
spreadsheet.

battles.csv has one row per battle won or lost, with the enemy's pilot name, ship
name, tier, danger rating, the phase and day, the round count, your hull before and
after, how long the fight took, your ship and pilot, the voyage's seed, and more.

runs.csv has one row per voyage: outcome, ship, pilot, seed, kills broken down by
tier, days survived, phase reached, time, coins earned and what destroyed you.

The mod only ever appends to battles.csv and runs.csv. It never rewrites or deletes a
row. If a voyage ends up written twice - once as "Incomplete" when you quit to the
menu and again when you actually finish it - the later row is the one the panel uses.

checkpoint.csv is the odd one out, and the reason nothing is lost when the game stops
badly. Those two logs only gain a row once something has finished, so the voyage you
are in the middle of - its kills, clock, coins, days, and the starting hull of the
battle you are in - is written here instead: at the start of the voyage, at the start
of every battle, after every result, every 20 seconds, and on quit. Crash, alt-F4 or
pull the plug and you lose at most those few seconds. This is the one file the mod
rewrites, because it is a checkpoint rather than a history - and a battle is written
to battles.csv before the checkpoint is touched, so it never holds the only copy of
anything.

Next time you start the game, an unfinished voyage is already in the list as
"Incomplete" with its real numbers. Load that save and the tracker carries on with the
same row instead of starting a second one; start a different voyage and the unfinished
one is kept as it was.

The panel also comes back to the view, filter and order you left it on.


Notes
-----
- Installing over a running game fails: the game holds the DLL open. Close it first.
- A voyage in progress when you installed the mod is picked up from the next battle.
- Turning "Log Battles" or "Log Voyages" off stops new rows. Nothing already written
  is removed.
- Safe to run alongside LONESTAR Trainer. Put the readout in a different corner from
  the trainer's overlay (the trainer uses bottom left).
