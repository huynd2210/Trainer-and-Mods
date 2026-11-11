using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TicketBooth
{
    // Everything in this file is plain C# with no game or Unity calls, so the offline
    // harness can run it. Randomness comes in through IRng.
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

    // One winning ticket that a customer brings to the counter. Saved in the mod's
    // per-save data as one '|'-separated line; claims are joined with ';'.
    internal sealed class Claim
    {
        internal const char Lottery = 'L';
        internal const char Scratcher = 'S';

        internal int Id;
        internal char Kind;
        internal int Asked;          // what the claimant asks to be paid
        internal int DueDay;         // first day they come to the counter
        internal string Data = "";   // lottery: "03 11 17 22 40 48"; scratcher: "2,2,5"
        internal int Edition;        // lottery only
        internal string Winning = ""; // lottery only: the draw the ticket is coloured against
        internal int StampDay = -1;  // retailer stamp; -1 = none
        internal bool Honest;        // a real winner asking for exactly what it won
        internal bool Forged;        // the ticket itself is fake: the board won't redeem it
        internal string Flaw = "";   // which trick a dishonest claim uses (for logs and tests)
        internal int GlueSlot;       // scratcher symbol that was re-glued, 1..3; 0 = none

        internal string Serialize()
        {
            return string.Join("|", new[]
            {
                Id.ToString(CultureInfo.InvariantCulture),
                Kind.ToString(),
                Asked.ToString(CultureInfo.InvariantCulture),
                DueDay.ToString(CultureInfo.InvariantCulture),
                Data,
                Edition.ToString(CultureInfo.InvariantCulture),
                Winning,
                StampDay.ToString(CultureInfo.InvariantCulture),
                Honest ? "1" : "0",
                Forged ? "1" : "0",
                Flaw,
                GlueSlot.ToString(CultureInfo.InvariantCulture),
            });
        }

        internal static Claim Parse(string line)
        {
            string[] f = line.Split('|');
            if (f.Length < 12 || f[1].Length != 1)
                return null;
            try
            {
                return new Claim
                {
                    Id = Int(f[0]),
                    Kind = f[1][0],
                    Asked = Int(f[2]),
                    DueDay = Int(f[3]),
                    Data = f[4],
                    Edition = Int(f[5]),
                    Winning = f[6],
                    StampDay = Int(f[7]),
                    Honest = f[8] == "1",
                    Forged = f[9] == "1",
                    Flaw = f[10],
                    GlueSlot = Int(f[11]),
                };
            }
            catch (FormatException)
            {
                return null;
            }
        }

        internal static List<Claim> ParseAll(string text)
        {
            List<Claim> list = new List<Claim>();
            if (string.IsNullOrEmpty(text))
                return list;
            foreach (string line in text.Split(';'))
            {
                Claim claim = Parse(line);
                if (claim != null)
                    list.Add(claim);
            }
            return list;
        }

        internal static string SerializeAll(IEnumerable<Claim> claims)
        {
            return string.Join(";", claims.Select(c => c.Serialize()));
        }

        internal int[] Symbols()
        {
            return Data.Split(',').Select(Int).ToArray();
        }

        private static int Int(string s)
        {
            return int.Parse(s, NumberStyles.Integer, CultureInfo.InvariantCulture);
        }
    }

    // A lottery ticket a customer bought from you, waiting for its draw.
    internal sealed class LedgerEntry
    {
        internal int Edition;
        internal string Numbers = "";
        internal int SoldDay;
        internal int Paid;           // what the customer paid; 0 = sold before this was saved

        internal string Serialize()
        {
            return Edition.ToString(CultureInfo.InvariantCulture) + "|" + Numbers + "|" + SoldDay.ToString(CultureInfo.InvariantCulture)
                   + "|" + Paid.ToString(CultureInfo.InvariantCulture);
        }

        internal static List<LedgerEntry> ParseAll(string text)
        {
            List<LedgerEntry> list = new List<LedgerEntry>();
            if (string.IsNullOrEmpty(text))
                return list;
            foreach (string line in text.Split(';'))
            {
                string[] f = line.Split('|');
                if (f.Length < 3)
                    continue;
                if (int.TryParse(f[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int edition)
                    && int.TryParse(f[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int day))
                {
                    int paid = 0;
                    if (f.Length > 3)
                        int.TryParse(f[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out paid);
                    list.Add(new LedgerEntry { Edition = edition, Numbers = f[1], SoldDay = day, Paid = paid });
                }
            }
            return list;
        }

        internal static string SerializeAll(IEnumerable<LedgerEntry> entries)
        {
            return string.Join(";", entries.Select(e => e.Serialize()));
        }
    }

    // What refusing to pay an honest winner costs. Bands follow the user's numbers:
    // 35 cr -> 3, 300 cr -> 15, 2,500 cr -> 250, a jackpot -> the mob.
    internal struct Penalty
    {
        internal int ReputationLoss;
        internal bool Lynch;
    }

    internal static class Odds
    {
        internal const int NutrifruitSymbol = 1;

        // What the board charges you: LotteryHelper.LOTTERY_TICKET_PRICE and
        // ScratcherHelper.SCRATCHER_PRICE (a stack of 5 is 100).
        internal const int LotteryCost = 25;
        internal const int ScratcherCost = 20;

        // Same table as LotteryHelper.GetPrizeForMatches.
        private static readonly Dictionary<int, int> LotteryPrizes = new Dictionary<int, int>
        {
            { 2, 35 }, { 3, 300 }, { 4, 2500 }, { 5, 100000 }, { 6, 1000000 },
        };

        // Upper bound of each band (exclusive) and its reputation loss.
        private static readonly (int below, int loss)[] PenaltyBands =
        {
            (100, 3), (1000, 15), (10000, 250),
        };

        internal static int LotteryPrize(int matches)
        {
            return LotteryPrizes.TryGetValue(matches, out int prize) ? prize : 0;
        }

        // Same as LotteryHelper.CountMatches, duplicates included: a number written twice
        // that is in the draw counts twice. Forgers use that.
        internal static int CountMatches(string numbers, string winning)
        {
            HashSet<int> drawn = new HashSet<int>(ParseNumbers(winning));
            return ParseNumbers(numbers).Count(drawn.Contains);
        }

        internal static int[] ParseNumbers(string numbers)
        {
            return numbers.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => int.Parse(s, NumberStyles.Integer, CultureInfo.InvariantCulture)).ToArray();
        }

        internal static string FormatNumbers(IEnumerable<int> numbers)
        {
            return string.Join(" ", numbers.Select(n => n.ToString("D2", CultureInfo.InvariantCulture)));
        }

        // Same as ScratcherHelper.UpdateValue on a fully scratched ticket: groups are read in
        // first-appearance order and a triple wins outright.
        internal static int ScratchPrize(int[] symbols)
        {
            Dictionary<int, int> counts = new Dictionary<int, int>();
            List<int> order = new List<int>();
            foreach (int s in symbols)
            {
                if (!counts.ContainsKey(s))
                {
                    counts[s] = 0;
                    order.Add(s);
                }
                counts[s]++;
            }
            int prize = 0;
            foreach (int s in order)
            {
                if (counts[s] >= 3)
                    return s == NutrifruitSymbol ? 350 : 75;
                if (counts[s] == 2)
                    prize = s == NutrifruitSymbol ? 30 : 10;
            }
            return prize;
        }

        internal static int TriplePrize(int symbol) { return symbol == NutrifruitSymbol ? 350 : 75; }

        // A scratcher printed at luck 0. ScratcherHelper.RollSymbol gives the favoured symbol
        // 20% of the time and each other one 20% too, so at luck 0 the three symbols are
        // simply independent and uniform. Customers' tickets ignore your luck.
        internal static int[] FairScratch(IRng rng)
        {
            return new[] { rng.Range(1, 5), rng.Range(1, 5), rng.Range(1, 5) };
        }

        internal static Penalty PenaltyFor(int prize)
        {
            foreach ((int below, int loss) in PenaltyBands)
                if (prize < below)
                    return new Penalty { ReputationLoss = loss };
            return new Penalty { Lynch = true };
        }

        // Six distinct numbers in 1..49, exactly k of them in the draw, sorted.
        internal static List<int> NumbersMatching(IRng rng, string winning, int k)
        {
            List<int> drawn = ParseNumbers(winning).ToList();
            List<int> others = Enumerable.Range(1, 49).Where(n => !drawn.Contains(n)).ToList();
            List<int> picked = new List<int>();
            for (int i = 0; i < k; i++)
                picked.Add(TakeRandom(rng, drawn));
            while (picked.Count < 6)
                picked.Add(TakeRandom(rng, others));
            picked.Sort();
            return picked;
        }

        internal static T TakeRandom<T>(IRng rng, List<T> list)
        {
            int index = rng.Range(0, list.Count - 1);
            T value = list[index];
            list.RemoveAt(index);
            return value;
        }

        // Picks by weight from a table, so adding a trick is one more row.
        internal static T Weighted<T>(IRng rng, IList<(int weight, T value)> table)
        {
            int total = table.Sum(row => row.weight);
            int roll = rng.Range(1, total);
            foreach ((int weight, T value) in table)
            {
                roll -= weight;
                if (roll <= 0)
                    return value;
            }
            return table[table.Count - 1].value;
        }
    }

    // Customers win less often than the game's odds, so that selling tickets earns the
    // shop its profit target on average. Bigger prizes are cut harder: each prize tier
    // keeps c * (smallest prize / prize) of its wins, capped at all of them, and c is
    // solved so the average payout per ticket equals what the sale leaves after the
    // board's price and the target. Jackpots (5 and 6 matches) are not in the tables and
    // are never cut. A win that is cut means that customer simply lost.
    internal static class Nerf
    {
        internal sealed class Tiers
        {
            internal readonly (int prize, double chance)[] Rows;
            private readonly Dictionary<long, Dictionary<int, double>> cache = new Dictionary<long, Dictionary<int, double>>();

            internal Tiers((int prize, double chance)[] rows) { Rows = rows; }

            // Average payout per ticket at the game's own odds.
            internal double FairPayout => Rows.Sum(r => r.prize * r.chance);

            // Chance that a win of this prize is kept, for a payout budget per ticket.
            internal double KeepChance(double budget, int prize)
            {
                long key = (long)Math.Round(budget * 1000);
                if (!cache.TryGetValue(key, out Dictionary<int, double> keep))
                    cache[key] = keep = Solve(key / 1000.0);
                return keep.TryGetValue(prize, out double k) ? k : 1.0;
            }

            private Dictionary<int, double> Solve(double budget)
            {
                int smallest = Rows.Min(r => r.prize);
                Func<double, int, double> keepAt = (c, prize) => Math.Min(1.0, c * smallest / prize);
                Func<double, double> payout = c => Rows.Sum(r => r.prize * r.chance * keepAt(c, r.prize));
                double lo = 0, hi = 1;
                if (budget >= FairPayout)
                    lo = Rows.Max(r => r.prize) / (double)smallest;   // nothing is cut
                else
                {
                    while (payout(hi) < budget)
                        hi *= 2;
                    for (int i = 0; i < 60; i++)
                    {
                        double mid = (lo + hi) / 2;
                        if (payout(mid) < budget) lo = mid; else hi = mid;
                    }
                }
                return Rows.ToDictionary(r => r.prize, r => keepAt(lo, r.prize));
            }
        }

        // ScratcherHelper at luck 0: pair 10, Nutrifruit pair 30, triple 75, Nutrifruit triple 350.
        internal static readonly Tiers Scratcher = new Tiers(new[] { (10, 0.384), (30, 0.096), (75, 0.032), (350, 0.008) });

        // 6 of 49: chance of exactly k matches is C(6,k) C(43,6-k) / C(49,6).
        internal static readonly Tiers Lottery = new Tiers(new[]
        {
            (Odds.LotteryPrize(2), MatchChance(2)), (Odds.LotteryPrize(3), MatchChance(3)), (Odds.LotteryPrize(4), MatchChance(4)),
        });

        // What is left per ticket for paying winners once the board's price and the
        // shop's profit (a % of that price) are covered.
        internal static double Budget(int paid, int cost, int profitPercent)
        {
            return Math.Max(0, paid - cost - cost * profitPercent / 100.0);
        }

        internal static bool Keeps(IRng rng, Tiers tiers, double budget, int prize)
        {
            return rng.Range(0, 999999) < tiers.KeepChance(budget, prize) * 1000000;
        }

        private static double MatchChance(int k)
        {
            return Choose(6, k) * Choose(43, 6 - k) / Choose(49, 6);
        }

        private static double Choose(int n, int k)
        {
            double r = 1;
            for (int i = 1; i <= k; i++)
                r = r * (n - k + i) / i;
            return r;
        }
    }

    // Builds the claims. Honest ones come from tickets you sold; forged ones from nothing.
    internal static class ClaimFactory
    {
        // Context for a forgery: today, the latest draw, and when you got your licence.
        internal sealed class Context
        {
            internal int Today;
            internal int LicenseDay;
            internal int DrawEdition;   // 0 = no draw seen since licensing
            internal string DrawNumbers = "";
            internal int DrawDay;
        }

        private delegate void Trick(IRng rng, Context ctx, Claim claim);

        // A trick and the name it goes by (Claim.Flaw), so one can be asked for by name.
        private sealed class NamedTrick
        {
            internal readonly string Flaw;
            private readonly Trick apply;
            internal NamedTrick(string flaw, Trick apply) { Flaw = flaw; this.apply = apply; }
            internal void Apply(IRng rng, Context ctx, Claim claim) { apply(rng, ctx, claim); }
        }

        // Lottery tricks. Every one leaves a clue on the ticket or in the ask. Adding a
        // trick is one more row.
        private static readonly (int weight, NamedTrick trick)[] LotteryTricks =
        {
            // A real winner asking for the next prize up: count the green numbers.
            (90, new NamedTrick("over", (rng, ctx, c) =>
            {
                int k = rng.Range(2, 3);
                c.Data = Odds.FormatNumbers(Odds.NumbersMatching(rng, ctx.DrawNumbers, k));
                c.Asked = Odds.LotteryPrize(k + 1);
                c.StampDay = StampInDrawWeek(rng, ctx);
            })),
            // Last week's ticket coloured against this week's draw: the edition is old.
            (75, new NamedTrick("edition", (rng, ctx, c) =>
            {
                int k = ForgedMatches(rng);
                c.Forged = true;
                c.Edition = ctx.DrawEdition - 1;
                c.Data = Odds.FormatNumbers(Odds.NumbersMatching(rng, ctx.DrawNumbers, k));
                c.Asked = Odds.LotteryPrize(k);
                c.StampDay = ctx.DrawDay - 7 - rng.Range(0, 5);
            })),
            // Numbers written over: out of order, above 49, or one written twice.
            (25, new NamedTrick("unsorted", Doctored((rng, ctx, c, numbers, k) =>
            {
                int i = rng.Range(0, numbers.Count - 2);
                int swap = numbers[i];
                numbers[i] = numbers[i + 1];
                numbers[i + 1] = swap;
                c.Asked = Odds.LotteryPrize(k);
            }))),
            (25, new NamedTrick("over49", Doctored((rng, ctx, c, numbers, k) =>
            {
                HashSet<int> drawn = new HashSet<int>(Odds.ParseNumbers(ctx.DrawNumbers));
                List<int> misses = Enumerable.Range(0, numbers.Count).Where(i => !drawn.Contains(numbers[i])).ToList();
                numbers[misses[rng.Range(0, misses.Count - 1)]] = rng.Range(50, 59);
                numbers.Sort();
                c.Asked = Odds.LotteryPrize(k);
            }))),
            (25, new NamedTrick("duplicate", Doctored((rng, ctx, c, numbers, k) =>
            {
                // One drawn number copied over a miss: the game counts it twice.
                HashSet<int> drawn = new HashSet<int>(Odds.ParseNumbers(ctx.DrawNumbers));
                List<int> hits = numbers.Where(drawn.Contains).ToList();
                List<int> misses = Enumerable.Range(0, numbers.Count).Where(i => !drawn.Contains(numbers[i])).ToList();
                numbers[misses[rng.Range(0, misses.Count - 1)]] = hits[rng.Range(0, hits.Count - 1)];
                numbers.Sort();
                c.Asked = Odds.LotteryPrize(k + 1);
            }))),
            // A clean-looking winner that never came through a licensed retailer.
            (60, new NamedTrick("nostamp", (rng, ctx, c) =>
            {
                int k = ForgedMatches(rng);
                c.Forged = true;
                c.Data = Odds.FormatNumbers(Odds.NumbersMatching(rng, ctx.DrawNumbers, k));
                c.Asked = Odds.LotteryPrize(k);
                c.StampDay = -1;
            })),
        };

        private delegate void Doctoring(IRng rng, Context ctx, Claim claim, List<int> numbers, int k);

        // A real ticket's numbers written over. Two or three real matches, so the duplicate
        // trick can only reach four.
        private static Trick Doctored(Doctoring doctor)
        {
            return (rng, ctx, c) =>
            {
                c.Forged = true;
                c.StampDay = StampInDrawWeek(rng, ctx);
                int k = rng.Range(2, 3);
                List<int> numbers = Odds.NumbersMatching(rng, ctx.DrawNumbers, k);
                doctor(rng, ctx, c, numbers, k);
                c.Data = Odds.FormatNumbers(numbers);
            };
        }

        // Scratcher tricks.
        private static readonly (int weight, NamedTrick trick)[] ScratcherTricks =
        {
            // A real pair asking for the three-of-a-kind prize: open it and look.
            (35, new NamedTrick("over", (rng, ctx, c) =>
            {
                int s = PickSymbol(rng);
                c.Data = Pair(rng, s);
                c.Asked = Odds.TriplePrize(s);
                c.StampDay = Math.Max(ctx.LicenseDay, ctx.Today - rng.Range(1, 4));
            })),
            // A third symbol cut from another ticket and glued on: it sits crooked.
            (40, new NamedTrick("reglued", (rng, ctx, c) =>
            {
                int s = PickSymbol(rng);
                c.Forged = true;
                c.Data = s + "," + s + "," + s;
                c.GlueSlot = rng.Range(1, 3);
                c.Asked = Odds.TriplePrize(s);
                c.StampDay = Math.Max(ctx.LicenseDay, ctx.Today - rng.Range(1, 4));
            })),
            // Printed off the books.
            (25, new NamedTrick("nostamp", (rng, ctx, c) =>
            {
                int s = PickSymbol(rng);
                c.Forged = true;
                c.Data = rng.Range(0, 1) == 0 ? Pair(rng, s) : s + "," + s + "," + s;
                c.Asked = Odds.ScratchPrize(c.Symbols());
                c.StampDay = -1;
            })),
        };

        // Every trick, lottery first, in table order.
        internal static IEnumerable<(char kind, string flaw)> AllTricks =>
            LotteryTricks.Select(r => (Claim.Lottery, r.trick.Flaw))
                .Concat(ScratcherTricks.Select(r => (Claim.Scratcher, r.trick.Flaw)));

        internal static Claim HonestLottery(int id, LedgerEntry entry, string winning, int matches, int dueDay)
        {
            return new Claim
            {
                Id = id, Kind = Claim.Lottery, Honest = true,
                Data = entry.Numbers, Edition = entry.Edition, Winning = winning,
                Asked = Odds.LotteryPrize(matches), StampDay = entry.SoldDay, DueDay = dueDay,
            };
        }

        internal static Claim HonestScratcher(int id, int[] symbols, int soldDay, int dueDay)
        {
            return new Claim
            {
                Id = id, Kind = Claim.Scratcher, Honest = true,
                Data = string.Join(",", symbols), Asked = Odds.ScratchPrize(symbols),
                StampDay = soldDay, DueDay = dueDay,
            };
        }

        // A dishonest claimant for today. Lottery tricks need a draw to colour against.
        // kind: Claim.Lottery or Claim.Scratcher to choose one, or AnyKind for either.
        internal const char AnyKind = '?';

        internal static Claim Forgery(IRng rng, int id, Context ctx, char kind = AnyKind)
        {
            bool lottery = ctx.DrawEdition > 0 && (kind == Claim.Lottery || (kind == AnyKind && rng.Range(0, 1) == 0));
            return Build(rng, id, ctx, lottery, Odds.Weighted(rng, lottery ? LotteryTricks : ScratcherTricks));
        }

        // One trick by name. Null for an unknown name, or a lottery trick with no draw yet.
        internal static Claim ForgeryOf(IRng rng, int id, Context ctx, char kind, string flaw)
        {
            bool lottery = kind == Claim.Lottery;
            if (lottery && ctx.DrawEdition <= 0)
                return null;
            NamedTrick trick = (lottery ? LotteryTricks : ScratcherTricks).Select(r => r.trick).FirstOrDefault(t => t.Flaw == flaw);
            return trick == null ? null : Build(rng, id, ctx, lottery, trick);
        }

        private static Claim Build(IRng rng, int id, Context ctx, bool lottery, NamedTrick trick)
        {
            Claim c = new Claim { Id = id, DueDay = ctx.Today, Honest = false, Flaw = trick.Flaw };
            if (lottery)
            {
                c.Kind = Claim.Lottery;
                c.Edition = ctx.DrawEdition;
                c.Winning = ctx.DrawNumbers;
            }
            else
                c.Kind = Claim.Scratcher;
            trick.Apply(rng, ctx, c);
            return c;
        }

        // Mostly two matches, sometimes three, rarely four.
        private static int ForgedMatches(IRng rng)
        {
            int roll = rng.Range(1, 100);
            return roll <= 60 ? 2 : roll <= 95 ? 3 : 4;
        }

        private static int StampInDrawWeek(IRng rng, Context ctx)
        {
            return Math.Max(ctx.LicenseDay, ctx.DrawDay - rng.Range(0, 5));
        }

        // The Nutrifruit (big prize) a quarter of the time.
        private static int PickSymbol(IRng rng)
        {
            return rng.Range(1, 4) == 1 ? Odds.NutrifruitSymbol : rng.Range(2, 5);
        }

        // Two of s and one other symbol, in a random order.
        private static string Pair(IRng rng, int s)
        {
            int other = rng.Range(1, 4);
            if (other >= s)
                other++;
            int[] symbols = { s, s, s };
            symbols[rng.Range(0, 2)] = other;
            return string.Join(",", symbols);
        }
    }
}
