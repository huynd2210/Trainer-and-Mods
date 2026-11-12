# Pawn Counter

Customers short on cash put a valuable, bulky item on your counter and ask for a loan against it.
Lend the credits and you hold the item until the owner comes back to buy it back for more. If they
never come back, it's yours to sell.

## What you'll see

- From day 3, about one morning in two, a **pawner** comes in (about one in ten while you
  already hold 3 pawns). They put the item on the counter
  and name their terms: "Lend me 220 credits against this Chem Scanner. I'll buy it back on day 14
  for 270." Hover over the item to see the terms and what your fee is made of.
- **What you earn is a holding fee, not a share of the item's value.** The loan they ask follows
  what the item is worth, but the fee on top follows only how much shelf space it takes and how
  risky it is to keep, for each day until they're due back:
  - **Storage:** 2 credits per grid cell per day.
  - **Risk:** 10 credits per risk level per day. Risk is the contraband level (low 1, mid 2,
    high 3), plus 1 if the item is stolen.

  A 6-cell scanner held 4 days earns 50; a 17-cell high-contraband shotgun held 4 days earns 255.
  Bulky or risky items can cost their owner more to buy back than they're worth.
- **Deal**, **haggle** (lend 75% of what they asked, for the same fee; they may refuse and leave)
  or **not interested**.
- The item goes to your back counter. Its tooltip says who comes back and when, and what they'll
  want if you've sold it by then.
- **On the due day the owner comes back.** If the item is still somewhere in your store, hand it
  over and get paid. If you sold it, or it was seized, they want its full worth, plus whatever you
  got for it (sold, bartered or handed in for a request), plus a premium on both (25% by default),
  and they keep the loan. Example: worth 300, sold for 100: they want 400 + 25% = 500. Selling a
  pawn early always loses money. Pay up, or
  refuse and lose reputation with their faction. Sending them away counts as refusing.
- **About one in ten never return.** The day after the due date you get a notice and the item is yours.

## What gets pawned

Only valuable and bulky things: at least 150 credits and 6 grid cells.

**Boxes come in more often than anything else** (about two pawns in three):

- **Lower-level citizens:** service or medical crate.
- **Technicians:** engineering or service crate.
- **Upper-level clients:** medical or engineering crate.
- **Shady types:** security or evidence crate.

About half arrive **locked** (the loan follows what's inside, so it hints at the contents) and
half **unlocked**, so you can look inside before you lend. **Unlocking a pawned box is fine.
Taking anything out is not:** while anything the owner packed is missing, the box counts as sold.
The owner won't take it back, and they want its worth plus whatever you got for what you took.
Put it all back before they come and it's fine again. Items from a box say so in their tooltip.

The rest are single items:

- **Lower-level citizens:** metal scanner, AquaScan, plant analyser, combat machete, stun baton,
  surgery toolset, medium backpack.
- **Technicians:** chem scanner, gene scanner, advanced module extractor, rare electronics, metal
  scanner, aug scanner, advanced turbo booster.
- **Upper-level clients** (from day 10): neuroactive perfume, gene scanner, hypertech electronics.
- **Shady types** (from day 7): revolver, shotgun, SMG, heavy handmade pistol, stun gun, rare
  electronics, pistol (Glock receiver), C4, an MBS nerve-agent canister, large military backpack.

Shady pawners and some lower-level citizens sometimes bring **stolen** goods. **Contraband:** the
stun baton (low); revolver, heavy handmade pistol, Glock and evidence crate (mid); shotgun, SMG,
C4, the MBS canister and security crate (high). The stun gun is not. The inspector can take stolen or contraband items, and the
owner will still come back for theirs. That is what the risk fee pays for.

## Settings (Mods > Pawn Counter)

First day, daily chance, pawns held before the chance falls off, how steeply it falls (each pawn
held from that limit on multiplies the chance; default 35%: 16% holding 3, 6% holding 4, 2% holding 5), cheapest item, chance of a box, share of boxes that are locked, loan size (% of worth), storage
fee (per cell per day), risk fee (per risk level per day), and the penalty for a sold pawn (% on
top of its worth and what you got for it). Fee changes apply to new pawns; ones you already hold
keep the terms you agreed.

## Install

Extract into the game folder, so you get `Mods\PawnCounter\PawnCounter.dll` and `manifest.xml`.
Enable it in the game's Mods menu.
