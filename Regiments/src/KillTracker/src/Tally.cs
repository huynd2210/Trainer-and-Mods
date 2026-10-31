using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KillTracker
{
    /// <summary>Counts for one side of the ledger (your kills, or your losses).</summary>
    internal sealed class Tally
    {
        private static readonly string[] Singular = { "vehicle", "helicopter", "aircraft", "soldier" };
        private static readonly string[] Plural = { "vehicles", "helicopters", "aircraft", "soldiers" };

        private readonly int[] totals = new int[4];
        private readonly Dictionary<string, int> byType = new Dictionary<string, int>();
        private readonly Dictionary<string, int[]> byPlatoon = new Dictionary<string, int[]>();
        public int platoonsWiped;

        public bool IsEmpty => totals.All(n => n == 0) && platoonsWiped == 0;

        public void Add(Kind kind, string type, string platoon)
        {
            totals[(int)kind]++;
            string typeKey = kind == Kind.Soldier ? "soldiers  " + type : type;
            byType.TryGetValue(typeKey, out int n);
            byType[typeKey] = n + 1;
            if (platoon == null)
            {
                return;
            }
            if (!byPlatoon.TryGetValue(platoon, out int[] counts))
            {
                counts = new int[4];
                byPlatoon[platoon] = counts;
            }
            counts[(int)kind]++;
        }

        public static string Count(Kind kind, int n)
        {
            return n + " " + (n == 1 ? Singular[(int)kind] : Plural[(int)kind]);
        }

        private static string Counts(int[] counts)
        {
            var parts = new List<string>();
            for (int k = 0; k < counts.Length; k++)
            {
                if (counts[k] > 0)
                {
                    parts.Add(Count((Kind)k, counts[k]));
                }
            }
            return parts.Count == 0 ? "none" : string.Join(", ", parts);
        }

        public void Write(StringBuilder sb, string title, string wipedWord, string platoonHeading)
        {
            sb.Append(title).Append(Counts(totals));
            if (platoonsWiped > 0)
            {
                sb.Append("  |  ").Append(platoonsWiped).Append(platoonsWiped == 1 ? " platoon " : " platoons ").Append(wipedWord);
            }
            sb.AppendLine();
            if (byType.Count > 0)
            {
                sb.AppendLine("  By type:");
                foreach (var e in byType.OrderByDescending(e => e.Value).ThenBy(e => e.Key))
                {
                    sb.Append("    ").Append(e.Value.ToString().PadLeft(4)).Append("  ").AppendLine(e.Key);
                }
            }
            if (byPlatoon.Count > 0)
            {
                sb.AppendLine("  " + platoonHeading + ":");
                int width = byPlatoon.Keys.Max(k => k.Length);
                foreach (var e in byPlatoon.OrderByDescending(e => e.Value.Sum()).ThenBy(e => e.Key))
                {
                    sb.Append("    ").Append(e.Key.PadRight(width)).Append("  ").AppendLine(Counts(e.Value));
                }
            }
        }
    }
}
