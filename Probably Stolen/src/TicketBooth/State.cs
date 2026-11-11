using System.Collections.Generic;

namespace TicketBooth
{
    // The mod's per-save data, kept in the game's own save (PlayerStore.modData), so it
    // follows the save file and nothing is written anywhere else.
    internal static class State
    {
        private const string Id = TicketBoothMod.Id;

        internal static bool Ready => PlayerStore.Instance != null && StoreStation.Instance != null;
        internal static int Today => StoreStation.Instance.dayCounter;

        internal static bool Licensed
        {
            get => PlayerStore.Instance != null && ModHelper.GetModDataBool(Id, "licensed");
            set => ModHelper.SetModDataBool(Id, "licensed", value);
        }

        internal static int LicenseDay
        {
            get => ModHelper.GetModDataInt(Id, "licenseDay");
            set => ModHelper.SetModDataInt(Id, "licenseDay", value);
        }

        // The day the rep next comes to pitch the licence. Unset until he has been
        // scheduled, so the configured first visit day applies until then.
        internal static bool HasPitchDay => ModHelper.HasModData(Id, "pitchDay");

        internal static int PitchDay
        {
            get => ModHelper.GetModDataInt(Id, "pitchDay", TicketBoothMod.FirstVisitDay);
            set => ModHelper.SetModDataInt(Id, "pitchDay", value);
        }

        internal static int LastSaleDay
        {
            get => ModHelper.GetModDataInt(Id, "lastSaleDay", -1000);
            set => ModHelper.SetModDataInt(Id, "lastSaleDay", value);
        }

        internal static int LastDrawEdition
        {
            get => ModHelper.GetModDataInt(Id, "lastDrawEdition");
            set => ModHelper.SetModDataInt(Id, "lastDrawEdition", value);
        }

        internal static string LastDrawNumbers
        {
            get => ModHelper.GetModData(Id, "lastDrawNumbers", "");
            set => ModHelper.SetModData(Id, "lastDrawNumbers", value);
        }

        internal static int LastDrawDay
        {
            get => ModHelper.GetModDataInt(Id, "lastDrawDay");
            set => ModHelper.SetModDataInt(Id, "lastDrawDay", value);
        }

        internal static int NextClaimId()
        {
            int id = ModHelper.GetModDataInt(Id, "nextClaimId", 1);
            ModHelper.SetModDataInt(Id, "nextClaimId", id + 1);
            return id;
        }

        internal static List<Claim> Claims
        {
            get => Claim.ParseAll(ModHelper.GetModData(Id, "claims", ""));
            set => ModHelper.SetModData(Id, "claims", Claim.SerializeAll(value));
        }

        internal static void AddClaim(Claim claim)
        {
            List<Claim> claims = Claims;
            claims.Add(claim);
            Claims = claims;
        }

        internal static void RemoveClaim(int id)
        {
            List<Claim> claims = Claims;
            claims.RemoveAll(c => c.Id == id);
            Claims = claims;
        }

        internal static List<LedgerEntry> Ledger
        {
            get => LedgerEntry.ParseAll(ModHelper.GetModData(Id, "ledger", ""));
            set => ModHelper.SetModData(Id, "ledger", LedgerEntry.SerializeAll(value));
        }

        internal static void Clear()
        {
            ModHelper.ClearModData(Id);
        }
    }
}
