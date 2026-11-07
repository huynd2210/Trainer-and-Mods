using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace MoreRunStats
{
    // One item leaving the shop, measured before the sale touches it.
    internal sealed class Sale
    {
        public GameItem Root;
        // The item and everything inside it that is sold with it (not behind a lock),
        // as PlayerStore.PerformOnSoldCheck and SoldItem.MarkStolenOrContrabandSale see it.
        public readonly List<GameItem> Parts = new List<GameItem>();
        public readonly List<double> Weights = new List<double>();
        public double TotalWeight;
        public bool HasHiddenTruth;
        // The price with every hidden truth shown, for the two ways the game prices a
        // sale: at the counter (GetNegociatedValue) and in the vending machine, the water
        // fountain and requests (GetCurrentValue with the retail markup forced on).
        public long CounterTruePrice;
        public long MachineTruePrice;
    }

    internal static class SaleTracker
    {
        // A FieldInfo, not AccessTools.FieldRefAccess<ItemFeature, bool>: the game's mod
        // scanner reports any AccessTools generic over a system type (bool) as the
        // reflection-external capability.
        private static readonly FieldInfo FeatureExposed = AccessTools.Field(typeof(ItemFeature), "isFeatureExposed");

        // Set by PerformOnSoldCheck, which runs right before every counter, vending
        // machine and fountain sale; used by the RunStats call that reports what was paid.
        private static Sale pending;

        internal static void Begin(GameItem root) => pending = root == null ? null : Measure(root);

        // The sale PerformOnSoldCheck measured, if it is for this item (any item when null).
        internal static Sale Take(GameItem root = null)
        {
            Sale sale = pending;
            pending = null;
            return sale != null && (root == null || sale.Root == root) ? sale : null;
        }

        internal static Sale Measure(GameItem root)
        {
            var sale = new Sale { Root = root };
            sale.Parts.Add(root);
            foreach (GameItem child in root.FindAllChildItems())
            {
                if (!child.IsTag("ITEM_BEHIND_LOCK_TAG"))
                    sale.Parts.Add(child);
            }
            foreach (GameItem part in sale.Parts)
            {
                double weight = Math.Max(0, part.GetCurrentValue(useRetailMarkup: true, withChild: false));
                sale.Weights.Add(weight);
                sale.TotalWeight += weight;
            }

            var hidden = new List<ItemFeature>();
            foreach (GameItem part in sale.Parts)
            {
                if (part.itemFeatures == null)
                    continue;
                foreach (ItemFeature feature in part.itemFeatures)
                {
                    if (IsHiddenTruth(feature))
                        hidden.Add(feature);
                }
            }
            sale.HasHiddenTruth = hidden.Count > 0;
            if (!sale.HasHiddenTruth)
                return sale;

            // Price the item as if the customer had seen through every label. Flip the flag
            // directly: ItemFeature.SetExposed also runs the condition's activate action.
            foreach (ItemFeature feature in hidden)
                FeatureExposed.SetValue(feature, true);
            try
            {
                sale.CounterTruePrice = root.GetNegociatedValue();
                sale.MachineTruePrice = root.GetCurrentValue(useRetailMarkup: true, withChild: true, includeEvents: false, forceMarkup: true);
            }
            finally
            {
                foreach (ItemFeature feature in hidden)
                    FeatureExposed.SetValue(feature, false);
            }
            return sale;
        }

        // A feature whose label is not what the item really is, and that nobody has
        // exposed yet. Bargains, markups and market events are marked as matching.
        internal static bool IsHiddenTruth(ItemFeature feature)
        {
            if (feature == null || feature.isDisabled || feature.IsExposed())
                return false;
            if (feature.useCondition)
                return feature.realCondition != null && feature.fakeCondition != null
                    && feature.realCondition.identifier != feature.fakeCondition.identifier;
            return feature.isExposable && !feature.IsFeatureMatch();
        }

        // paid: what the buyer actually handed over. The scam is what that is above the
        // item's honest price; the paid amount is split over contraband and stolen parts
        // by their own value.
        internal static void Record(Sale sale, long paid, bool machinePrice, bool countScam)
        {
            if (sale == null || paid <= 0)
                return;

            if (countScam && sale.HasHiddenTruth)
            {
                long gain = paid - (machinePrice ? sale.MachineTruePrice : sale.CounterTruePrice);
                if (gain > 0)
                    RunLedger.RecordScam(sale.Root.GetDisplayName(), gain);
            }

            for (int i = 0; i < sale.Parts.Count; i++)
            {
                GameItem part = sale.Parts[i];
                bool contraband = part.IsTag("CONTRABAND_ITEM_TAG");
                bool stolen = part.IsTag("STOLEN_TAG");
                if (!contraband && !stolen)
                    continue;
                long share = sale.TotalWeight > 0
                    ? (long)Math.Round(paid * sale.Weights[i] / sale.TotalWeight)
                    : paid / sale.Parts.Count;
                if (contraband)
                    RunLedger.RecordContraband(part, share);
                if (stolen)
                    RunLedger.RecordStolen(share);
            }
        }

        internal static void Guard(string what, Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[MoreRunStats] " + what + " failed: " + ex);
            }
        }
    }

    [HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.PerformOnSoldCheck))]
    internal static class SoldCheckPatch
    {
        private static void Prefix(GameItem item) => SaleTracker.Guard("Measuring a sale", () => SaleTracker.Begin(item));
    }

    // Counter sales. cost is after the customer's budget cap. Same exclusions as RunStats.
    [HarmonyPatch(typeof(RunStats), nameof(RunStats.OnItemSold))]
    internal static class CounterSalePatch
    {
        private static void Postfix(GameItem item, int cost, StoreClient client) => SaleTracker.Guard("Recording a sale", () =>
        {
            Sale sale = SaleTracker.Take(item);
            if (client != null && client.identifier == "businessPermitOfficial")
                return;
            SaleTracker.Record(sale, cost, machinePrice: false, countScam: true);
        });
    }

    [HarmonyPatch(typeof(RunStats), nameof(RunStats.OnVendingSale))]
    internal static class VendingSalePatch
    {
        private static void Postfix(int value) => SaleTracker.Guard("Recording a vending sale", () =>
            SaleTracker.Record(SaleTracker.Take(), value, machinePrice: true, countScam: true));
    }

    [HarmonyPatch(typeof(RunStats), nameof(RunStats.OnFountainSale))]
    internal static class FountainSalePatch
    {
        private static void Postfix(int value) => SaleTracker.Guard("Recording a fountain sale", () =>
            SaleTracker.Record(SaleTracker.Take(), value, machinePrice: true, countScam: true));
    }

    // Items handed in for a request. The reward is fixed whatever the label says, so a
    // mislabelled delivery earns nothing extra and is not counted as a scam.
    [HarmonyPatch(typeof(StoreRequest), "CheckDeliveredItem")]
    internal static class RequestDeliveryPatch
    {
        private static readonly Func<StoreRequest, GameItem, int> PaidValuePerItem =
            (Func<StoreRequest, GameItem, int>)AccessTools.Method(typeof(StoreRequest), "GetPaidValuePerItem")
                .CreateDelegate(typeof(Func<StoreRequest, GameItem, int>));

        private static void Prefix(StoreRequest __instance, GameItem item) => SaleTracker.Guard("Recording a request delivery", () =>
        {
            if (item != null)
                SaleTracker.Record(SaleTracker.Measure(item), PaidValuePerItem(__instance, item), machinePrice: true, countScam: false);
        });
    }
}
