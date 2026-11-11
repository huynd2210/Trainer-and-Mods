# Ticket Booth

Run your shop as a licensed lottery retailer. Sell tickets and scratchers to the lower
levels, pay out their winners from your till, and catch the forgers.

- **The licence.** After the first week (day 8 by default) the Lottery Representative
  offers you a retailer licence for 300 credits. Say "not now" and he comes back a week
  later. If you install the mod on a save that is already past day 8, he comes the next day.
- **The phone line.** The licence gives you the Lottery Board's number (56883, already in
  your phone book). Call it to order 10 lottery tickets and 2 scratcher stacks; after 500
  credits of orders a bigger order of 20 tickets and 4 stacks unlocks. A courier brings the
  order the next day at the normal price. One order every 5 days. Once you are
  licensed, the weekly vanilla Lottery Representative visits stop.
- **Ticket buyers.** Lower-level citizens come in to buy lottery tickets for the coming
  draw and unscratched scratchers, paying the usual shop markup. Open scratcher stacks to sell them as
  singles.
- **Paying out.** After the draw, or the day after they scratch, winners come back to
  your counter and you pay them from your till. That money is gone: the ticket you keep is
  spent, worth 1 credit, and double-clicking it throws it away. Your markup on ticket sales
  is what has to cover the winners.
- **The odds are against them, not you.** Customers win less often than you do, so that
  ticket sales leave you about 10% profit on average after paying the winners. Big prizes
  are cut the most: a 35 cr lottery win still turns up often, a 2,500 one almost never.
  Scratching your own tickets uses the normal odds and still loses money on average.
  When a customer's win is cut, a forger sometimes comes in their place.
- **Forgers.** Some winners are fake. The clue is always on the ticket: check the
  retailer stamp, the edition against the latest draw (both on the tooltip), the
  numbers (out of order, above 49, or one written twice), what they ask against what the
  ticket shows, and, for scratchers, open the ticket on the counter and look for a
  symbol glued on crooked. Refusing a fake costs nothing.
- **Refusing an honest winner** costs lower-level reputation: 3 for a small prize, 15 for
  a 300 one, 250 for a 2,500 one.
- **Meme mode** (off by default): customers can hit 5 or 6 numbers. Pay 100,000 or
  1,000,000 credits on the spot, or the lower levels lynch you.

Everything is adjustable in the mod's options: first visit day, licence fee, days between
orders, buyers per day, winners per day, forger chance, your profit on sales (0-15%, the
shop markup is the ceiling), fakes in place of cut wins, and meme mode.

**For screenshots and testing:** with the game's developer tools on (an empty `dev.txt` next to
`Probably Stolen.exe`), Ctrl+Shift+T puts the next kind of fake at the front of the queue,
cycling through all nine tricks. Ring the bell to call them in. Without `dev.txt` the key does
nothing.

## Install

1. Extract the zip into the game folder (the one with `Probably Stolen.exe`), so you get
   `Mods\TicketBooth\TicketBooth.dll` and `manifest.xml`.
2. Start the game, open **Mods** from the main menu, enable **Ticket Booth**, restart.

Uses the game's built-in mod loader; nothing else is needed. Made for game VERSION 100.

Removing the mod keeps your save loadable: the board's phone line stops working and a
courier still on its way turns into an ordinary scavenger.

Works with Lottery Foresight; its tooltips show on the tickets you stock. Works with Extra
Customer: the Ticket Buyer shows up in its customer list, under Ticket Booth.
