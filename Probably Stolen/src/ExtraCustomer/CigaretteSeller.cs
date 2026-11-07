using System.Collections.Generic;

namespace ExtraCustomer
{
    // The game's only cigarette sellers are the scavenger who brings cigarettes, stamps or
    // injectors at random (scavInspectable) and a story character. This one is that
    // scavenger with the dice taken out: always cigarettes, the same packs and day scaling.
    // Only vanilla items change hands, and a saved queue that names this id after the mod
    // is removed falls back to a vanilla scav, so saves do not depend on the mod.
    internal static class CigaretteSeller
    {
        internal const string ClientId = "extraCustomerCigaretteSeller";

        internal static StoreClient Create()
        {
            StoreClient client = new StoreClient();
            client.identifier = ClientId;
            client.displayName = LocHelper.GetLocalizedClientName("name_scavenger");
            client.cooldown = 3;
            client.spriteName = SpriteDict.GetRandomScavSpriteName();
            client.clientFaction = "FACTION_LOWER_LEVEL";
            StoreClientFactionSetup.InitScav(client);
            client.clientIntent = StoreClient.ClientIntent.SELL;
            client.isOfferWholesale = true;
            client.wholesaleDiscount = -RNG.GetRandomInt(10, 20);

            List<string> greetings = StoreClientList.GetScavGreetingLines();
            List<string> offers = StoreClientList.GetScavOfferLines();
            client.mainDialogue
                .SetText(client.displayName, greetings[RNG.GetRandomInt(0, greetings.Count - 1)])
                .NextDialogue()
                .SetText(client.displayName, offers[RNG.GetRandomInt(0, offers.Count - 1)])
                .SetEndAction(delegate
                {
                    int day = StoreStation.Instance.dayCounter;
                    int packs = 1 + (day >= 15 ? 1 : 0) + (day >= 40 ? 1 : 0) + (day >= 75 ? 1 : 0);
                    for (int i = 0; i < packs; i++)
                        PlayerStore.Instance.AddDirectSellingItemToTable(PreBuiltItemHelper.RandomCigarette());
                });
            client.AddBasicDialog();
            client.CompleteClientCreation();
            return client;
        }
    }
}
