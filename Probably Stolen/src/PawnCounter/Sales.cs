using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace PawnCounter
{
    // Remembers what a pawned item brought in when you let it go, so the returning owner
    // can claim it on top of the item's worth. The game's loader applies these patches.
    internal static class PawnSales
    {
        internal static void Record(GameItem item, int price)
        {
            try
            {
                // The pawned item itself, or something taken out of a pawned box.
                int id = item == null || !State.Ready ? 0 : Tags.PawnOf(item);
                if (id == 0)
                    return;
                List<Pawn> pawns = State.Pawns;
                Pawn pawn = pawns.FirstOrDefault(p => p.Id == id);
                if (pawn == null)
                    return;
                pawn.SoldFor += Math.Max(0, price);
                State.Pawns = pawns;
                Debug.Log("[PawnCounter] Sold " + pawn.Owner + "'s " + pawn.ItemName + " for " + price + "; they'll want " + pawn.Compensation);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PawnCounter] Could not record a pawn sale: " + e);
            }
        }
    }

    // Counter sales (PlayerStore.SellItem) and barters (PlayerStore.OnItemsSold), with the
    // price actually paid.
    [HarmonyPatch(typeof(SoldItem), nameof(SoldItem.TrackItem))]
    internal static class TrackSalePatch
    {
        private static void Postfix(GameItem gameItem, int soldPrice) => PawnSales.Record(gameItem, soldPrice);
    }

    // Items handed in for a station request, at the share of the reward each one earns.
    [HarmonyPatch(typeof(StoreRequest), "CheckDeliveredItem")]
    internal static class RequestDeliveryPatch
    {
        private static readonly Func<StoreRequest, GameItem, int> PaidValuePerItem =
            (Func<StoreRequest, GameItem, int>)AccessTools.Method(typeof(StoreRequest), "GetPaidValuePerItem")
                .CreateDelegate(typeof(Func<StoreRequest, GameItem, int>));

        private static void Prefix(StoreRequest __instance, GameItem item)
        {
            try
            {
                if (item != null && Tags.PawnOf(item) != 0)
                    PawnSales.Record(item, PaidValuePerItem(__instance, item));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PawnCounter] Could not record a request delivery: " + e);
            }
        }
    }
}
