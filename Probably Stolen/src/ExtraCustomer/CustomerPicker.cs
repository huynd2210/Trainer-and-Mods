using System;
using System.Collections.Generic;
using UnityEngine;

namespace ExtraCustomer
{
    // A window in the game's own style (same builder and "paper" layer as the appraisal
    // items window) listing every typed buyer and seller by name. One click queues that
    // customer; the window stays open so several can be queued in a row.
    internal static class CustomerPicker
    {
        private const string WindowId = "extra_customer_picker";
        private const int ButtonsPerRow = 3;
        private const float ButtonWidth = 205f;

        private static readonly Dictionary<StoreClient.ClientIntent, string> IntentText =
            new Dictionary<StoreClient.ClientIntent, string>
            {
                { StoreClient.ClientIntent.BUY, "Buys" },
                { StoreClient.ClientIntent.SELL, "Sells" },
                { StoreClient.ClientIntent.SELLNBUY, "Buys and sells" },
                { StoreClient.ClientIntent.GUNSMITH, "Orders a custom gun" },
                { StoreClient.ClientIntent.BARTER, "Barters" },
                { StoreClient.ClientIntent.APPRAISAL_SERVICE, "Wants an appraisal" },
                { StoreClient.ClientIntent.INFORMATION_DEALER, "Sells information" },
            };

        private static readonly string[] TierText = { null, "basic tier", "mid tier", "top tier" };

        // Name and intent per customer id, read from a throwaway instance the way the game's
        // own StoreClientListDict.GetClientDisplayName does.
        private static readonly Dictionary<string, (string name, StoreClient.ClientIntent intent)> info =
            new Dictionary<string, (string, StoreClient.ClientIntent)>();

        internal static bool IsOpen =>
            CustomUIManager.Instance != null && CustomUIManager.Instance.IsOpen(WindowId);

        internal static void Toggle()
        {
            if (IsOpen)
                Close();
            else
                Open();
        }

        internal static void Close()
        {
            if (CustomUIManager.Instance != null)
                CustomUIManager.Instance.CloseWindow(WindowId);
        }

        private static void Open()
        {
            CustomUIManager ui = CustomUIManager.Instance;
            if (ui == null)
                return;

            CustomUIWindow previous = ui.GetWindow(WindowId);
            Vector2? position = previous != null && previous.IsAlive ? previous.Rect.anchoredPosition : (Vector2?)null;

            CustomUIBuilder builder = ui.CreateWindow(WindowId, "Call a customer", "paper")
                .SetSize(700f, 680f)
                .SetDraggable(true)
                .SetCloseOnEscape();
            builder.AddLabel("Click a customer to put them at the end of today's queue.");
            builder.BeginScroll(540f);
            AddSection(builder, "Buyers", CustomerCatalog.Buyers);
            builder.AddSpace(12f);
            AddSection(builder, "Sellers", CustomerCatalog.Sellers);
            builder.AddSpace(12f);
            AddSection(builder, "Barter and services", CustomerCatalog.Others);
            builder.End();

            builder.BeginRow(6f, TextAnchor.MiddleLeft);
            builder.AddLabel("Shift+" + KeyBindHelper.KeyName(ExtraCustomerMod.AddCustomerKey) + ": random customer");
            builder.AddFlexibleSpace();
            builder.AddButton(LocHelper.GetLocalizedOrDefault("MainMenu", "close", "Close"), Close);
            builder.End();

            if (position.HasValue)
                builder.SetPosition(position.Value);
            else
                builder.Center();
            builder.Show();

            if (PaperUIManager.Instance != null && PaperUIManager.Instance.audioSourcePickup != null)
                PaperUIManager.Instance.audioSourcePickup.Play();
        }

        private static void AddSection(CustomUIBuilder builder, string title, CustomerCatalog.Group[] groups)
        {
            builder.AddHeader(title);
            builder.AddSeparator(null, 2);
            foreach (CustomerCatalog.Group group in groups)
            {
                List<CustomerCatalog.Entry> entries = new List<CustomerCatalog.Entry>(CustomerCatalog.Available(group));
                if (entries.Count == 0)
                    continue;

                builder.AddLabel(group.Label());
                for (int start = 0; start < entries.Count; start += ButtonsPerRow)
                {
                    builder.BeginRow(6f, TextAnchor.MiddleLeft);
                    for (int i = start; i < Math.Min(start + ButtonsPerRow, entries.Count); i++)
                        AddCustomerButton(builder, entries[i]);
                    builder.End();
                }
            }
        }

        private static void AddCustomerButton(CustomUIBuilder builder, CustomerCatalog.Entry entry)
        {
            (string name, StoreClient.ClientIntent intent) = Describe(entry.Id);
            string id = entry.Id;
            string label = entry.Suffix == null ? name : name + " (" + entry.Suffix + ")";
            builder.AddButton(label, () => ExtraCustomerMod.Feedback(ExtraCustomerMod.AddCustomer(id)));
            builder.Last?.SetFixedWidth(ButtonWidth);

            if (entry.Locked != null && entry.Locked())
            {
                builder.Last?.SetInteractable(false);
                builder.WithTooltip(entry.LockedReason);
                return;
            }

            List<string> parts = new List<string>();
            if (IntentText.TryGetValue(intent, out string text))
                parts.Add(text);
            if (entry.Tier > 0)
                parts.Add(TierText[entry.Tier]);
            if (entry.Note != null)
                parts.Add(entry.Note);
            builder.WithTooltip(string.Join(" · ", parts));
        }

        private static (string name, StoreClient.ClientIntent intent) Describe(string id)
        {
            if (info.TryGetValue(id, out var cached))
                return cached;

            (string, StoreClient.ClientIntent) result = (id, StoreClient.ClientIntent.UNDEFINED);
            try
            {
                StoreClient sample = StoreClientListDict.storeClientDict[id]();
                if (sample != null)
                    result = (sample.displayName, sample.clientIntent);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ExtraCustomer] Could not read customer '" + id + "': " + e.Message);
            }
            info[id] = result;
            return result;
        }
    }
}
