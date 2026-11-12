// The offline test harness (not shipped) calls the internal logic directly.
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("PawnCounterHarness")]

namespace PawnCounter
{
    // Pawn Counter: customers short on cash leave a valuable, bulky item with you against a
    // loan and come back to buy it back for more. Or don't, and it's yours.
    public class PawnCounterMod : IMod
    {
        internal const string Id = "PawnCounter";

        private static ConfigEntry<int> firstDay;
        private static ConfigEntry<int> chance;
        private static ConfigEntry<int> maxActive;
        private static ConfigEntry<int> falloff;
        private static ConfigEntry<int> loanPercent;
        private static ConfigEntry<int> lossPremiumPercent;
        private static ConfigEntry<int> storageFee;
        private static ConfigEntry<int> riskFee;
        private static ConfigEntry<int> minValue;
        private static ConfigEntry<int> boxChance;
        private static ConfigEntry<int> lockedChance;

        internal static int FirstDay => firstDay?.Value ?? 3;
        internal static int Chance => chance?.Value ?? 45;
        internal static int MaxActive => maxActive?.Value ?? 3;
        internal static int Falloff => falloff?.Value ?? 35;
        internal static int LoanPercent => loanPercent?.Value ?? 55;
        internal static int LossPremiumPercent => lossPremiumPercent?.Value ?? 25;
        internal static Terms.Rates FeeRates => new Terms.Rates
        {
            PerCell = storageFee?.Value ?? 2,
            PerRisk = riskFee?.Value ?? 10,
        };
        internal static int MinValue => minValue?.Value ?? 150;
        internal static int BoxChance => boxChance?.Value ?? 65;
        internal static int LockedChance => lockedChance?.Value ?? 50;

        public void Init(ModManifest manifest)
        {
            ModConfig config = ModHelper.GetConfig(Id);
            firstDay = config.Define("firstDay", 3, "Pawners",
                () => "First day pawners come in", 1, 60,
                () => "Pawners start coming from this day on.");
            chance = config.Define("chance", 45, "Pawners",
                () => "Chance of a pawner each day (%)", 0, 100,
                () => "Chance each morning that someone comes in to pawn something.");
            maxActive = config.Define("maxActive", 3, "Pawners",
                () => "Pawns held before pawners fall off", 1, 10,
                () => "Hold this many pawned items and the daily chance starts dropping steeply (see below).");
            falloff = config.Define("falloffPercent", 35, "Pawners",
                () => "Falloff per pawn past the limit (%)", 0, 100,
                () => "Each pawn you hold from the limit on multiplies the daily chance by this. At 35% with a 45% chance and a limit of 3: "
                      + "16% holding 3, 6% holding 4, 2% holding 5. 0 = none until one is settled.");
            minValue = config.Define("minValue", 150, "Pawners",
                () => "Cheapest item a pawner brings (credits)", 0, 2000,
                () => "Items worth less than this are never brought in. The pool is already limited to valuable, bulky things.");
            boxChance = config.Define("boxChance", 65, "Pawners",
                () => "Chance a pawner brings a box (%)", 0, 100,
                () => "Chance that what a pawner brings is a packed crate rather than a single item.");
            lockedChance = config.Define("lockedChance", 50, "Pawners",
                () => "Chance a pawned box is locked (%)", 0, 100,
                () => "Share of pawned boxes that arrive locked. Unlocked ones let you see what's inside before you lend. "
                      + "Unlocking a pawned box is fine; taking anything out of it counts as selling it.");
            loanPercent = config.Define("loanPercent", 55, "Terms",
                () => "Loan asked, as % of the item's worth", 20, 90,
                () => "What a pawner asks to borrow, as a share of what the item is worth. Each pawner varies by 10% either way.");
            // What you earn on a pawn: a holding fee on top of the loan, by size and risk only.
            storageFee = config.Define("storageFeePerCell", 2, "Terms",
                () => "Storage fee (credits per cell per day)", 0, 20,
                () => "For each grid cell the item takes up, the owner pays this much per day you hold it. Applies to new pawns only.");
            riskFee = config.Define("riskFeePerLevel", 10, "Terms",
                () => "Risk fee (credits per risk level per day)", 0, 100,
                () => "For each risk level, the owner pays this much per day you hold it. Risk is the item's contraband level "
                      + "(low 1, mid 2, high 3, critical 4), plus 1 if it is stolen. Applies to new pawns only.");
            lossPremiumPercent = config.Define("lossPremiumPercent", 25, "Terms",
                () => "Sold-pawn penalty (%)", 5, 200,
                () => "If you sell a pawned item (or it is seized) before the owner comes back, they want its full worth plus whatever you got for it, and this much more on top of both. They keep the loan.");
        }

        public void OnEnable()
        {
            ModHook.OnNewGameStarted += OnNewGame;
            ModHook.OnGameLoadedLate += Daily.OnGameLoaded;
            ModHook.OnGenerateCustomerNormal += Daily.AddCustomers;
            ModHook.OnCreateTooltipLate += PawnTooltip.Add;
        }

        public void OnDisable()
        {
            ModHook.OnNewGameStarted -= OnNewGame;
            ModHook.OnGameLoadedLate -= Daily.OnGameLoaded;
            ModHook.OnGenerateCustomerNormal -= Daily.AddCustomers;
            ModHook.OnCreateTooltipLate -= PawnTooltip.Add;
            ModHelper.RemoveGameOptions(Id);
        }

        // The game keeps modData when a new game starts, so start clean.
        private static void OnNewGame(string scenarioId)
        {
            State.Clear();
        }
    }
}
