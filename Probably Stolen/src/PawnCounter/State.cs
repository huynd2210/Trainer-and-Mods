using System.Collections.Generic;
using System.Linq;

namespace PawnCounter
{
    internal static class Tags
    {
        internal const string PawnId = "PC_PAWN_ID";        // on an item you hold as a pawn
        internal const string OfferLoan = "PC_OFFER_LOAN";  // on an item a pawner has put on the counter
        internal const string OfferRedeem = "PC_OFFER_REDEEM";
        internal const string OfferDue = "PC_OFFER_DUE";
    }

    // The mod's per-save data, kept in the game's own save (PlayerStore.modData).
    internal static class State
    {
        private const string Id = PawnCounterMod.Id;

        internal static bool Ready => PlayerStore.Instance != null && StoreStation.Instance != null;
        internal static int Today => StoreStation.Instance.dayCounter;

        // Parsed once per change of the stored text, so tooltips cost nothing.
        private static string cachedText;
        private static List<Pawn> cached = new List<Pawn>();

        internal static List<Pawn> Pawns
        {
            get
            {
                string text = ModHelper.GetModData(Id, "pawns", "");
                if (text != cachedText)
                {
                    cachedText = text;
                    cached = Pawn.ParseAll(text);
                }
                return cached.ToList();
            }
            set => ModHelper.SetModData(Id, "pawns", Pawn.SerializeAll(value));
        }

        internal static Pawn Find(int id)
        {
            return Pawns.FirstOrDefault(p => p.Id == id);
        }

        internal static void Add(Pawn pawn)
        {
            List<Pawn> pawns = Pawns;
            pawns.Add(pawn);
            Pawns = pawns;
        }

        internal static void Remove(int id)
        {
            List<Pawn> pawns = Pawns;
            pawns.RemoveAll(p => p.Id == id);
            Pawns = pawns;
        }

        internal static int NextId()
        {
            int id = ModHelper.GetModDataInt(Id, "nextId", 1);
            ModHelper.SetModDataInt(Id, "nextId", id + 1);
            return id;
        }

        internal static void Clear()
        {
            ModHelper.ClearModData(Id);
            cachedText = null;
        }
    }
}
