using System;
using System.Collections.Generic;
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
        private static ConfigEntry<string> hotkey;
        private GameObject listener;

        public void Init(ModManifest manifest)
        {
            ModConfig config = ModHelper.GetConfig(Id);
            extraPerDay = config.Define(
                "extraPerDay", 1, "General",
                () => "Extra customers each day",
                0, 10,
                () => "Random customers added to the queue every morning, on top of the normal ones. 0 = off.");

            // Drawn as a key-capture button by HotkeyConfigRow; stored as the KeyCode name.
            hotkey = config.Define(
                "addCustomerKey", DefaultKey.ToString(), "General",
                () => "Call a customer",
                null,
                () => "Opens the list of buyers and sellers to add to today's queue (in the morning, before the shutter is up, they are booked and arrive once you open). With Shift: adds a random customer. Click, then press the new key; Esc cancels.");
            HotkeyConfigRow.Entry = hotkey;

            // The game's settings file (also written by Options > Keybinds) has the last word;
            // the mod settings file is kept in step with it.
            KeyCode key = ES3.KeyExists(KeySaveKey) ? (KeyCode)ES3.Load<int>(KeySaveKey) : ParseKey(hotkey.Value);
            SetKey(key);
            hotkey.OnChanged += name => SetKey(ParseKey(name));
        }

        // The one place the hotkey changes, from either settings window.
        private static void SetKey(KeyCode key)
        {
            AddCustomerKey = key;
            if (!ES3.KeyExists(KeySaveKey) || ES3.Load<int>(KeySaveKey) != (int)key)
                ES3.Save(KeySaveKey, (int)key);
            hotkey.Set(key.ToString());
        }

        internal static KeyCode ParseKey(string name)
        {
            return !string.IsNullOrEmpty(name) && Enum.TryParse(name, true, out KeyCode key) ? key : DefaultKey;
        }

        public void OnEnable()
        {
            OwnRegistrations.Add();
            ModHook.OnGenerateCustomerLate += AddDailyExtras;
            ModHook.OnGameLoadedInit += ClearBookings;
            ModHook.OnNewGameStarted += ClearBookingsForNewGame;

            // "keybind" is the game's own Keybinds section, so the hotkey sits with the others.
            ModHelper.AddGameOptionKeyBind(Id, "keybind", "add_customer",
                () => "Call a customer",
                () => AddCustomerKey,
                SetKey,
                () => "Opens the list of buyers and sellers to add to today's queue (in the morning they are booked for when the shutter opens). With Shift: adds a random customer.");

            listener = new GameObject("ExtraCustomer.Hotkey");
            UnityEngine.Object.DontDestroyOnLoad(listener);
            listener.AddComponent<HotkeyListener>();
        }

        public void OnDisable()
        {
            ModHook.OnGenerateCustomerLate -= AddDailyExtras;
            ModHook.OnGameLoadedInit -= ClearBookings;
            ModHook.OnNewGameStarted -= ClearBookingsForNewGame;
            ClearBookings();
            ModHelper.RemoveGameOptions(Id);
            CustomerPicker.Close();
            if (listener != null)
                UnityEngine.Object.Destroy(listener);
            OwnRegistrations.Remove();
        }

        // Runs while the morning queue is built, after the normal walk-ins. Clients
        // added inside a ModHook generate event are tagged with this mod by the game.
        private static void AddDailyExtras(StoreClientManager manager)
        {
            // Customers booked this morning go first; they were picked by hand, so the intro
            // gate below doesn't apply to them (it doesn't for picks while open either).
            List<CustomerCatalog.Entry> due = new List<CustomerCatalog.Entry>(booked);
            booked.Clear();
            foreach (CustomerCatalog.Entry entry in due)
            {
                if (entry == null)
                    AddRandom(manager);
                else
                    AddPicked(entry);
            }

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

        // Picks made in the morning, before the shutter is up: the day's queue is only built
        // when the shutter opens, so they join it then. null = a random customer.
        private static readonly List<CustomerCatalog.Entry> booked = new List<CustomerCatalog.Entry>();

        private static void ClearBookings() => booked.Clear();
        private static void ClearBookingsForNewGame(string scenarioId) => booked.Clear();

        private static bool IsMorning =>
            PlayerStore.Instance != null && PlayerStore.Instance.storeState == PlayerStore.StoreState.MORNING;

        // Whether customers can be called now: while the shop is open, or booked in the morning.
        internal static bool CanCall()
        {
            PlayerStore store = PlayerStore.Instance;
            if (store != null && (store.storeState == PlayerStore.StoreState.OPEN || store.storeState == PlayerStore.StoreState.MORNING))
                return true;
            Debug.Log("[ExtraCustomer] The shop is closed for the night; customers can be called from the morning on.");
            return false;
        }

        private static bool Book(CustomerCatalog.Entry entry)
        {
            booked.Add(entry);
            Debug.Log("[ExtraCustomer] Booked " + (entry == null ? "a random customer" : entry.Id ?? entry.Ids?[0] ?? "a customer")
                      + " for when the shutter opens (" + booked.Count + " booked).");
            return true;
        }

        // Adds one random customer to the end of today's queue. Returns false when the
        // shop is not open or every eligible customer is on cooldown.
        internal static bool AddCustomerNow()
        {
            if (!CanCall())
                return false;
            if (IsMorning)
                return Book(null);
            return AddRandom(PlayerStore.Instance.storeClientManager);
        }

        private static bool AddRandom(StoreClientManager manager)
        {
            StoreClient client = manager.PickClient();
            if (client == null)
            {
                Debug.Log("[ExtraCustomer] No customer available right now (all on cooldown).");
                return false;
            }
            return Enqueue(client);
        }

        // Adds the customer with this game id to the end of today's queue. An explicit
        // pick ignores the visit cooldown that random picks respect.
        internal static bool AddCustomer(CustomerCatalog.Entry entry)
        {
            if (!CanCall())
                return false;
            if (IsMorning)
                return Book(entry);
            return AddPicked(entry);
        }

        private static bool AddPicked(CustomerCatalog.Entry entry)
        {
            string id = entry.PickId();
            if (!CustomerCatalog.Exists(id))
            {
                Debug.LogWarning("[ExtraCustomer] Unknown customer id '" + id + "'.");
                return false;
            }
            StoreClient client;
            try
            {
                // Customers from other mods run that mod's code, which may expect its own state.
                client = StoreClientListDict.CreateStoreClient(id);
                entry.Prepare?.Invoke(client);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ExtraCustomer] Could not create customer '" + id + "': " + e.Message);
                return false;
            }
            if (client == null)
                return false;
            return Enqueue(client);
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
            HotkeyConfigRow.Poll();
            CustomerPicker.Tick();

            KeyCode key = ExtraCustomerMod.AddCustomerKey;
            if (key == KeyCode.None || KeyBindHelper.SuppressHotkeys || !Input.GetKeyDown(key))
                return;
            // A key that types text (when rebound to one) belongs to the focused text field,
            // such as the picker's own search box. Function keys still work while typing.
            if (!(key >= KeyCode.F1 && key <= KeyCode.F15) && StoreUIManager.IsAnyTyping())
                return;

            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (shift)
            {
                ExtraCustomerMod.Feedback(ExtraCustomerMod.AddCustomerNow());
                return;
            }

            if (CustomerPicker.IsOpen)
                CustomerPicker.Close();
            else if (ExtraCustomerMod.CanCall())
                CustomerPicker.Toggle();
            else
                ExtraCustomerMod.Feedback(false);
        }
    }
}
