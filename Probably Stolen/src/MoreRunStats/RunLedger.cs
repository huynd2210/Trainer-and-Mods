using System;

namespace MoreRunStats
{
    // A contraband bucket: the same split and labels as SecData.CheckSoldContraband and
    // SecData.GetCrimeString, so the rows line up with the Security tab's record.
    internal sealed class ContrabandKind
    {
        public readonly string Key;
        public readonly string ItemType;
        public readonly string LabelKey;

        public ContrabandKind(string key, string itemType, string labelKey)
        {
            Key = key;
            ItemType = itemType;
            LabelKey = labelKey;
        }

        public string Label => LocHelper.GetLocalizedMechanic(LabelKey);
        public string CountKey => "contraband." + Key + ".count";
        public string CreditsKey => "contraband." + Key + ".credits";
    }

    // This run's totals. They live in the save, in PlayerStore.modData.
    internal static class RunLedger
    {
        internal const string ModId = "MoreRunStats";

        // Checked in order; the last one (no item type) takes everything else.
        internal static readonly ContrabandKind[] Kinds =
        {
            new ContrabandKind("drug", "SUBSTANCE", "sec_data_crime_drug"),
            new ContrabandKind("weapon", "WEAPON", "sec_data_crime_weapon"),
            new ContrabandKind("card", "ACCESS_CARD", "sec_data_crime_card"),
            new ContrabandKind("other", null, "sec_data_crime_contraband"),
        };

        internal const string StolenCount = "stolen.count";
        internal const string StolenCredits = "stolen.credits";
        internal const string ScamCount = "scam.count";
        internal const string ScamCredits = "scam.credits";
        internal const string BiggestScamItem = "scam.biggest.item";
        internal const string BiggestScamValue = "scam.biggest.value";

        internal static ContrabandKind KindOf(GameItem item)
        {
            foreach (ContrabandKind kind in Kinds)
            {
                if (kind.ItemType == null || item.IsGameItemType(kind.ItemType))
                    return kind;
            }
            return Kinds[Kinds.Length - 1];
        }

        internal static int Get(string key) => ModHelper.GetModDataInt(ModId, key);

        internal static string GetText(string key) => ModHelper.GetModData(ModId, key, "");

        internal static void Add(string key, long amount) =>
            ModHelper.SetModDataInt(ModId, key, (int)Math.Min(int.MaxValue, Get(key) + amount));

        internal static void RecordContraband(GameItem item, long credits)
        {
            ContrabandKind kind = KindOf(item);
            Add(kind.CountKey, 1);
            Add(kind.CreditsKey, credits);
        }

        internal static void RecordStolen(long credits)
        {
            Add(StolenCount, 1);
            Add(StolenCredits, credits);
        }

        internal static void RecordScam(string itemName, long gain)
        {
            Add(ScamCount, 1);
            Add(ScamCredits, gain);
            if (gain > Get(BiggestScamValue))
            {
                ModHelper.SetModDataInt(ModId, BiggestScamValue, (int)Math.Min(int.MaxValue, gain));
                ModHelper.SetModData(ModId, BiggestScamItem, itemName);
            }
        }

        internal static void Clear() => ModHelper.ClearModData(ModId);
    }
}
