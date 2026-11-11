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
        private static ConfigEntry<int> chanceWhenFull;
        private static ConfigEntry<int> loanPercent;
        private static ConfigEntry<int> lossPremiumPercent;
        private static ConfigEntry<int> storageFee;
        private static ConfigEntry<int> riskFee;
        private static ConfigEntry<int> minValue;

        internal static int FirstDay => firstDay?.Value ?? 3;
        internal static int Chance => chance?.Value ?? 45;
        internal static int MaxActive => maxActive?.Value ?? 3;
        internal static int ChanceWhenFull => chanceWhenFull?.Value ?? 10;
        internal static int LoanPercent => loanPercent?.Value ?? 55;
        internal static int LossPremiumPercent => lossPremiumPercent?.Value ?? 25;
        internal static Terms.Rates FeeRates => new Terms.Rates
        {
            PerCell = storageFee?.Value ?? 2,
            PerRisk = riskFee?.Value ?? 10,
        };
        internal static int MinValue => minValue?.Value ?? 150;

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
                () => "Pawns held before pawners slow down", 1, 10,
                () => "While you hold this many pawned items or more, the daily chance drops to the one below.");
            chanceWhenFull = config.Define("chanceWhenFull", 10, "Pawners",
                () => "Chance of a pawner each day when full (%)", 0, 100,
                () => "Chance each morning of a pawner while you hold the number of pawns above. 0 = none until one is settled.");
            minValue = config.Define("minValue", 150, "Pawners",
                () => "Cheapest item a pawner brings (credits)", 0, 2000,
                () => "Items worth less than this are never brought in. The pool is already limited to valuable, bulky things.");
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
