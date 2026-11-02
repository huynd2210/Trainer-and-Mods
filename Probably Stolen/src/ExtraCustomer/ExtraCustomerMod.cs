using UnityEngine;

namespace ExtraCustomer
{
    public class ExtraCustomerMod : IMod
    {
        internal const string Id = "ExtraCustomer";

        // Stored next to the game's own "keybind_<id>" entries in its settings file.
        private const string KeySaveKey = "mod_ExtraCustomer_addCustomerKey";
        private const KeyCode DefaultKey = KeyCode.F8;

        internal static KeyCode AddCustomerKey = DefaultKey;

        private static ConfigEntry<int> extraPerDay;
        private GameObject listener;

        public void Init(ModManifest manifest)
        {
            extraPerDay = ModHelper.GetConfig(Id).Define(
                "extraPerDay", 1, "General",
                () => "Extra customers each day",
                0, 10,
                () => "Random customers added to the queue every morning, on top of the normal ones. 0 = off.");

            if (ES3.KeyExists(KeySaveKey))
                AddCustomerKey = (KeyCode)ES3.Load<int>(KeySaveKey);
        }

        public void OnEnable()
        {
            ModHook.OnGenerateCustomerLate += AddDailyExtras;

            // "keybind" is the game's own Keybinds section, so the hotkey sits with the others.
            ModHelper.AddGameOptionKeyBind(Id, "keybind", "add_customer",
                () => "Call a customer",
                () => AddCustomerKey,
                key =>
                {
                    AddCustomerKey = key;
                    ES3.Save(KeySaveKey, (int)key);
                },
                () => "While the shop is open: opens the list of buyers and sellers to add to today's queue. With Shift: adds a random customer.");

            listener = new GameObject("ExtraCustomer.Hotkey");
            Object.DontDestroyOnLoad(listener);
            listener.AddComponent<HotkeyListener>();
        }

        public void OnDisable()
        {
            ModHook.OnGenerateCustomerLate -= AddDailyExtras;
            ModHelper.RemoveGameOptions(Id);
            CustomerPicker.Close();
            if (listener != null)
                Object.Destroy(listener);
        }

        // Runs while the morning queue is built, after the normal walk-ins. Clients
        // added inside a ModHook generate event are tagged with this mod by the game.
        private static void AddDailyExtras(StoreClientManager manager)
        {
            // Same gate as the vanilla random walk-ins (HandleNormalClient): the intro
            // days only get scripted customers.
            PlayerStore store = PlayerStore.Instance;
            int day = StoreStation.Instance.dayCounter;
            if ((!store.skipIntro && day <= 13) || (store.skipIntro && day == 1))
                return;

            for (int i = 0; i < extraPerDay.Value; i++)
            {
                StoreClient client = manager.PickClient();
                if (client == null)
                    break;
                manager.AddClient(client);
            }
        }

        internal static bool IsShopOpen()
        {
            PlayerStore store = PlayerStore.Instance;
            if (store != null && store.storeState == PlayerStore.StoreState.OPEN)
                return true;
            Debug.Log("[ExtraCustomer] The shop is not open; open the shutter first.");
            return false;
        }

        // Adds one random customer to the end of today's queue. Returns false when the
        // shop is not open or every eligible customer is on cooldown.
        internal static bool AddCustomerNow()
        {
            if (!IsShopOpen())
                return false;

            StoreClient client = PlayerStore.Instance.storeClientManager.PickClient();
            if (client == null)
            {
                Debug.Log("[ExtraCustomer] No customer available right now (all on cooldown).");
                return false;
            }
            return Enqueue(client);
        }

        // Adds the customer with this game id to the end of today's queue. An explicit
        // pick ignores the visit cooldown that random picks respect.
        internal static bool AddCustomer(string clientId)
        {
            if (!IsShopOpen())
                return false;
            if (!CustomerCatalog.Exists(clientId))
            {
                Debug.LogWarning("[ExtraCustomer] Unknown customer id '" + clientId + "'.");
                return false;
            }
            return Enqueue(StoreClientListDict.CreateStoreClient(clientId));
        }

        private static bool Enqueue(StoreClient client)
        {
            StoreClientManager manager = PlayerStore.Instance.storeClientManager;
            ModHelper.SetClientSource(client, Id);
            // AddClient silently refuses some customers (starting perks such as Known Snitch).
            manager.AddClient(client);
            bool added = manager.clientStack.Contains(client);
            Debug.Log("[ExtraCustomer] " + (added ? "Added " : "The game refused ") + client.identifier
                      + " (" + manager.clientStack.Count + " waiting).");
            return added;
        }

        // The queue counter is the visual feedback; the sound says whether it worked,
        // since nothing changes on screen when it did not.
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
        private void Update()
        {
            KeyCode key = ExtraCustomerMod.AddCustomerKey;
            if (key == KeyCode.None || KeyBindHelper.SuppressHotkeys || !Input.GetKeyDown(key))
                return;

            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (shift)
            {
                ExtraCustomerMod.Feedback(ExtraCustomerMod.AddCustomerNow());
                return;
            }

            if (CustomerPicker.IsOpen)
                CustomerPicker.Close();
            else if (ExtraCustomerMod.IsShopOpen())
                CustomerPicker.Toggle();
            else
                ExtraCustomerMod.Feedback(false);
        }
    }
}
