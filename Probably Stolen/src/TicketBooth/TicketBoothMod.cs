// The offline test harness (not shipped) calls the internal logic directly.
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("TicketBoothHarness")]

namespace TicketBooth
{
    // Ticket Booth: the Lottery Representative sells you a retailer licence, the Lottery
    // Board ships stock by phone, lower-level citizens buy tickets from you and come back
    // to cash their wins at your counter. Some of the winners are fakes.
    public class TicketBoothMod : IMod
    {
        internal const string Id = "TicketBooth";

        private static ConfigEntry<int> firstVisitDay;
        private static ConfigEntry<int> licenseFee;
        private static ConfigEntry<int> phoneCooldown;
        private static ConfigEntry<int> buyersPerDay;
        private static ConfigEntry<int> claimantsPerDay;
        private static ConfigEntry<int> forgerChance;
        private static ConfigEntry<bool> memeJackpots;
        private static ConfigEntry<int> shopProfit;
        private static ConfigEntry<int> fakeRefill;

        internal static int FirstVisitDay => firstVisitDay?.Value ?? 8;
        internal static int LicenseFee => licenseFee?.Value ?? 300;
        internal static int PhoneCooldown => phoneCooldown?.Value ?? 5;
        internal static int BuyersPerDay => buyersPerDay?.Value ?? 3;
        internal static int ClaimantsPerDay => claimantsPerDay?.Value ?? 4;
        internal static int ForgerChance => forgerChance?.Value ?? 25;
        internal static bool MemeJackpots => memeJackpots?.Value ?? false;
        internal static int ShopProfit => shopProfit?.Value ?? 10;
        internal static int FakeRefill => fakeRefill?.Value ?? 50;

        public void Init(ModManifest manifest)
        {
            ModConfig config = ModHelper.GetConfig(Id);
            firstVisitDay = config.Define("firstVisitDay", 8, "General",
                () => "Lottery Representative's first visit (day)", 2, 60,
                () => "The day the Lottery Representative first comes to offer you a retailer licence. If your save is already past it, he comes the next day.");
            licenseFee = config.Define("licenseFee", 300, "General",
                () => "Retailer licence fee (credits)", 0, 5000,
                () => "One-time fee for the licence and the Lottery Board's phone number.");
            phoneCooldown = config.Define("phoneCooldown", 5, "General",
                () => "Days between Lottery Board orders", 1, 14,
                () => "How long after an order before the Lottery Board takes the next one.");
            buyersPerDay = config.Define("buyersPerDay", 3, "Customers",
                () => "Ticket buyers per day", 0, 10,
                () => "Lower-level citizens who come in to buy lottery tickets and scratchers, once you are licensed.");
            claimantsPerDay = config.Define("claimantsPerDay", 4, "Customers",
                () => "Most winners cashing in per day", 1, 10,
                () => "Winners beyond this come back on a later day.");
            forgerChance = config.Define("forgerChance", 25, "Customers",
                () => "Forged ticket chance per day (%)", 0, 100,
                () => "Chance each day that someone tries to cash a fake or doctored winner. Only while you have sold tickets in the last week.");
            memeJackpots = config.Define("memeJackpots", false, "Customers",
                () => "Meme mode: customers can hit the jackpot",
                () => "Off: customers never match 5 or 6 numbers. On: they can, and you pay 100,000 or 1,000,000 credits from your till, or the lower levels lynch you.");
            shopProfit = config.Define("shopProfit", 10, "Customers",
                () => "Your average profit on tickets you sell (%)", 0, 15,
                () => "Customers win less often than the game's odds, so that after paying their winners you keep this much on average, as a % of what the board charged you. Big prizes are cut the most. Your shop markup is the ceiling: 15% on normal difficulty, nothing on the hardest ones.");
            fakeRefill = config.Define("fakeRefill", 50, "Customers",
                () => "Fakes in place of cut wins (%)", 0, 100,
                () => "When a customer's win is cut, the chance that a forger comes to your counter in their place.");
        }

        public void OnEnable()
        {
            StoreClientListDict.storeClientDict[Phone.CourierId] = Phone.CreateCourier;
            // Registered so other mods (Extra Customer) can list and call in a ticket buyer.
            StoreClientListDict.storeClientDict[Buyers.ClientId] = Buyers.Create;
            PhoneClientList.DialogFuncs[Phone.DialogFuncId] = Phone.CallDialog;
            ModHook.OnNewGameStarted += OnNewGame;
            ModHook.OnGameLoadedLate += OnGameLoaded;
            ModHook.OnGenerateCustomerNormal += Daily.AddCustomers;
            ModHook.OnGenerateCustomerVeryLate += Daily.ReplaceVanillaRep;
            ModHook.OnCreateTooltipLate += TicketTooltip.Add;
            DevSpawn.Enable();
        }

        public void OnDisable()
        {
            ModHook.OnNewGameStarted -= OnNewGame;
            ModHook.OnGameLoadedLate -= OnGameLoaded;
            ModHook.OnGenerateCustomerNormal -= Daily.AddCustomers;
            ModHook.OnGenerateCustomerVeryLate -= Daily.ReplaceVanillaRep;
            ModHook.OnCreateTooltipLate -= TicketTooltip.Add;
            DevSpawn.Disable();
            ModHelper.RemoveGameOptions(Id);
            StoreClientListDict.storeClientDict.Remove(Phone.CourierId);
            StoreClientListDict.storeClientDict.Remove(Buyers.ClientId);
            PhoneClientList.DialogFuncs.Remove(Phone.DialogFuncId);
        }

        // The game keeps modData when a new game starts, so start clean.
        private static void OnNewGame(string scenarioId)
        {
            State.Clear();
        }

        // Installed on a save that is already past the first visit day: he comes tomorrow.
        private static void OnGameLoaded()
        {
            Spent.RestoreAfterLoad();
            if (!State.Ready || State.Licensed || State.HasPitchDay)
                return;
            if (State.Today >= FirstVisitDay)
                State.PitchDay = State.Today + 1;
        }
    }
}
