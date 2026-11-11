using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ExtraCustomer
{
    // Customers other mods add to the game's customer dictionary (StoreClientListDict),
    // found by asking the game which mod's assembly each factory lives in
    // (ModSourceHelper.FindModByDelegate, the same lookup its "from mod" tooltips use).
    // Read each time the picker opens, so mods enabled or disabled since are reflected.
    // Not found: customers a mod builds on the spot and puts straight into the queue
    // without registering an id (they can't be created on demand anyway).
    //
    // De-duplicated, because mods register ids for their own bookkeeping rather than as
    // new customers (Agri Expansion: 650 "perfume audience" ids and 2 "market" ids that
    // each roll a random ordinary customer, and 7 "guest" ids that are the game's retired
    // specialists and inventor under another id):
    //   - ids that differ only by trailing numbers ("x_0_5", "x_1_12") form a family;
    //   - a family whose customer changes from one call to the next is random: it becomes
    //     one "random" button that calls a random member;
    //   - otherwise each member that is the same customer as one already listed (same
    //     name, intent and faction) is dropped.
    internal static class ModdedCustomers
    {
        private static readonly Regex TrailingNumbers = new Regex(@"(?:_\d+)+$");

        internal static CustomerCatalog.Group[] Groups()
        {
            HashSet<string> listed = new HashSet<string>(CustomerCatalog.All.Select(e => e.Id));
            HashSet<string> seen = new HashSet<string>(
                listed.Where(CustomerCatalog.Exists).Select(CustomerPicker.Signature).Where(s => s != null));

            // mod -> family stem -> member ids.
            var byMod = new SortedDictionary<string, SortedDictionary<string, List<string>>>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, Func<StoreClient>> pair in StoreClientListDict.storeClientDict.ToList())
            {
                if (listed.Contains(pair.Key))
                    continue;
                string mod = ModName(pair.Value);
                if (mod == null)
                    continue;
                if (!byMod.TryGetValue(mod, out var families))
                    byMod[mod] = families = new SortedDictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                string stem = TrailingNumbers.Replace(pair.Key, "");
                if (stem.Length == 0)
                    stem = pair.Key;
                if (!families.TryGetValue(stem, out List<string> members))
                    families[stem] = members = new List<string>();
                members.Add(pair.Key);
            }

            List<CustomerCatalog.Group> groups = new List<CustomerCatalog.Group>();
            foreach (var mod in byMod)
            {
                string modName = mod.Key;
                List<CustomerCatalog.Entry> entries = new List<CustomerCatalog.Entry>();
                foreach (var family in mod.Value)
                {
                    List<string> members = family.Value.OrderBy(id => id, NaturalOrder.Instance).ToList();
                    if (IsRandom(members[0]))
                    {
                        entries.Add(new CustomerCatalog.Entry
                        {
                            Id = members[0],
                            Ids = members.Count > 1 ? members.ToArray() : null,
                            IsRandom = true,
                            Name = Humanize(family.Key),
                            Suffix = "random",
                            Note = "A random ordinary customer each time"
                                   + (members.Count > 1 ? " (" + members.Count + " ids)" : "") + ". Added by " + modName,
                        });
                        continue;
                    }
                    foreach (string id in members)
                    {
                        string signature = CustomerPicker.Signature(id);
                        if (signature != null && !seen.Add(signature))
                            continue;
                        entries.Add(new CustomerCatalog.Entry { Id = id, Note = "Added by " + modName });
                    }
                }
                if (entries.Count > 0)
                    groups.Add(new CustomerCatalog.Group { Label = () => modName, Entries = entries.ToArray() });
            }
            return groups.ToArray();
        }

        // Fresh customers from this id differ. Several tries, since a random pick can repeat
        // by chance (Agri Expansion's security audience is a coin flip between two).
        private const int RandomTries = 4;

        private static bool IsRandom(string id)
        {
            string first = CustomerPicker.Sample(id);
            for (int i = 1; i < RandomTries; i++)
                if (CustomerPicker.Sample(id) != first)
                    return true;
            return false;
        }

        // "agri_perfume_audience" -> "Agri perfume audience".
        private static string Humanize(string id)
        {
            string text = Regex.Replace(id.Replace('_', ' '), "([a-z])([A-Z])", "$1 $2").Trim().ToLowerInvariant();
            return text.Length == 0 ? id : char.ToUpperInvariant(text[0]) + text.Substring(1);
        }

        // Display name of the mod that registered this factory; null for the game's own
        // customers and for this mod's.
        private static string ModName(Func<StoreClient> factory)
        {
            try
            {
                LoadedMod mod = ModSourceHelper.FindModByDelegate(factory);
                if (mod?.Manifest == null || mod.Manifest.ID == ExtraCustomerMod.Id)
                    return null;
                string label = Regex.Replace(ModLoader.GetModLabel(mod.Manifest) ?? "", "<[^>]*>", "").Trim();
                return label.Length > 0 ? label : mod.Manifest.ID;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ExtraCustomer] Could not tell which mod added a customer: " + e.Message);
                return null;
            }
        }

        // "x_2" before "x_10".
        private sealed class NaturalOrder : IComparer<string>
        {
            internal static readonly NaturalOrder Instance = new NaturalOrder();
            private static readonly Regex Parts = new Regex(@"\d+|\D+");

            public int Compare(string a, string b)
            {
                string[] pa = Parts.Matches(a).Cast<Match>().Select(m => m.Value).ToArray();
                string[] pb = Parts.Matches(b).Cast<Match>().Select(m => m.Value).ToArray();
                for (int i = 0; i < Math.Min(pa.Length, pb.Length); i++)
                {
                    int c = char.IsDigit(pa[i][0]) && char.IsDigit(pb[i][0])
                            && long.TryParse(pa[i], out long na) && long.TryParse(pb[i], out long nb)
                        ? na.CompareTo(nb)
                        : string.Compare(pa[i], pb[i], StringComparison.OrdinalIgnoreCase);
                    if (c != 0)
                        return c;
                }
                return pa.Length.CompareTo(pb.Length);
            }
        }
    }
}
