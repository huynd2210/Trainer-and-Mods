using System;
using System.Collections.Generic;

namespace ExtraCustomer
{
    // Customers this mod puts in the game's customer dictionary (StoreClientListDict) so
    // they can be called by id like any other. They are in no random pool, so registering
    // them changes nothing about who walks in on their own.
    //   - The cigarette seller is this mod's own.
    //   - The rest are the game's: ordinary traders it builds straight into the queue
    //     (StoreClientManager.HandlePerkClient and the weekly schedule) without ever
    //     registering an id. Each is registered under the identifier the game gives it.
    // An id the game already has (a later build may register these itself) is left alone,
    // and only the ids added here are taken out again.
    internal static class OwnRegistrations
    {
        private static readonly Dictionary<string, Func<StoreClient>> Factories =
            new Dictionary<string, Func<StoreClient>>
            {
                { CigaretteSeller.ClientId, CigaretteSeller.Create },
                { "revRaider", StoreClientListRev.CreateRevRaider },
                { "revQuartermaster", StoreClientListRev.CreateRevQuartermaster },
                { "lower_level_flexi_buyer", StoreClientList.CreateFlexiBuyer },
            };

        private static readonly List<string> added = new List<string>();

        internal static void Add()
        {
            foreach (KeyValuePair<string, Func<StoreClient>> pair in Factories)
            {
                if (StoreClientListDict.storeClientDict.ContainsKey(pair.Key))
                    continue;
                StoreClientListDict.storeClientDict[pair.Key] = pair.Value;
                added.Add(pair.Key);
            }
        }

        internal static void Remove()
        {
            foreach (string id in added)
                StoreClientListDict.storeClientDict.Remove(id);
            added.Clear();
        }
    }
}
