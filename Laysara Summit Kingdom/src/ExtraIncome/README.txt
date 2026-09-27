Extra Income
============

Adds extra monthly income to the treasury of the map you are playing. You set the
amount in game, and it shows up as its own row in the income breakdown.

Built for Laysara: Summit Kingdom (Unreal Engine 4.27, AS-Win64-Shipping.exe).
Runs on UE4SS, which is Unreal's equivalent of BepInEx.


Install
-------

You need UE4SS installed first. If you do not have it, use ExtraIncome-Pack.zip
instead of this file. It contains both.

If you already have UE4SS, extract this zip into your Laysara folder, the one
holding Laysara.exe. For a Steam install that is usually:

    C:\Program Files (x86)\Steam\steamapps\common\Laysara Summit Kingdom

The files land in:

    AS\Binaries\Win64\ue4ss\Mods\ExtraIncome\

Start the game. There is nothing else to switch on: the mod ships an
enabled.txt, so UE4SS loads it automatically.


Using it
--------

1. Load a map and hover the treasury on the top bar. The income breakdown opens,
   and a new row sits between "Exporting goods" and "Donations":

       Extra income  (+/-)        +0

2. While the breakdown is open, press + or - to change the amount. Hold Shift
   to step by 100 instead of 10. The row, Total Revenue and Balance update as
   you press.

3. The amount is paid into the treasury every in-game month, together with the
   rest of your income.

Each map remembers its own amount. The amounts live in
AS\Binaries\Win64\ue4ss\Mods\ExtraIncome\amounts.txt, one "MapName=amount" line
per map. A map you have never set starts at DefaultAmount from config.lua (0).

The + and - keys work only while the breakdown is open, so they never interfere
with anything else. Both the main-keyboard and the numpad keys work.

The treasury cap still applies. Like the game's own income, the extra cannot
push the treasury above its capacity (the "2000 / 2000" in the breakdown). Build
treasuries to raise the cap, or set RespectTreasuryCap = false in config.lua.


Settings
--------

Edit Scripts\config.lua, save, and restart the game:

    DefaultAmount      = 0       -- starting amount for a map you have not set
    Step               = 10      -- change per + / - press
    BigStep            = 100     -- change per Shift + / - press
    Label              = "Extra income"
    RespectTreasuryCap = true


What it changes in your save
----------------------------

Only the money. The extra is added straight into the treasury when the month
turns over. The mod does not touch the game's own income figures (such as
"Assistance from The Capital"), so saving never bakes a changed rate into the
file. Remove the mod and the income goes back to normal, while money already
paid stays.

The breakdown's Total Revenue and Balance include the extra, but only in the
breakdown. The game's own calculations, such as the months-until-bankrupt
warning and balance objectives, use its own numbers without the extra.


Log
---

The mod logs to AS\Binaries\Win64\ue4ss\UE4SS.log, with lines tagged
[ExtraIncome]: the map and amount when you load, each change you make, and each
monthly payment.


Uninstall
---------

Delete AS\Binaries\Win64\ue4ss\Mods\ExtraIncome\. The mod does not patch or
replace any game file.
