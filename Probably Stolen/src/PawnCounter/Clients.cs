using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace PawnCounter
{
    internal sealed class GameRng : IRng
    {
        public int Range(int minInclusive, int maxInclusive) { return RNG.GetRandomInt(minInclusive, maxInclusive); }
    }

    // Someone short on cash puts an item on your counter and asks for a loan against it.
    internal static class Pawners
    {
        internal const string ClientId = "pawnCounterPawner";

        internal static StoreClient Create(IRng rng, PawnerKind kind, GameItem item)
        {
            int today = State.Today;
            int worth = Pool.Worth(item);
            int risk = RiskOf(item);
            Terms.Offer offer = Terms.Make(rng, worth, PawnCounterMod.LoanPercent, Pool.Cells(item), risk, PawnCounterMod.FeeRates);
            int due = today + offer.Days;
            // Haggling lowers what you lend, not what you charge for holding it.
            int lowLoan = Terms.Haggled(offer.Loan);
            int lowRedeem = lowLoan + offer.Fee;
            string itemName = item.name;
            bool willReturn = rng.Range(1, 100) <= kind.ReturnChance;
            bool takesLowOffer = rng.Range(1, 100) <= kind.HaggleChance;

            StoreClient client = new StoreClient();
            client.identifier = ClientId;
            client.displayName = FreshName(rng);
            client.spriteName = Pool.RandomSprite(kind.Id);
            Pool.SetFaction(client, kind.Id);
            client.clientIntent = StoreClient.ClientIntent.DIALOGUE;
            client.dismissable = true;
            string who = client.displayName;

            item.ModifyTag(Tags.OfferLoan, s => s.Enable().SetInt(offer.Loan));
            item.ModifyTag(Tags.OfferRedeem, s => s.Enable().SetInt(offer.Redeem));
            item.ModifyTag(Tags.OfferDue, s => s.Enable().SetInt(due));

            bool placed = false;
            Dialogue ask = new Dialogue();
            ask.SetText(who, "Lend me " + Credits(offer.Loan) + " against this " + itemName + ". I'll buy it back on day " + due + " for " + Credits(offer.Redeem) + "."
                + (Terms.RiskPerDay(risk, PawnCounterMod.FeeRates) > 0 ? " It's hot, I know. That's extra for the risk." : ""));

            Dialogue deal = new Dialogue();
            deal.SetText("PLAYER", "Deal. " + Credits(offer.Loan) + ".");
            ask.AddChoice(DirectoryMaster.Item("$"), "", "", deal, delegate
            {
                Take(item, kind, client, worth, offer.Loan, offer.Redeem, due, willReturn);
                deal.NextDialogue()
                    .SetText(who, "Day " + due + ". Don't sell it.")
                    .SetEndAction(delegate { DialogUIManager.Instance.CurrentClientLeave(); });
            }, () => placed && PlayerStore.Instance.playerCash >= offer.Loan);

            Dialogue haggle = new Dialogue();
            haggle.SetText("PLAYER", Credits(lowLoan) + ". Not a credit more.");
            ask.AddChoice(DirectoryMaster.Item("$"), "", "", haggle, delegate
            {
                if (takesLowOffer)
                {
                    Take(item, kind, client, worth, lowLoan, lowRedeem, due, willReturn);
                    haggle.NextDialogue()
                        .SetText(who, "Fine. " + Credits(lowLoan) + " now, " + Credits(lowRedeem) + " to get it back on day " + due + ".")
                        .SetEndAction(delegate { DialogUIManager.Instance.CurrentClientLeave(); });
                }
                else
                {
                    haggle.NextDialogue()
                        .SetText(who, "It's worth more than that. I'll go somewhere else.")
                        .SetEndAction(delegate { DialogUIManager.Instance.CurrentClientLeave(); });
                }
            }, () => placed && PlayerStore.Instance.playerCash >= lowLoan);

            Dialogue no = new Dialogue();
            no.SetText("PLAYER", "Not interested.");
            ask.AddChoice(DirectoryMaster.Item("$"), "", "", no, delegate
            {
                no.NextDialogue()
                    .SetText(who, "Your loss.")
                    .SetEndAction(delegate { DialogUIManager.Instance.CurrentClientLeave(); });
            });

            client.mainDialogue
                .SetText(who, PawnerKind.Pick(rng, kind.Openers))
                .SetStartAction(delegate
                {
                    if (placed)
                        return;
                    // Not the plain AddDirectSellingItemToTable: that one force-buys items for
                    // Gambling Addiction players. Unowned, so it leaves with them if you say no.
                    PlayerStore.Instance.AddDirectSellingItemToTableMiddle(item, isOwend: false);
                    placed = EmporiumEntry.Instance.frontInvinvElement.childItems.Contains(item);
                    if (!placed)
                        Debug.LogWarning("[PawnCounter] front counter full, " + itemName + " not placed");
                })
                .nextDialogue = ask;

            client.CompleteClientCreation();
            return client;
        }

        // You lend the credits; the item is yours to hold until the owner comes back.
        private static void Take(GameItem item, PawnerKind kind, StoreClient client, int worth, int loan, int redeem, int due, bool willReturn)
        {
            PlayerStore.Instance.ModCash(-loan);
            item.DisableTag(Tags.OfferLoan);
            item.DisableTag(Tags.OfferRedeem);
            item.DisableTag(Tags.OfferDue);
            Pawn pawn = new Pawn
            {
                Id = State.NextId(), Uid = item.GetUniqueID(), Kind = kind.Id, Owner = client.displayName,
                Sprite = client.spriteName, ItemName = item.name, Worth = worth, Loan = loan, Redeem = redeem,
                DueDay = due, WillReturn = willReturn,
            };
            item.ModifyTag(Tags.PawnId, s => s.Enable().SetInt(pawn.Id));
            ModHelper.SetItemOwnerShip(item, true);
            ModHelper.MoveItemToBackCounter(item);
            State.Add(pawn);
            Debug.Log("[PawnCounter] Pawned " + pawn.ItemName + " (worth " + worth + ") for " + loan + ", back day " + due + " for " + redeem + (willReturn ? "" : ", won't return"));
        }

        // A name no current pawn owner has, so returning owners can't be mixed up.
        private static string FreshName(IRng rng)
        {
            var taken = new System.Collections.Generic.HashSet<string>(State.Pawns.Select(p => p.Owner));
            string[] free = PawnerKind.Names.Where(n => !taken.Contains(n)).ToArray();
            return PawnerKind.Pick(rng, free.Length > 0 ? free : PawnerKind.Names);
        }

        internal static int RiskOf(GameItem item)
        {
            return Terms.Risk(ContrabandHelper.GetContrabandLevel(item), StolenHelper.IsStolenItem(item));
        }

        // "12 cells, stolen, high contraband": what the holding fee is made of.
        internal static string FeeBasis(GameItem item)
        {
            List<string> parts = new List<string> { Pool.Cells(item) + " cells" };
            if (StolenHelper.IsStolenItem(item))
                parts.Add("stolen");
            int level = ContrabandHelper.GetContrabandLevel(item);
            if (level > 0)
                parts.Add(ContrabandNames[Math.Min(level, ContrabandNames.Length - 1)] + " contraband");
            return string.Join(", ", parts);
        }

        private static readonly string[] ContrabandNames = { "", "low", "mid", "high", "critical" };

        internal static string Credits(int amount)
        {
            return amount.ToString("N0", CultureInfo.InvariantCulture) + " credits";
        }
    }

    // The owner, back to buy their item back.
    internal static class Redeemers
    {
        internal const string ClientId = "pawnCounterRedeemer";

        internal static StoreClient Create(IRng rng, Pawn pawn)
        {
            PawnerKind kind = PawnerKind.Get(pawn.Kind);
            StoreClient client = new StoreClient();
            client.identifier = ClientId;
            client.displayName = pawn.Owner;
            client.spriteName = string.IsNullOrEmpty(pawn.Sprite) ? Pool.RandomSprite(kind.Id) : pawn.Sprite;
            Pool.SetFaction(client, kind.Id);
            client.clientIntent = StoreClient.ClientIntent.DIALOGUE;
            client.dismissable = true;
            string who = pawn.Owner;
            string faction = client.clientFaction;
            int comp = pawn.Compensation;   // worked out again when they hear the item is gone
            bool resolved = false;

            Dialogue ask = new Dialogue();
            ask.SetText(who, Pawners.Credits(pawn.Redeem) + ", as agreed. My " + pawn.ItemName + ", please.");

            Dialogue give = new Dialogue();
            give.SetText("PLAYER", "Here it is.");
            ask.AddChoice(DirectoryMaster.Item("$"), "", "", give, delegate
            {
                resolved = true;
                GameItem item = Held(pawn);
                if (item != null)
                    item.Destroy();
                PlayerStore.Instance.ModCash(pawn.Redeem);
                StoreReputation.ModReputation(faction, 1);
                State.Remove(pawn.Id);
                Debug.Log("[PawnCounter] Redeemed " + pawn.ItemName + " for " + pawn.Redeem);
                give.NextDialogue()
                    .SetText(who, PawnerKind.Pick(rng, kind.Thanks))
                    .SetEndAction(delegate { DialogUIManager.Instance.CurrentClientLeave(); });
            }, () => { GameItem held = Held(pawn); return held != null && !Pool.Opened(held); });

            Dialogue gone = new Dialogue();
            gone.SetText("PLAYER", "I can't give it back.");
            Dialogue demand = gone.NextDialogue();
            Dialogue pay = new Dialogue();
            // The owner was queued this morning; the item may have been sold since.
            ask.AddChoice(DirectoryMaster.Item("$"), "", "", gone, delegate
            {
                Pawn now = State.Find(pawn.Id) ?? pawn;
                comp = now.Compensation;
                demand.SetText(who, Demand(now, comp));
                pay.SetText("PLAYER", "Here's " + Pawners.Credits(comp) + ".");
            });

            demand.SetText(who, Demand(pawn, comp));
            pay.SetText("PLAYER", "Here's " + Pawners.Credits(comp) + ".");
            demand.AddChoice(DirectoryMaster.Item("$"), "", "", pay, delegate
            {
                resolved = true;
                PlayerStore.Instance.ModCash(-comp);
                Settle(pawn);
                Debug.Log("[PawnCounter] Compensated " + comp + " for " + pawn.ItemName);
                pay.NextDialogue()
                    .SetText(who, "Next time I'm going somewhere else.")
                    .SetEndAction(delegate { DialogUIManager.Instance.CurrentClientLeave(); });
            }, () => PlayerStore.Instance.playerCash >= comp);

            Dialogue refuse = new Dialogue();
            refuse.SetText("PLAYER", "Tough luck.");
            demand.AddChoice(DirectoryMaster.Item("$"), "", "", refuse, delegate
            {
                resolved = true;
                Refused(pawn, faction);
                refuse.NextDialogue()
                    .SetText(who, PawnerKind.Pick(rng, kind.Angry))
                    .SetEndAction(delegate { DialogUIManager.Instance.CurrentClientLeave(); });
            });

            client.mainDialogue
                .SetText(who, "I'm back for my " + pawn.ItemName + ".")
                .nextDialogue = ask;

            // Sent away without an answer: the same as refusing.
            client.OnDismiss = delegate
            {
                if (resolved)
                    return;
                resolved = true;
                Refused(pawn, faction);
            };
            client.CompleteClientCreation();
            return client;
        }

        internal static string Demand(Pawn pawn, int comp)
        {
            int extra = comp - pawn.Worth - pawn.SoldFor;
            if (pawn.SoldFor <= 0)
                return "That was mine, worth " + Pawners.Credits(pawn.Worth) + ". You pay me that, plus "
                    + Pawners.Credits(extra) + " for my trouble: " + Pawners.Credits(comp) + ".";
            return "That was mine, worth " + Pawners.Credits(pawn.Worth) + ", and you got " + Pawners.Credits(pawn.SoldFor)
                + " for it. You pay me both, plus " + Pawners.Credits(extra) + " for my trouble: " + Pawners.Credits(comp) + ".";
        }

        // The pawned item, if it is still somewhere in your store and still yours.
        internal static GameItem Held(Pawn pawn)
        {
            GameItem item = EmporiumEntry.Instance.FindItemByUniqueID(pawn.Uid);
            if (item == null || !GeneralHelper.IsItemOwned(item))
                return null;
            return item;
        }

        // The deal is over and whatever you still hold is fully yours.
        internal static void Settle(Pawn pawn)
        {
            GameItem item = Held(pawn);
            if (item != null)
                item.DisableTag(Tags.PawnId);
            State.Remove(pawn.Id);
        }

        private static void Refused(Pawn pawn, string faction)
        {
            int loss = Terms.ReputationLoss((State.Find(pawn.Id) ?? pawn).Compensation);
            StoreReputation.ModReputation(faction, -loss);
            Settle(pawn);
            Debug.Log("[PawnCounter] Refused " + pawn.Owner + " over " + pawn.ItemName + ", -" + loss + " " + faction);
        }
    }
}
