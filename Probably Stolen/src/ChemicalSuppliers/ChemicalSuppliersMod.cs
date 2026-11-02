using UnityEngine;

namespace ChemicalSuppliers
{
    public class ChemicalSuppliersMod : IMod
    {
        internal const string Id = "ChemicalSuppliers";
        private const string ExtraCustomerId = "ExtraCustomer";

        // Stored next to the game's own "keybind_<id>" entries in its settings file.
        private const string KeySaveKey = "mod_ChemicalSuppliers_callKey";
        // F7 is unused by the game; Extra Customer has F8.
        private const KeyCode DefaultKey = KeyCode.F7;

        internal static KeyCode CallKey = DefaultKey;

        private static ConfigEntry<int> mushroomSellerChance;
        private static ConfigEntry<int> reagentPricePercent;
        private GameObject listener;

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

            if (ES3.KeyExists(KeySaveKey))
                CallKey = (KeyCode)ES3.Load<int>(KeySaveKey);
        }

        public void OnEnable()
        {
            // Registering in the game's own customer dictionary makes them callable by id
            // (Extra Customer's list uses it when it's installed). A saved queue that still
            // names one of them after this mod is removed falls back to a vanilla scav, so
            // saves stay loadable.
            StoreClientListDict.storeClientDict[Suppliers.ChemicalMerchantId] = Suppliers.CreateChemicalMerchant;
            StoreClientListDict.storeClientDict[Suppliers.MushroomSellerId] = Suppliers.CreateMushroomSeller;
            ModHook.OnGenerateCustomerNormal += AddMushroomSellerWalkIn;

            // Decides in its Start whether our own hotkey is needed.
            listener = new GameObject("ChemicalSuppliers.Hotkey");
            Object.DontDestroyOnLoad(listener);
            listener.AddComponent<HotkeyListener>();
        }

        public void OnDisable()
        {
            ModHook.OnGenerateCustomerNormal -= AddMushroomSellerWalkIn;
            ModHelper.RemoveGameOptions(Id);
            if (listener != null)
                Object.Destroy(listener);
            StoreClientListDict.storeClientDict.Remove(Suppliers.ChemicalMerchantId);
            StoreClientListDict.storeClientDict.Remove(Suppliers.MushroomSellerId);
        }

        // Extra Customer lists both sellers under its F8 key, so ours is only a fallback for
        // when it isn't running. All mods load in the loader's Awake, so by the listener's
        // Start the list is complete whatever the load order.
        internal static bool ExtraCustomerRunning()
        {
            return System.Linq.Enumerable.Any(ModLoader.Mods, m => m.IsEnabled && m.Manifest.ID == ExtraCustomerId);
        }

        internal static void AddKeyBind()
        {
            ModHelper.AddGameOptionKeyBind(Id, "keybind", "call_supplier",
                () => "Call a chemical supplier",
                () => CallKey,
                key =>
                {
                    CallKey = key;
                    ES3.Save(KeySaveKey, (int)key);
                },
                () => "While the shop is open: adds the Chemical Merchant to the end of today's queue. With Shift: the Shady Mushroom Seller.");
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

        // Adds the seller to the end of today's queue. Returns false when the shop isn't
        // open or the game refused the customer.
        internal static bool Call(System.Func<StoreClient> create)
        {
            PlayerStore store = PlayerStore.Instance;
            if (store == null || store.storeState != PlayerStore.StoreState.OPEN)
            {
                Debug.Log("[ChemicalSuppliers] The shop is not open; open the shutter first.");
                return false;
            }
            StoreClient client = create();
            StoreClientManager manager = store.storeClientManager;
            ModHelper.SetClientSource(client, Id);
            manager.AddClient(client);
            bool added = manager.clientStack.Contains(client);
            Debug.Log("[ChemicalSuppliers] " + (added ? "Added " : "The game refused ") + client.identifier
                      + " (" + manager.clientStack.Count + " waiting).");
            return added;
        }

        // The queue counter is the visual feedback; the sound says whether it worked.
        internal static void Feedback(bool added)
        {
            EmporiumAudioPlayer audio = EmporiumAudioPlayer.Instance;
            if (audio == null)
                return;
            if (added)
                audio.PlayMatched();
            else if (audio.audioNegativeTwoBeep != null)
                audio.audioNegativeTwoBeep.Play();
        }
    }

    internal class HotkeyListener : MonoBehaviour
    {
        private void Start()
        {
            if (ChemicalSuppliersMod.ExtraCustomerRunning())
            {
                Debug.Log("[ChemicalSuppliers] Extra Customer is running; call the sellers from its F8 list.");
                enabled = false;
                return;
            }
            ChemicalSuppliersMod.AddKeyBind();
            Debug.Log("[ChemicalSuppliers] Extra Customer is not running; " + ChemicalSuppliersMod.CallKey + " calls the sellers.");
        }

        private void Update()
        {
            KeyCode key = ChemicalSuppliersMod.CallKey;
            if (key == KeyCode.None || KeyBindHelper.SuppressHotkeys || !Input.GetKeyDown(key))
                return;

            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            System.Func<StoreClient> create = shift ? Suppliers.CreateMushroomSeller : Suppliers.CreateChemicalMerchant;
            ChemicalSuppliersMod.Feedback(ChemicalSuppliersMod.Call(create));
        }
    }
}
