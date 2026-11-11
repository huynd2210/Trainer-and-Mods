using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TicketBooth
{
    internal static class Tags
    {
        internal const string Claim = "TB_CLAIM";
        internal const string Forged = "TB_FORGED";
        internal const string StampDay = "TB_STAMP_DAY";
        internal const string Glue = "TB_GLUE";
        internal const string Asked = "TB_ASKED";
        internal const string Spent = "TB_SPENT";
    }

    // Winners coming back to the counter, honest or not.
    internal static class Claims
    {
        internal const string ClientId = "ticketBoothClaimant";
        private const string LowerLevel = "FACTION_LOWER_LEVEL";
        private const int ExpireAfterDays = 7;

        // Sold before prices were saved with the ledger: the normal-difficulty price.
        private const int UnknownLotteryPrice = 29;

        // Draw night (postfix of LotteryHelper.OnDrawWiningNumber): tickets you sold for
        // this edition are checked like your own, and the winners will come to cash in.
        internal static void OnDraw()
        {
            if (!State.Ready || !State.Licensed)
                return;
            PlayerStore store = PlayerStore.Instance;
            int edition = store.currentLotteryEdition - 1;
            string winning = store.currentWinningLotteryNumber;
            if (string.IsNullOrEmpty(winning))
                return;
            int today = State.Today;
            State.LastDrawEdition = edition;
            State.LastDrawNumbers = winning;
            State.LastDrawDay = today;

            List<LedgerEntry> ledger = State.Ledger;
            foreach (LedgerEntry entry in ledger.Where(e => e.Edition == edition))
            {
                int matches = Odds.CountMatches(entry.Numbers, winning);
                if (Odds.LotteryPrize(matches) == 0)
                    continue;
                // Off by default: customers never hit 5 or 6.
                if (matches >= 5 && !TicketBoothMod.MemeJackpots)
                    continue;
                int due = today + Daily.Rng.Range(1, 2);
                int paid = entry.Paid > 0 ? entry.Paid : UnknownLotteryPrice;
                double budget = Nerf.Budget(paid, Odds.LotteryCost, TicketBoothMod.ShopProfit);
                if (Nerf.Keeps(Daily.Rng, Nerf.Lottery, budget, Odds.LotteryPrize(matches)))
                    State.AddClaim(ClaimFactory.HonestLottery(State.NextClaimId(), entry, winning, matches, due));
                else
                    FakeInPlaceOf(Claim.Lottery, due);
            }
            State.Ledger = ledger.Where(e => e.Edition > edition).ToList();
        }

        // Someone tries a fake, but only while you are actually selling tickets.
        internal static void RollForgery(int today)
        {
            if (today - State.LastSaleDay > 7 || Daily.Rng.Range(1, 100) > TicketBoothMod.ForgerChance)
                return;
            AddForgery(today, ClaimFactory.AnyKind, "Forger today");
        }

        // A customer's win was cut (Nerf): sometimes a forger turns up in their place, so
        // the counter stays about as busy.
        internal static void FakeInPlaceOf(char kind, int dueDay)
        {
            if (Daily.Rng.Range(1, 100) <= TicketBoothMod.FakeRefill)
                AddForgery(dueDay, kind, "Forger in place of a cut win");
        }

        // What a forger knows on their day: the latest draw you were licensed for.
        internal static ClaimFactory.Context ForgeryContext(int dueDay)
        {
            ClaimFactory.Context ctx = new ClaimFactory.Context
            {
                Today = dueDay,
                LicenseDay = State.LicenseDay,
                DrawEdition = State.LastDrawEdition,
                DrawNumbers = State.LastDrawNumbers,
                DrawDay = State.LastDrawDay,
            };
            if (string.IsNullOrEmpty(ctx.DrawNumbers))
                ctx.DrawEdition = 0;
            return ctx;
        }

        private static void AddForgery(int dueDay, char kind, string why)
        {
            ClaimFactory.Context ctx = ForgeryContext(dueDay);
            Claim claim = ClaimFactory.Forgery(Daily.Rng, State.NextClaimId(), ctx, kind);
            Debug.Log("[TicketBooth] " + why + ": " + claim.Kind + " " + claim.Flaw + ", asking " + claim.Asked);
            State.AddClaim(claim);
        }

        // Due claimants join the queue, up to the daily cap; the rest wait for tomorrow.
        // Ones nobody served for a week stop coming.
        internal static void QueueDue(StoreClientManager manager, int today, HashSet<int> queued)
        {
            List<Claim> claims = State.Claims;
            int before = claims.Count;
            claims.RemoveAll(c => c.DueDay < today - ExpireAfterDays);
            if (claims.Count != before)
                State.Claims = claims;

            foreach (Claim claim in claims.Where(c => c.DueDay <= today && !queued.Contains(c.Id)).OrderBy(c => c.DueDay).ToList())
            {
                if (queued.Count >= TicketBoothMod.ClaimantsPerDay)
                    break;
                manager.AddClient(CreateClaimant(claim));
                queued.Add(claim.Id);
            }
        }

        private static readonly Dictionary<char, string[]> Openers = new Dictionary<char, string[]>
        {
            { Claim.Lottery, new[] { "My numbers came up!", "Read the draw this morning. I won!", "Lottery ticket. It's a winner." } },
            { Claim.Scratcher, new[] { "Scratched it on the way home. Winner!", "Look at this scratcher!", "This one paid out. Cash it for me?" } },
        };

        internal static StoreClient CreateClaimant(Claim claim)
        {
            StoreClient client = new StoreClient();
            client.identifier = ClientId;
            client.displayName = "Ticket Holder";
            client.spriteName = SpriteDict.GetRandomLowerLevelSpriteName();
            StoreClientFactionSetup.InitLowerLevelCitizen(client);
            client.clientIntent = StoreClient.ClientIntent.DIALOGUE;
            client.dismissable = true;

            GameItem ticket = null;
            bool resolved = false;

            Dialogue ask = new Dialogue();
            ask.SetText(client.displayName, "That's " + Credits(claim.Asked) + ". You're a licensed retailer, you pay out.");

            Dialogue pay = new Dialogue();
            pay.SetText("PLAYER", "Here's your " + Credits(claim.Asked) + ".");
            ask.AddChoice(DirectoryMaster.Item("$"), "", "", pay, delegate
            {
                resolved = true;
                Paid(claim, ticket);
                pay.NextDialogue()
                    .SetText(client.displayName, claim.Honest ? "Knew this was my week. Thanks!" : "Pleasure doing business.")
                    .SetEndAction(delegate { DialogUIManager.Instance.CurrentClientLeave(); });
            }, () => PlayerStore.Instance.playerCash >= claim.Asked);

            Dialogue refuse = new Dialogue();
            refuse.SetText("PLAYER", "I'm not paying that.");
            ask.AddChoice(DirectoryMaster.Item("$"), "", "", refuse, delegate
            {
                resolved = true;
                Refused(claim);
                refuse.NextDialogue()
                    .SetText(client.displayName, claim.Honest ? "It's a real winner! The whole level will hear about this." : "Worth a shot.")
                    .SetEndAction(delegate { DialogUIManager.Instance.CurrentClientLeave(); });
            });

            string[] openers = Openers[claim.Kind];
            client.mainDialogue
                .SetText(client.displayName, openers[RNG.GetRandomInt(0, openers.Length - 1)])
                .SetStartAction(delegate
                {
                    if (ticket != null)
                        return;
                    ticket = BuildTicket(claim);
                    // Not the plain AddDirectSellingItemToTable: that one force-buys
                    // tickets for Gambling Addiction players.
                    PlayerStore.Instance.AddDirectSellingItemToTableMiddle(ticket, isOwend: false);
                })
                .nextDialogue = ask;

            // Sent away without an answer: the same as refusing.
            client.OnDismiss = delegate
            {
                if (resolved)
                    return;
                resolved = true;
                Refused(claim);
            };
            client.CompleteClientCreation();
            return client;
        }

        private static void Paid(Claim claim, GameItem ticket)
        {
            State.RemoveClaim(claim.Id);
            PlayerStore.Instance.ModCash(-claim.Asked);
            if (ticket != null)
            {
                // You keep the used ticket; it is worth nothing to you now.
                ModHelper.SetItemOwnerShip(ticket, true);
                Spent.Apply(ticket);
                ModHelper.MoveItemToBackCounter(ticket);
            }
            Debug.Log("[TicketBooth] Paid " + claim.Asked + " (" + (claim.Honest ? "honest" : claim.Flaw) + ")");
        }

        // The ticket goes back with them (the game clears unowned counter items on leave).
        private static void Refused(Claim claim)
        {
            State.RemoveClaim(claim.Id);
            Debug.Log("[TicketBooth] Refused " + claim.Asked + " (" + (claim.Honest ? "honest" : claim.Flaw) + ")");
            if (!claim.Honest)
                return;
            Penalty penalty = Odds.PenaltyFor(claim.Asked);
            if (penalty.Lynch)
            {
                // A jackpot you can't or won't pay: straight to -100, and the mob is next.
                int current = StoreReputation.GetLLReputation();
                if (current > -100)
                    StoreReputation.ModReputationRaw(LowerLevel, -100 - current);
                PlayerStore.Instance.storeClientManager.AddNextClient(StoreClientListMorning.CreateMobLeader());
                return;
            }
            StoreReputation.ModReputationRaw(LowerLevel, -penalty.ReputationLoss);
        }

        private static string Credits(int amount)
        {
            return amount.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " credits";
        }

        // The ticket on the counter. Built from vanilla items so the game's own tooltip,
        // scratch view and redeem path all apply.
        internal static GameItem BuildTicket(Claim claim)
        {
            GameItem item = claim.Kind == Claim.Lottery ? LotteryTicket(claim) : ScratcherTicket(claim);
            item.EnableTag(Tags.Claim);
            item.ModifyTag(Tags.Asked, s => s.Enable().SetInt(claim.Asked));
            if (claim.StampDay >= 0)
                item.ModifyTag(Tags.StampDay, s => s.Enable().SetInt(claim.StampDay));
            if (claim.Forged)
            {
                item.EnableTag(Tags.Forged);
                // A fake carries the value it pretends to have.
                item.SetValue(claim.Asked);
                item.EnableTag(LotteryHelper.LOTTERY_IS_WINNING_TAG);
            }
            return item;
        }

        private static GameItem LotteryTicket(Claim claim)
        {
            GameItem item = DirectoryMaster.Item("lottery_ticket");
            item.ModifyTag(LotteryHelper.LOTTERY_NUMBER_TAG, s => s.Enable().SetString(claim.Data));
            item.ModifyTag(LotteryHelper.LOTTERY_EDITION_TAG, s => s.Enable().SetInt(claim.Edition));
            item.ModifyTag(LotteryHelper.LOTTERY_WINNING_NUMBER_TAG, s => s.Enable().SetString(claim.Winning));
            LotteryHelper.CheckTicket(item, claim.Winning);
            return item;
        }

        private static GameItem ScratcherTicket(Claim claim)
        {
            GameItem item = DirectoryMaster.Item("scratcher");
            int[] symbols = claim.Symbols();
            for (int i = 1; i <= 3; i++)
            {
                int symbol = symbols[i - 1];
                item.ModifyTag(ScratcherHelper.SCRATCHER_SYMBOL + i, s => s.Enable().SetInt(symbol));
                item.ModifyTag(ScratcherHelper.SCRATCHER_STATE + i, s => s.Enable().SetInt(1));
                item.DisableTag(ScratcherHelper.SCRATCHER_LUCK_INT + i);
            }
            // What scratching does (ScratchInput.OnAnyScratch, ScratcherHelper.UpdateValue),
            // minus the achievement check, which is about your own luck.
            item.DisableTag(ScratcherHelper.SCRATCHER_NEW_TAG);
            item.RemoveGameItemType("LOTTERY");
            int value = Odds.ScratchPrize(symbols);
            item.SetValue(value);
            if (value > 0)
                item.EnableTag(LotteryHelper.LOTTERY_IS_WINNING_TAG);
            if (claim.GlueSlot > 0)
                item.ModifyTag(Tags.Glue, s => s.Enable().SetInt(claim.GlueSlot));

            // The vanilla scratcher only opens for its owner; a claim opens on the counter
            // too, so you can look at the symbols before paying. Only the owner redeems.
            item.canActivateSlotItemFunc = (GameItem it, GameInventory inv, SlotMarker marker) => true;
            item.mayActivateSlotItemFunc = (GameItem it, GameInventory inv, SlotMarker marker) => true;
            item.onActivateSlotItemFunc = delegate(GameItem it, GameInventory inv, SlotMarker marker)
            {
                if (GeneralHelper.IsItemOwned(it) && ScratcherHelper.TryRedeemPrize(it))
                    return;
                ScratcherUIManager.Instance.OpenUI(it);
            };
            return item;
        }
    }
}
