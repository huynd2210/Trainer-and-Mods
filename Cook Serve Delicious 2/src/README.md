# CSD2 Autoplay

A bot that plays *Cook, Serve, Delicious! 2* shifts for you.

- **F8** — play the current shift. Press again to stop.
- **F9** — campaign mode: play shifts back to back, confirming the end-of-day screens — the
  results, the medal, and however many reward screens the shift earned — and starting the next
  shift on its own. Gold medals come from playing cleanly, which is what the
  bot does anyway, so this just removes the clicking in between. It stops by itself when it runs
  out of unlocked shifts.

An indicator in the top-left shows which mode is running.

## Install

Right-click `install.ps1` → **Run with PowerShell**, or from a terminal:

```powershell
.\install.ps1
```

It finds the game via Steam, backs up your `data.win` and verifies the copy byte-for-byte,
downloads UndertaleModTool CLI (63 MB, once, checksum-pinned), patches, and installs. Your game
is only touched after every earlier step has succeeded.

If auto-detection fails:

```powershell
.\install.ps1 -GameDir "D:\SteamLibrary\steamapps\common\CookServeDelicious2"
```

To revert:

```powershell
.\uninstall.ps1
```

The patch is always built from the pristine backup, so re-running `install.ps1` can't stack two
copies of the mod. Close the game first — both scripts refuse to run while it's open.

Steam's *Verify integrity of game files* also restores the original. The flip side is that a game
update silently reverts the mod: if F8 stops responding, run `install.ps1` again.

## What it does

Six events are modified, all by appending. Nothing else in the 834 MB data file changes.

The bot never simulates game logic. Each frame it reads the same state the game reads, decides
which key a player would press, and presses it. Scoring, combos, rush hour and validation all run
on untouched code — it plays the game rather than editing the result.

It handles prep-station orders end to end: multi-stage dishes, cook-and-return timing (cooked
dishes outrank new orders, most-nearly-burnt first, so nothing catches fire), multi-page
ingredient panels, per-customer recipe variations, ordered/layered recipes, mash tickets like
trash and kitchen fires, tapped pours, chores and drinks, and holding stations — which it stocks
proactively during quiet moments, preferring variety and always leaving one station free for an
emergency. When there are more stations than dish types on the menu it keeps going round,
topping up whichever dish is held least, so five stations and two dish types end up two of each
rather than one of each with three sitting idle.

## Scoring

It aims for perfect orders, not fast ones. It will not serve a partial dish to save time.

Achievements unlock normally, since the game can't tell the difference between the bot's
keystrokes and yours. If that matters to you, don't use it on an account you care about.

## Troubleshooting

**F8 does nothing.** You're probably not in a shift, or a game update reverted the patch. Re-run
`install.ps1`.

**It stalls on a dish.** It writes a log to
`%LOCALAPPDATA%\NEW_CSD2_PS4Steam\autoplay.log` recording every ticket state change, every key
press with the reason it was chosen, and placed-vs-required ingredients at the moment it serves.
A stall also dumps a snapshot of what it could see. That log is the thing to attach to a bug
report — it usually names the cause outright.

**Installer can't find the game.** Pass `-GameDir` as shown above.

## Credits and licence

The mod is applied with [UndertaleModTool](https://github.com/UnderminersTeam/UndertaleModTool)
by the Underminers team. It is downloaded at install time, not redistributed here.

This package contains only the mod's own source. No game assets or decompiled game code are
included. *Cook, Serve, Delicious! 2* is © Vertigo Gaming Inc.
