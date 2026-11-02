namespace ChemicalSuppliers
{
    // The two customers. Both only hand over vanilla items (bottles filled with vanilla
    // liquids, vanilla caps), so nothing in a save depends on this mod.
    internal static class Suppliers
    {
        internal const string ChemicalMerchantId = "chemicalMerchant";
        internal const string MushroomSellerId = "shadyMushroomSeller";

        // Prices before the game's market and faction modifiers. The reagents are then scaled
        // by the mod's price setting (default 200%); starch stays dirt cheap.
        private const long AlcoholBottleValue = 12;
        private const long ReducingAgentValue = 35;
        private const long MethylolValue = 45;
        private const long StarchValue = 8;

        internal static StoreClient CreateChemicalMerchant()
        {
            StoreClient client = new StoreClient();
            client.identifier = ChemicalMerchantId;
            client.displayName = "Chemical Merchant";
            client.spriteName = SpriteDict.GetRandomSciSpriteName();
            StoreClientFactionSetup.InitBlackMarket(client);
            client.clientIntent = StoreClient.ClientIntent.SELL;
            client.dismissable = true;
            client.mainDialogue
                .SetText(client.displayName, "Solvent, reducer, the base for the purple stuff, and starch to stretch it. No stabilizer, no questions.")
                .NextDialogue()
                .SetText(client.displayName, "Lab-grade, more or less. Pick what you need.")
                .SetEndAction(delegate
                {
                    PlayerStore store = PlayerStore.Instance;
                    for (int i = 0; i < 4; i++)
                        store.AddDirectSellingItemToTable(AlcoholBottle());
                    for (int i = 0; i < 4; i++)
                        if (RNG.Roll(50))
                            store.AddDirectSellingItemToTable(AlcoholBottle());

                    store.AddDirectSellingItemToTable(ReducingAgentBottle());
                    store.AddDirectSellingItemToTable(ReducingAgentBottle());
                    if (RNG.Roll(50))
                        store.AddDirectSellingItemToTable(ReducingAgentBottle());

                    store.AddDirectSellingItemToTable(MethylolBottle());
                    if (RNG.Roll(50))
                        store.AddDirectSellingItemToTable(MethylolBottle());

                    store.AddDirectSellingItemToTable(StarchBottle());
                    store.AddDirectSellingItemToTable(StarchBottle());
                    if (RNG.Roll(50))
                        store.AddDirectSellingItemToTable(StarchBottle());
                });
            client.AddBasicDialog();
            client.CompleteClientCreation();
            return client;
        }

        internal static StoreClient CreateMushroomSeller()
        {
            StoreClient client = new StoreClient();
            client.identifier = MushroomSellerId;
            client.displayName = "Shady Mushroom Seller";
            client.spriteName = SpriteDict.GetRandomShadySpriteName();
            StoreClientFactionSetup.InitBlackMarket(client);
            client.clientIntent = StoreClient.ClientIntent.SELL;
            client.dismissable = true;
            client.isOfferWholesale = true;
            client.wholesaleDiscount = -RNG.GetRandomInt(15, 25);
            client.mainDialogue
                .SetText(client.displayName, "Fresh from the dark rooms. Dream Caps, and the other kind.")
                .NextDialogue()
                .SetText(client.displayName, "Take the whole lot and I'll knock some off.")
                .SetEndAction(delegate
                {
                    PlayerStore store = PlayerStore.Instance;
                    for (int i = 0; i < 6; i++)
                        store.AddDirectSellingItemToTable(PreBuiltItemHelper.CreateDreamCap());
                    for (int i = 0; i < 4; i++)
                        if (RNG.Roll(60))
                            store.AddDirectSellingItemToTable(PreBuiltItemHelper.CreateDreamCap());
                    for (int i = 0; i < 2; i++)
                        if (RNG.Roll(50))
                            store.AddDirectSellingItemToTable(PreBuiltItemHelper.CreateLargeDreamCap());

                    for (int i = 0; i < 3; i++)
                        store.AddDirectSellingItemToTable("nightmare_cap");
                    for (int i = 0; i < 3; i++)
                        if (RNG.Roll(50))
                            store.AddDirectSellingItemToTable("nightmare_cap");
                });
            client.AddBasicDialog();
            client.CompleteClientCreation();
            return client;
        }

        private static long ReagentPrice(long baseValue)
        {
            return System.Math.Max(1L, baseValue * ChemicalSuppliersMod.ReagentPricePercent / 100);
        }

        // 175 ml alcohol + 75 ml water: the bottle the Common Chemical Supplies box contains.
        private static GameItem AlcoholBottle()
        {
            GameItem item = PreBuildChemHelper.CreateMiniAlcoholBottle();
            item.unitValue = ReagentPrice(AlcoholBottleValue);
            return item;
        }

        // 250 ml, same bottle and 2-8% impurity as the Restricted Chemical Supplies one.
        private static GameItem ReducingAgentBottle()
        {
            GameItem item = PreBuildChemHelper.CreateReducingAgentBottle();
            item.unitValue = ReagentPrice(ReducingAgentValue);
            return item;
        }

        // The Phanta Precursor bottle without its Stabilizing Agent: 250 ml N-Methylol Base,
        // so it reacts with Reducing Agent straight away, with no acid step first.
        private static GameItem MethylolBottle()
        {
            GameItem item = DirectoryMaster.Item("med_bottle_violet");
            WaterHelper.AddLiquid(item, "methylol", 250000);
            PreBuildChemHelper.SetImpurity(item, 200, 800);
            AudioHelper.InitPlasticContainer(item);
            item.unitValue = ReagentPrice(MethylolValue);
            return item;
        }

        // 2 L of the game's own starch filler (the same liquid bought pills are cut with).
        // It isn't evaporative, so the Chem Finisher doesn't count it as wet, and at particle
        // size 5 no filter catches it: it's a pure diluent for pressing.
        private static GameItem StarchBottle()
        {
            GameItem item = DirectoryMaster.Item("large_bottled_water");
            item.name = "Starch Bottle";
            WaterHelper.AddLiquid(item, "starch", 2000000);
            item.unitValue = StarchValue;
            return item;
        }
    }
}
