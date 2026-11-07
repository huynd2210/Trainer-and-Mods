# Probably Stolen: Cyberpunk Shopkeeper Sim mods

Thirteen mods by NyanDoggo for the game's built-in mod loader (no BepInEx needed). Made for v0.46D.

- **No Rent Increase**: the landlord stops raising the weekly rent. Deals you accept
  yourself (vending machines, starting perks) still change it.
- **Extra Customer**: F8 while the shop is open lists about 150 of the game's regular
  buyers, sellers, barterers and service customers, grouped by what they trade (food,
  water, medical, materials, household, substances, cigarettes/stamps/injectors, weapons,
  firearms, contraband, specialists...); click one to put them at the end of today's queue. Shift+F8 adds a
  random customer. Rebind F8 in the mod's settings (Mods menu: click the key, press the
  new one, Apply) or in Options > Keybinds. Every morning it also adds extra
  customers on top of the normal ones: 1 by default, 0 to 10 in the mod's settings.
  With Chemical Suppliers installed, its two sellers are in the F8 list too, and that mod
  then adds no key of its own. The list includes the Clinic Volunteer, who normally only
  comes during the Oxymore wave and gives away 2 to 6 Pure White injectors; turn off
  *Clinic Volunteer's injectors are free* in the mod's settings to make him charge the
  normal price instead.
- **Fraud Detection**: hovering an item shows what it really is when its label lies
  (counterfeit cigarettes, stamps and injectors, impure water and chemicals, worn or
  expired goods...) and the real value change. The inspection screen opens with the
  counterfeit's flaw already ticked; you still press Confront yourself.
- **Auto Appraisal**: appraisal items show their true value on hover, and the estimate
  box opens with that value filled in; you still press Estimate yourself.
- **Lottery Foresight**: hover a lottery ticket to see the coming draw's numbers and what
  the ticket wins; hover a scratch ticket to see its prize before scratching. The weekly
  draw uses the foreseen numbers.
- **More Scavenging**: 1000 extra scavenging attempts a night at the dumping ground
  (0 to 10000 in its settings), with the injury risk held at its normal level by default.
- **Recursive Storage**: storage bays fit inside other storage bays, as deep as you like
  (makeshift, normal, large, machine and hypertech bays). The gunsmith and chemist bays
  still take only their own goods, and a bay can't go inside itself. Take nested bays out
  before disabling the mod.
- **Chemical Suppliers**: two new sellers for the drug lab. The **Chemical Merchant**
  sells alcohol, reducing agent, pure N-Methylol Base for
  Phanta (no stabilizer, so no acid step) and cheap 2 L starch bottles for cutting a batch
  (starch shows milky white in clear containers, not water teal);
  the reagents cost 2x base price by default (50-500% in its settings). The **Shady
  Mushroom Seller** sells a wholesale lot of Dream Caps and Nightmare Caps and walks in on
  20% of days (0-100 in its settings). Call either one: it detects Extra Customer at
  startup and uses its F8 list when it's running; otherwise F7 calls the merchant and
  Shift+F7 the mushroom seller. Only vanilla items, so saves are fine without the mod.
- **Instant Evaporator**: the Evaporator does a whole night's work every second (about
  300 ml), at the same Energy Cell power per litre. Distillate and the Solvent Recovery
  Module work as before.
- **Precise Temperature Control**: the Heating Plate's target temperature can be set to
  the degree at any module count (-/+ 1 °C, Shift-click 10 °C); the plate stops at the
  target and holds it exactly. Max temperature and heating speed still follow the modules.
- **Precision Pipettes**: 0.5 ml and 0.1 ml droppers, used like the vanilla ones, with a
  green (0.5 ml) or red (0.1 ml) bulb to tell them from the teal 1 ml and purple 5 ml. The
  Retired Chemist sells both whenever they offer the 1 ml dropper. Sell or trash them
  before removing the mod.
- **Unlimited Reputation**: faction reputation keeps climbing past 200, and everything
  that scales with it (haggling odds, supplier chances, black market stock, tips) keeps
  scaling. The panel shows the real number; the -200 floor is unchanged.
- **More Run Stats**: a Shady Deals tab on the end-of-run summary with contraband sold
  by type (drugs, weapons, access cards, other), scam profit (what mislabelled goods
  earned above their honest price, plus the biggest single scam) and stolen goods sold.
  Counts from when the mod is enabled; the totals are kept in the save.

## Install

Every mod stands alone: install any one, or any mix, with nothing else needed (no other
mod, no BepInEx). Extract `dist\<Mod>.zip` into the game folder (next to
`Probably Stolen.exe`). It creates `Mods\<Mod>\`. Start the game, open **Mods** on the
main menu, enable the mod, and restart. Each mod is tagged for game version 0.46, so the
Mods menu flags it if your game is a different version.

## Build

`src\package.ps1` builds every mod folder under `src\` against the game's own DLLs
(set `GameDir` if the game is not at `..\Probably Stolen`) and writes `dist\`.
