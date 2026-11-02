namespace ChemicalSuppliers
{
    public class ChemicalSuppliersMod : IMod
    {
        internal const string Id = "ChemicalSuppliers";

        private static ConfigEntry<int> mushroomSellerChance;
        private static ConfigEntry<int> reagentPricePercent;

        // Read when the merchant lays out stock, so a change applies to the next visit.
        internal static int ReagentPricePercent => reagentPricePercent?.Value ?? 200;

        public void Init(ModManifest manifest)
        {
            reagentPricePercent = ModHelper.GetConfig(Id).Define(
                "reagentPricePercent", 200, "General",
                () => "Chemical Merchant reagent price (%)",
                50, 500,
                () => "Price of the Chemical Merchant's alcohol, reducing agent and N-Methylol Base, as a % of their base price. Starch is not affected.");

            mushroomSellerChance = ModHelper.GetConfig(Id).Define(
                "mushroomSellerChance", 20, "General",
                () => "Shady Mushroom Seller visit chance (%)",
                0, 100,
                () => "Chance each morning that the Shady Mushroom Seller joins the queue on their own. 0 = only when called.");
        }

        public void OnEnable()
        {
            // Registering in the game's own customer dictionary makes them callable by id
            // (Extra Customer's list uses it). A saved queue that still names one of them
            // after this mod is removed falls back to a vanilla scav, so saves stay loadable.
            StoreClientListDict.storeClientDict[Suppliers.ChemicalMerchantId] = Suppliers.CreateChemicalMerchant;
            StoreClientListDict.storeClientDict[Suppliers.MushroomSellerId] = Suppliers.CreateMushroomSeller;
            ModHook.OnGenerateCustomerNormal += AddMushroomSellerWalkIn;
        }

        public void OnDisable()
        {
            ModHook.OnGenerateCustomerNormal -= AddMushroomSellerWalkIn;
            StoreClientListDict.storeClientDict.Remove(Suppliers.ChemicalMerchantId);
            StoreClientListDict.storeClientDict.Remove(Suppliers.MushroomSellerId);
        }

        // Runs while the morning queue is built. Clients added inside a ModHook generate
        // event are tagged with this mod by the game.
        private static void AddMushroomSellerWalkIn(StoreClientManager manager)
        {
            // Same gate as the vanilla random walk-ins: the intro days only get scripted customers.
            PlayerStore store = PlayerStore.Instance;
            int day = StoreStation.Instance.dayCounter;
            if ((!store.skipIntro && day <= 13) || (store.skipIntro && day == 1))
                return;

            if (RNG.Roll(mushroomSellerChance.Value))
                manager.AddClient(Suppliers.CreateMushroomSeller());
        }
    }
}
