namespace TicketBooth
{
    // Lower-level citizens who come in for a ticket or a scratcher, paying the game's
    // normal retail markup (15% on normal difficulty).
    // What they buy is remembered, so the winners come back to cash in. They win less
    // often than the game's odds (Nerf), so the markup leaves you a profit.
    internal static class Buyers
    {
        internal const string ClientId = "ticketBoothBuyer";

        private static readonly string[] Greetings =
        {
            "Feeling lucky today. Got any tickets?",
            "One more try. This week is the week.",
            "The lottery, or a scratcher. Whatever you have.",
            "My cousin won three hundred off a ticket. My turn.",
        };

        internal static StoreClient Create()
        {
            StoreClient client = new StoreClient();
            client.identifier = ClientId;
            client.displayName = "Ticket Buyer";
            client.spriteName = SpriteDict.GetRandomLowerLevelSpriteName();
            StoreClientFactionSetup.InitLowerLevelCitizen(client);
            client.clientIntent = StoreClient.ClientIntent.BUY;
            client.SetBudget(RNG.GetRandomInt(30, 100));
            client.mainDialogue
                .SetText(client.displayName, Greetings[RNG.GetRandomInt(0, Greetings.Length - 1)])
                .NextDialogue()
                .SetText(client.displayName, "I have " + client.GetBudget() + " credits to try my luck with.");
            client.willAcceptItem = Accepts;
            client.onItemBought = OnBought;
            client.AddBasicDialog();
            client.CompleteClientCreation();
            return client;
        }

        // Unscratched scratchers, and lottery tickets for the coming draw.
        internal static bool Accepts(GameItem item)
        {
            if (item == null || item.IsTag(Tags.Claim))
                return false;
            if (item.IsTag(LotteryHelper.LOTTERY_TICKET_TAG))
                return string.IsNullOrEmpty(item.GetTagReadonly(LotteryHelper.LOTTERY_WINNING_NUMBER_TAG).GetString())
                       && item.GetTagReadonly(LotteryHelper.LOTTERY_EDITION_TAG).GetInt() == LotteryHelper.GetCurrentEdition();
            if (item.IsTag(ScratcherHelper.SCRATCHER_TAG))
                return !ScratcherHelper.IsAnySymbolScratched(item);
            return false;
        }

        private static void OnBought(GameItem item, int price)
        {
            int today = State.Today;
            State.LastSaleDay = today;
            if (item.IsTag(LotteryHelper.LOTTERY_TICKET_TAG))
            {
                var ledger = State.Ledger;
                ledger.Add(new LedgerEntry
                {
                    Edition = item.GetTagReadonly(LotteryHelper.LOTTERY_EDITION_TAG).GetInt(),
                    Numbers = item.GetTagReadonly(LotteryHelper.LOTTERY_NUMBER_TAG).GetString(),
                    SoldDay = today,
                    Paid = price,
                });
                State.Ledger = ledger;
                return;
            }
            if (item.IsTag(ScratcherHelper.SCRATCHER_TAG))
            {
                // They scratch it at home. The symbols are rolled here at luck 0 rather than
                // read off the ticket, which carries your luck from when it was delivered.
                int[] symbols = Odds.FairScratch(Daily.Rng);
                int prize = Odds.ScratchPrize(symbols);
                if (prize == 0)
                    return;
                double budget = Nerf.Budget(price, Odds.ScratcherCost, TicketBoothMod.ShopProfit);
                if (Nerf.Keeps(Daily.Rng, Nerf.Scratcher, budget, prize))
                    State.AddClaim(ClaimFactory.HonestScratcher(State.NextClaimId(), symbols, today, today + 1));
                else
                    Claims.FakeInPlaceOf(Claim.Scratcher, today + 1);
            }
        }
    }
}
