using System.Collections.Generic;

namespace LonestarTracker
{
    internal sealed class NameCount
    {
        public string Name = "";
        public string Detail = "";
        public int Count;
    }

    /// <summary>
    /// Everything the overview shows, folded out of the logs in one pass. Rebuilt only
    /// when a row is added, so drawing the panel never walks the whole history.
    /// </summary>
    internal sealed class Stats
    {
        public int Kills;
        public int KillsMinion;
        public int KillsElite;
        public int KillsBoss;
        public int Defeats;

        public int RunsWon;
        public int RunsLost;
        public int RunsIncomplete;
        public int RunsTotal;

        public int BestKillStreak;
        public int CurrentKillStreak;
        public int MostKillsInRun;
        public int TrackedSeconds;
        public int FastestWinSeconds = -1;

        public readonly List<NameCount> MostKilled = new List<NameCount>();
        public readonly List<NameCount> Nemesis = new List<NameCount>();

        public int Battles { get { return Kills + Defeats; } }

        public int WinRatePercent
        {
            get { return Battles == 0 ? 0 : Mathf_RoundToInt(100f * Kills / Battles); }
        }

        public int RunWinRatePercent
        {
            get
            {
                int decided = RunsWon + RunsLost;
                return decided == 0 ? 0 : Mathf_RoundToInt(100f * RunsWon / decided);
            }
        }

        private static int Mathf_RoundToInt(float v) { return (int)(v + 0.5f); }

        public static Stats Build(TrackerStore store)
        {
            Stats s = new Stats();
            if (store == null) return s;

            Dictionary<string, NameCount> killed = new Dictionary<string, NameCount>();
            Dictionary<string, NameCount> nemesis = new Dictionary<string, NameCount>();
            Dictionary<string, int> killsPerRun = new Dictionary<string, int>();

            int streak = 0;
            foreach (BattleRecord battle in store.Battles)
            {
                if (battle.IsKill)
                {
                    s.Kills++;
                    if (battle.Tier == Tier.Boss) s.KillsBoss++;
                    else if (battle.Tier == Tier.Elite) s.KillsElite++;
                    else s.KillsMinion++;

                    streak++;
                    if (streak > s.BestKillStreak) s.BestKillStreak = streak;

                    Bump(killed, battle.EnemyPilot, battle.EnemyShip);

                    if (!string.IsNullOrEmpty(battle.RunId))
                    {
                        int n;
                        killsPerRun.TryGetValue(battle.RunId, out n);
                        killsPerRun[battle.RunId] = n + 1;
                        if (n + 1 > s.MostKillsInRun) s.MostKillsInRun = n + 1;
                    }
                }
                else
                {
                    s.Defeats++;
                    streak = 0;
                    Bump(nemesis, battle.EnemyPilot, battle.EnemyShip);
                }
            }
            s.CurrentKillStreak = streak;

            foreach (RunRecord run in store.Runs)
            {
                s.RunsTotal++;
                if (run.IsWin) s.RunsWon++;
                else if (run.IsLoss) s.RunsLost++;
                else s.RunsIncomplete++;

                s.TrackedSeconds += run.RunSeconds;
                if (run.IsWin && run.RunSeconds > 0 && (s.FastestWinSeconds < 0 || run.RunSeconds < s.FastestWinSeconds))
                    s.FastestWinSeconds = run.RunSeconds;
            }

            TopInto(killed, s.MostKilled, 5);
            TopInto(nemesis, s.Nemesis, 5);
            return s;
        }

        private static void Bump(Dictionary<string, NameCount> into, string name, string detail)
        {
            string key = name + "" + detail;
            NameCount entry;
            if (!into.TryGetValue(key, out entry))
            {
                entry = new NameCount { Name = string.IsNullOrEmpty(name) ? "?" : name, Detail = detail };
                into[key] = entry;
            }
            entry.Count++;
        }

        private static void TopInto(Dictionary<string, NameCount> from, List<NameCount> into, int count)
        {
            List<NameCount> all = new List<NameCount>(from.Values);
            all.Sort((a, b) =>
            {
                int byCount = b.Count.CompareTo(a.Count);
                return byCount != 0 ? byCount : string.CompareOrdinal(a.Name, b.Name);
            });
            for (int i = 0; i < all.Count && i < count; i++) into.Add(all[i]);
        }
    }
}
