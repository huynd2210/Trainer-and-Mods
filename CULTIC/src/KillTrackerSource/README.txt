CULTIC Kill Tracker v1.1.0
==========================

A BepInEx plugin that counts how many of each enemy type you have killed.

Every enemy death in the game goes through one function, so the tracker sees all
of them - cultists, the infected, bosses, civilians, everything - and files each
kill under the enemy's own in-game name ("Axe Cultist", "Infected Brute", ...).

Three counts are kept per enemy type:

    LEVEL     kills in the level you are currently in (resets on level change)
    SESSION   kills since the game was launched
    TOTAL     all-time kills, kept in a file so they survive restarts


INSTALL (pick one)
------------------

  Option A - you already run CULTIC with BepInEx (the game folder has a
  "BepInEx" folder and winhttp.dll):
    Extract KillTracker.zip into the CULTIC game folder. It adds
    BepInEx\plugins\KillTrackerPlugin.dll.

  Option B - fresh / self-contained (no separate BepInEx install needed):
    Extract KillTracker-Pack.zip into the CULTIC game folder. It brings
    BepInEx + the mod and the Doorstop bootstrap.

Then just launch CULTIC.exe. Tracking starts immediately - the panel is what you
toggle, not the counting.


HOTKEYS
-------
  F7          show / hide the kill count panel (top right)
  Shift+F7    reset the LEVEL and SESSION columns. All-time totals are kept.
  Ctrl+F7     export the whole table to a .txt file (see EXPORT below)

The first kill of a session prints a one-line reminder of the panel key in the
top-left corner, so you do not have to remember it.


THE PANEL
---------
  Enemy types are sorted by session kills, then by all-time kills, so whatever
  you have been fighting is at the top. The bottom row totals every column.

  If you have more enemy types than fit on screen, the panel says how many are
  not shown - the full list is always in the stats file.


EXPORT
------
  Ctrl+F7 writes the whole table - every column, exactly as the panel shows it -
  to a plain text file, and the filename appears in the corner so you know it
  landed. Nothing else needs to be running; the panel does not even have to be
  visible.

      <game folder>\KillTrackerExports\KillTracker-2026-08-19_235416.txt

  Every export is timestamped and none ever overwrites an earlier one, so you
  can snapshot after each level and keep the lot. Columns are padded to line up
  in any editor - no monospace font required, though it looks best in one:

      CULTIC Kill Tracker
      Exported 2026-08-19 23:54:16
      Level:    The Bell Tolls
      Counting: every enemy death

      ENEMY              LEVEL  SESSION  TOTAL
      ----------------------------------------
      Cultist                7       42    311
      Axe Cultist            4       28    190
      Infected Devourer      0        9     71
      ----------------------------------------
      ALL ENEMIES           11       79    572

  Set ExportFolder in the config to send them somewhere else - your Desktop, a
  notes folder, wherever.


ALL-TIME TOTALS FILE
--------------------
  BepInEx\config\CulticKillTracker.stats.tsv

  Plain text, one line per enemy type, "<count><TAB><enemy name>", sorted with
  the biggest count first - readable and easy to feed to anything else. It is
  written every few seconds while you are killing things, on every level change
  and when you quit.

  This one holds all-time totals only, because that is all that has to survive a
  restart. For the full table - level and session alongside all-time - use the
  export above.

  Delete the file to reset all-time totals to zero. (There is deliberately no
  hotkey for that - a stray keypress should not be able to wipe your history.)


CONFIG
------
  Edit BepInEx\config\local.codex.cultickilltracker.cfg (created on first run):

    ShowOnStart          = false  panel visible when the game starts.
    ExportFolder         = ""     where Ctrl+F7 exports go. Empty means
                                  <game folder>\KillTrackerExports; otherwise
                                  give a full path, e.g. C:\Users\You\Desktop.
    CountOnlyPlayerKills = false  when true, only kills the game attributes to a
                                  player are counted. Deaths with no attribution
                                  (drowning, crushes, enemy infighting, some
                                  explosions) are dropped, so it undercounts -
                                  off by default, which matches the game's own
                                  "enemies killed" map stat.

  You can also rebind TogglePanel, ResetSession and ExportTable under [Hotkeys].


NOTES & LIMITATIONS
-------------------
  - Counts every enemy death, including ones you did not cause, unless
    CountOnlyPlayerKills is turned on. This matches the level-complete
    "enemies killed" number the game itself shows.
  - Enemies swapped out by the enemy randomizer are not double counted; only the
    enemy that actually died is recorded.
  - Enemy names come from the game's own name for each enemy, so two variants
    that share a name share a row.
  - The panel is a read-out only. It never takes mouse or keyboard input away
    from the game, and it draws in menus and while paused as well.
  - Multiplayer: kills are counted from what the local game sees.
