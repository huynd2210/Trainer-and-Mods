using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ExtraCustomer
{
    // A window in the game's own style (same builder and "paper" layer as the appraisal
    // items window) listing every typed buyer and seller by name. One click queues that
    // customer; the window stays open so several can be queued in a row.
    // A search box and a section dropdown sit above the list, laid out like the game's own
    // Item Spawner (CustomUIItemSpawner): the list is rebuilt in place as you type.
    internal static class CustomerPicker
    {
        private const string WindowId = "extra_customer_picker";
        private const string SearchId = "search";
        private const string CountId = "count";
        private const int ButtonsPerRow = 3;
        private const float ButtonWidth = 205f;
        private const float SearchDelaySeconds = 0.2f;

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

        private class Section
        {
            public string Title;
            public CustomerCatalog.Group[] Groups;
        }

        // Name, intent and faction per customer id, read from a throwaway instance the way
        // the game's own StoreClientListDict.GetClientDisplayName does.
        private static readonly Dictionary<string, StoreClient> info = new Dictionary<string, StoreClient>();

        // Kept between openings, like the Item Spawner's filter.
        private static string search = "";
        private static int sectionIndex;

        private static List<Section> sections = new List<Section>();
        private static RectTransform listHost;
        // Work waiting for a later frame, run by Tick() from the mod's own per-frame update.
        // (Coroutines started on the game's UI object count as "background code" to the game's
        // mod scanner.)
        private static string pendingSearch;   // typed text not searched for yet
        private static float pendingSearchAt;
        private static int focusSearchAtFrame = -1;

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
            StopSearchRoutine();
            if (CustomUIManager.Instance != null)
                CustomUIManager.Instance.CloseWindow(WindowId);
        }

        private static void Open()
        {
            CustomUIManager ui = CustomUIManager.Instance;
            if (ui == null)
                return;
            StopSearchRoutine();

            CustomUIWindow previous = ui.GetWindow(WindowId);
            Vector2? position = previous != null && previous.IsAlive ? previous.Rect.anchoredPosition : (Vector2?)null;

            sections = new List<Section>
            {
                new Section { Title = "Buyers", Groups = CustomerCatalog.Buyers },
                new Section { Title = "Sellers", Groups = CustomerCatalog.Sellers },
                new Section { Title = "Barter and services", Groups = CustomerCatalog.Others },
            };
            CustomerCatalog.Group[] modded = ModdedCustomers.Groups();
            if (modded.Length > 0)
                sections.Add(new Section { Title = "From other mods", Groups = modded });

            List<string> options = new List<string> { "All customers" };
            options.AddRange(sections.Select(s => s.Title));
            if (sectionIndex < 0 || sectionIndex >= options.Count)
                sectionIndex = 0;

            CustomUIBuilder builder = ui.CreateWindow(WindowId, "Call a customer", "paper")
                .SetSize(700f, 680f)
                .SetDraggable(true)
                .SetCloseOnEscape()
                .OnClose(StopSearchRoutine);

            builder.BeginRow(6f, TextAnchor.MiddleLeft);
            builder.AddInput("Search: name, goods, mod...", search, OnSearchSubmitted, SearchId, OnSearchTyping);
            builder.Last?.SetPreferredSize(300f, 0f);
            builder.AddDropdown(options, sectionIndex, i =>
            {
                sectionIndex = i;
                RebuildList();
            });
            builder.Last?.SetPreferredSize(200f, 0f);
            builder.AddFlexibleSpace();
            builder.AddLabel("", CountId);
            builder.End();

            builder.AddLabel("Click a customer to put them at the end of today's queue (in the morning: booked for when you open).");
            builder.BeginScroll(500f);
            listHost = builder.CurrentContainer;
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
            RebuildList();
            focusSearchAtFrame = Time.frameCount + 1;

            if (PaperUIManager.Instance != null && PaperUIManager.Instance.audioSourcePickup != null)
                PaperUIManager.Instance.audioSourcePickup.Play();
        }

        private static void OnSearchTyping(string value)
        {
            pendingSearch = value ?? "";
            pendingSearchAt = Time.unscaledTime + SearchDelaySeconds;
        }

        private static void OnSearchSubmitted(string value)
        {
            StopSearchRoutine();
            ApplySearch(value ?? "");
        }

        // Called every frame.
        internal static void Tick()
        {
            if (pendingSearch != null && Time.unscaledTime >= pendingSearchAt)
            {
                string value = pendingSearch;
                pendingSearch = null;
                ApplySearch(value);
            }
            if (focusSearchAtFrame >= 0 && Time.frameCount >= focusSearchAtFrame)
            {
                focusSearchAtFrame = -1;
                FocusSearch();
            }
        }

        private static void ApplySearch(string value)
        {
            if (value == search)
                return;
            search = value;
            RebuildList();
        }

        private static void StopSearchRoutine()
        {
            pendingSearch = null;
            focusSearchAtFrame = -1;
        }

        private static void FocusSearch()
        {
            CustomUIWindow window = CustomUIManager.Instance != null ? CustomUIManager.Instance.GetWindow(WindowId) : null;
            if (window == null || !window.IsAlive || !window.IsVisible)
                return;
            CustomUIElement element = window.Get(SearchId);
            if (element?.input == null)
                return;
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(element.input.gameObject);
            element.input.ActivateInputField();
        }

        private static void RebuildList()
        {
            CustomUIWindow window = CustomUIManager.Instance != null ? CustomUIManager.Instance.GetWindow(WindowId) : null;
            if (window == null || !window.IsAlive || listHost == null)
                return;

            CustomUIBuilder.ClearContainer(listHost);
            CustomUIBuilder builder = new CustomUIBuilder(CustomUIManager.Instance, window, listHost);
            string[] terms = search.Split((char[])null, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.ToLowerInvariant()).ToArray();

            int shown = 0;
            bool first = true;
            for (int s = 0; s < sections.Count; s++)
            {
                if (sectionIndex != 0 && sectionIndex - 1 != s)
                    continue;
                int count = AddSection(builder, sections[s], terms, first);
                if (count > 0)
                    first = false;
                shown += count;
            }
            if (shown == 0)
                builder.AddLabel(terms.Length > 0 ? "No customer matches \"" + search.Trim() + "\"." : "Nobody to list here.");

            window.Get(CountId)?.SetText(shown + (shown == 1 ? " customer" : " customers"));
        }

        // Returns how many customers it listed; adds nothing when none match.
        private static int AddSection(CustomUIBuilder builder, Section section, string[] terms, bool first)
        {
            int shown = 0;
            foreach (CustomerCatalog.Group group in section.Groups)
            {
                string groupLabel = group.Label();
                List<CustomerCatalog.Entry> entries = CustomerCatalog.Available(group)
                    .Where(e => Matches(e, section.Title, groupLabel, terms))
                    .ToList();
                if (entries.Count == 0)
                    continue;

                if (shown == 0)
                {
                    if (!first)
                        builder.AddSpace(12f);
                    builder.AddHeader(section.Title);
                    builder.AddSeparator(null, 2);
                }
                builder.AddLabel(groupLabel);
                for (int start = 0; start < entries.Count; start += ButtonsPerRow)
                {
                    builder.BeginRow(6f, TextAnchor.MiddleLeft);
                    for (int i = start; i < Math.Min(start + ButtonsPerRow, entries.Count); i++)
                        AddCustomerButton(builder, entries[i]);
                    builder.End();
                }
                shown += entries.Count;
            }
            return shown;
        }

        // Every search word must appear in the button text, the tooltip, the group or
        // section heading, or the customer's id.
        private static bool Matches(CustomerCatalog.Entry entry, string sectionTitle, string groupLabel, string[] terms)
        {
            if (terms.Length == 0)
                return true;
            string text = string.Join("\n", Label(entry), Tooltip(entry), groupLabel, sectionTitle, entry.Id).ToLowerInvariant();
            return terms.All(text.Contains);
        }

        private static string Label(CustomerCatalog.Entry entry)
        {
            string name = entry.Name ?? Describe(entry.Id)?.displayName ?? entry.Id;
            return entry.Suffix == null ? name : name + " (" + entry.Suffix + ")";
        }

        private static string Tooltip(CustomerCatalog.Entry entry)
        {
            if (entry.Locked != null && entry.Locked())
                return entry.LockedReason;

            List<string> parts = new List<string>();
            StoreClient sample = entry.IsRandom ? null : Describe(entry.Id);
            if (sample != null && IntentText.TryGetValue(sample.clientIntent, out string text))
                parts.Add(text);
            if (entry.Tier > 0)
                parts.Add(TierText[entry.Tier]);
            if (entry.Note != null)
                parts.Add(entry.Note);
            return string.Join(" · ", parts);
        }

        private static void AddCustomerButton(CustomUIBuilder builder, CustomerCatalog.Entry entry)
        {
            builder.AddButton(Label(entry), () => ExtraCustomerMod.Feedback(ExtraCustomerMod.AddCustomer(entry)));
            builder.Last?.SetFixedWidth(ButtonWidth);
            if (entry.Locked != null && entry.Locked())
                builder.Last?.SetInteractable(false);
            builder.WithTooltip(Tooltip(entry));
        }

        private static StoreClient Describe(string id)
        {
            if (info.TryGetValue(id, out StoreClient cached))
                return cached;
            StoreClient sample = Create(id);
            info[id] = sample;
            return sample;
        }

        // Name, intent and faction of the customer this id gives, for telling duplicates
        // apart; cached like the button text.
        internal static string Signature(string id) => SignatureOf(Describe(id));

        // Same, from a fresh instance every call (to tell whether an id is random).
        internal static string Sample(string id) => SignatureOf(Create(id));

        private static string SignatureOf(StoreClient client) =>
            client == null ? null : client.displayName + "|" + client.clientIntent + "|" + client.clientFaction;

        private static StoreClient Create(string id)
        {
            try
            {
                return StoreClientListDict.storeClientDict[id]();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ExtraCustomer] Could not read customer '" + id + "': " + e.Message);
                return null;
            }
        }
    }
}
