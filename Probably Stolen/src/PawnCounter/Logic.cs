using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PawnCounter
{
    internal interface IRng
    {
        int Range(int minInclusive, int maxInclusive);
    }

    internal sealed class SystemRng : IRng
    {
        private readonly Random random;
        internal SystemRng(int seed) { random = new Random(seed); }
        public int Range(int minInclusive, int maxInclusive) { return random.Next(minInclusive, maxInclusive + 1); }
    }

    // One loan you made: the item you hold, what you lent, what the owner pays to get it back.
    // Kept in the save as text (PlayerStore.modData).
    internal sealed class Pawn
    {
        internal int Id;
        internal int Uid;            // GameItem.uniqueId of the item you hold; survives save/load
        internal string Kind = "";   // PawnerKind.Id
        internal string Owner = "";
        internal string Sprite = "";
        internal string ItemName = "";
        internal int Worth;          // the item's value when you took it
        internal int Loan;
        internal int Redeem;
        internal int DueDay;
        internal bool WillReturn;    // decided when you take the item, so reloading the morning changes nothing
        internal int SoldFor;        // what you got for it if you let it go (sold, bartered, handed in); 0 if seized or still held

        internal int Compensation => Terms.Compensation(Worth, SoldFor, PawnCounterMod.LossPremiumPercent);

        internal string Serialize()
        {
            return string.Join("|", new[]
            {
                Id.ToString(CultureInfo.InvariantCulture), Uid.ToString(CultureInfo.InvariantCulture), Clean(Kind), Clean(Owner), Clean(Sprite),
                Clean(ItemName), Worth.ToString(CultureInfo.InvariantCulture), Loan.ToString(CultureInfo.InvariantCulture),
                Redeem.ToString(CultureInfo.InvariantCulture), DueDay.ToString(CultureInfo.InvariantCulture), WillReturn ? "1" : "0",
                SoldFor.ToString(CultureInfo.InvariantCulture),
            });
        }

        internal static Pawn Parse(string line)
        {
            string[] f = line.Split('|');
            // 11 fields: saved by 1.0/1.1, before SoldFor existed.
            if (f.Length != 11 && f.Length != 12)
                return null;
            int id = Int(f[0]), uid = Int(f[1]);
            if (id <= 0 || uid == 0)
                return null;
            return new Pawn
            {
                Id = id, Uid = uid, Kind = f[2], Owner = f[3], Sprite = f[4], ItemName = f[5],
                Worth = Int(f[6]), Loan = Int(f[7]), Redeem = Int(f[8]), DueDay = Int(f[9]), WillReturn = f[10] == "1",
                SoldFor = f.Length > 11 ? Int(f[11]) : 0,
            };
        }

        internal static List<Pawn> ParseAll(string text)
        {
            List<Pawn> list = new List<Pawn>();
            if (string.IsNullOrEmpty(text))
                return list;
            foreach (string line in text.Split(';'))
            {
                Pawn p = Parse(line);
                if (p != null)
                    list.Add(p);
            }
            return list;
        }

        internal static string SerializeAll(IEnumerable<Pawn> pawns)
        {
            return string.Join(";", pawns.Select(p => p.Serialize()));
        }

        private static string Clean(string s)
        {
            return (s ?? "").Replace("|", "/").Replace(";", ",");
        }

        private static int Int(string s)
        {
            return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : 0;
        }
    }

    // What a pawner asks for, and what the deal is worth to each side.
    internal static class Terms
    {
        internal const int LoanSpread = 10;     // the asked loan varies +-10% of worth around the setting
        internal const int MinTermDays = 3;
        internal const int MaxTermDays = 6;
        internal const int HagglePercent = 75;  // your counter-offer: 75% of what they asked
        internal const int GraceDays = 2;       // a returning owner keeps trying this long after the due day

        internal sealed class Offer
        {
            internal int Loan;
            internal int Fee;     // what you earn: Redeem - Loan
            internal int Redeem;
            internal int Days;
        }

        // What you charge for holding an item, by day: shelf space it takes and how much
        // trouble it can bring you, never what it's worth.
        internal sealed class Rates
        {
            internal int PerCell;   // credits per grid cell per day
            internal int PerRisk;   // credits per risk level per day
        }

        // The loan is what the owner needs, so it still follows the item's worth. The fee on
        // top follows only its size and risk, over the days you hold it.
        internal static Offer Make(IRng rng, int worth, int loanPercent, int cells, int risk, Rates rates)
        {
            int pct = Clamp(loanPercent + rng.Range(-LoanSpread, LoanSpread), 10, 95);
            int loan = Math.Max(5, Round5(worth * pct / 100.0));
            int days = rng.Range(MinTermDays, MaxTermDays);
            int fee = Fee(cells, risk, days, rates);
            return new Offer { Loan = loan, Fee = fee, Redeem = loan + fee, Days = days };
        }

        internal static int Fee(int cells, int risk, int days, Rates rates)
        {
            return Math.Max(5, Round5(StoragePerDay(cells, rates) * days + RiskPerDay(risk, rates) * days));
        }

        internal static int StoragePerDay(int cells, Rates rates) => Math.Max(0, cells) * Math.Max(0, rates.PerCell);

        internal static int RiskPerDay(int risk, Rates rates) => Math.Max(0, risk) * Math.Max(0, rates.PerRisk);

        // How much trouble holding it can bring: its contraband level (1 low .. 4 critical),
        // plus one if it is stolen. Either can be taken by the inspector.
        internal static int Risk(int contrabandLevel, bool stolen)
        {
            return Math.Max(0, contrabandLevel) + (stolen ? 1 : 0);
        }

        internal static int Haggled(int loan)
        {
            return Math.Max(5, Round5(loan * HagglePercent / 100.0));
        }

        // Sold or lost before the owner came back: you owe them the item's full worth, plus
        // whatever you got for it, plus a premium on both. Always more than worth + proceeds,
        // so letting a pawn go early never pays. They keep the loan, which was theirs against
        // an item you were only holding.
        internal static int Compensation(int worth, int soldFor, int premiumPercent)
        {
            int owed = worth + Math.Max(0, soldFor);
            return Math.Max(owed + 5, owed + Round5(owed * premiumPercent / 100.0));
        }

        // Refusing to make it right costs reputation with the owner's people, more for more
        // money. Steep on purpose: selling early and refusing would otherwise beat honest
        // holding by a wide margin.
        internal static int ReputationLoss(int compensation)
        {
            return Clamp(5 + compensation / 20, 5, 25);
        }

        internal static int Round5(double v)
        {
            return (int)(Math.Round(v / 5.0, MidpointRounding.AwayFromZero) * 5);
        }

        internal static int Clamp(int v, int lo, int hi)
        {
            return v < lo ? lo : (v > hi ? hi : v);
        }
    }

    // Who comes in to pawn something: their faction, what they own, how reliable they are.
    // Data only; the game-side setup (faction, sprite) is looked up by Id in Pool.
    internal sealed class PawnerKind
    {
        internal string Id;
        internal int Weight;
        internal int MinDay;
        internal string[] Items;
        internal int ReturnChance;   // % that they come back for it
        internal int HaggleChance;   // % that they take your lower offer
        internal int StolenChance;   // % that what they bring is stolen
        internal string[] Openers;
        internal string[] Thanks;
        internal string[] Angry;

        internal static readonly PawnerKind[] All =
        {
            new PawnerKind
            {
                Id = "lower", Weight = 40, MinDay = 0, ReturnChance = 88, HaggleChance = 60, StolenChance = 15,
                Items = new[] { "metal_scanner", "aquascan", "plant_analyser", "combat_machete", "stun_baton", "surgery_tool", "backpack_medium",
                                "loot_crate_service", "loot_crate_medical" },
                Openers = new[] { "Rent's due and the landlord doesn't take excuses.", "Times are hard. I need cash, today.", "I'm not selling it. I just need a loan." },
                Thanks = new[] { "Knew I could count on you.", "Thanks. I'll remember this." },
                Angry = new[] { "The whole level will hear about this.", "Thief. Everyone down below will know." },
            },
            new PawnerKind
            {
                Id = "tech", Weight = 30, MinDay = 0, ReturnChance = 92, HaggleChance = 45, StolenChance = 5,
                Items = new[] { "chem_scanner", "gene_scanner", "module_extractor_advanced", "rare_electronic", "metal_scanner", "xray_scanner", "aug_scanner", "turbo_booster_adv",
                                "loot_crate_engineering" },
                Openers = new[] { "Shift pay got delayed again.", "I need credits until payday. This is good collateral.", "I need this back, so treat it well." },
                Thanks = new[] { "Good. Back to work, then.", "Pleasure doing business." },
                Angry = new[] { "The crews talk. They'll hear what you do.", "Unbelievable. Never again." },
            },
            new PawnerKind
            {
                Id = "upper", Weight = 15, MinDay = 10, ReturnChance = 95, HaggleChance = 25, StolenChance = 0,
                Items = new[] { "self_refilling_neuroactive_perfume", "gene_scanner", "hypertech_electronic", "xray_scanner" },
                Openers = new[] { "A temporary cash flow problem. Nothing more.", "Discretion, please. I need liquid credits.", "Consider it a favour. I will be generous when I return." },
                Thanks = new[] { "Discreet and reliable. Good.", "As expected. Good day." },
                Angry = new[] { "My friends upstairs will hear about this.", "You will regret this." },
            },
            new PawnerKind
            {
                Id = "shady", Weight = 15, MinDay = 7, ReturnChance = 87, HaggleChance = 55, StolenChance = 35,
                Items = new[] { "revolver", "shotgun", "smg", "heavy_handmade_pistol", "stun_gun", "rare_electronic",
                                "glock_receiver", "c4", "canister_mbs", "backpack_large_military",
                                "loot_crate_security", "loot_crate_evidence" },
                Openers = new[] { "Don't ask where it's from. Ask what it's worth.", "I need cash quick, and I'll be back for it.", "Keep this out of sight, yeah?" },
                Thanks = new[] { "Good. You're alright.", "No questions asked. I like that." },
                Angry = new[] { "People like me don't forget.", "You just made enemies." },
            },
        };

        internal static PawnerKind Get(string id)
        {
            return All.FirstOrDefault(k => k.Id == id) ?? All[0];
        }

        internal static PawnerKind PickFor(IRng rng, int day)
        {
            List<PawnerKind> open = All.Where(k => day >= k.MinDay).ToList();
            int total = open.Sum(k => k.Weight);
            int roll = rng.Range(1, total);
            foreach (PawnerKind k in open)
            {
                roll -= k.Weight;
                if (roll <= 0)
                    return k;
            }
            return open[open.Count - 1];
        }

        internal static readonly string[] Names =
        {
            "Rhea", "Dario", "Mila", "Teo", "Ines", "Bram", "Yuki", "Oskar", "Lena", "Kofi", "Sana", "Pavel",
            "Noor", "Elias", "Vera", "Tomas", "Ada", "Jun", "Marta", "Ravi", "Celia", "Igor", "Hana", "Luca",
        };

        internal static string Pick(IRng rng, string[] lines)
        {
            return lines[rng.Range(0, lines.Length - 1)];
        }
    }
}
