using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace LotteryForesight
{
    public class LotteryForesightMod : IMod
    {
        public void Init(ModManifest manifest) { }

        public void OnEnable()
        {
            ModHook.OnCreateTooltipLate += ForesightTooltip.Add;
        }

        public void OnDisable()
        {
            ModHook.OnCreateTooltipLate -= ForesightTooltip.Add;
        }
    }

    // The game draws the weekly numbers at random on draw night, so nothing exists to read
    // in advance. Instead each edition's numbers are fixed ahead of time from the store's
    // name and the edition number, and the real draw uses them. The same save always gets
    // the same numbers, through saves and reloads.
    internal static class Forecast
    {
        private static readonly Dictionary<string, string> cache = new Dictionary<string, string>();

        internal static string For(int edition)
        {
            string key = (PlayerStore.Instance.storeName ?? "") + "#" + edition;
            if (cache.TryGetValue(key, out string numbers))
                return numbers;

            System.Random rng = new System.Random(Fnv1a(key));
            List<int> pool = Enumerable.Range(1, 49).ToList();
            List<int> picked = new List<int>();
            for (int i = 0; i < 6; i++)
            {
                int index = rng.Next(pool.Count);
                picked.Add(pool[index]);
                pool.RemoveAt(index);
            }
            // Same format as LotteryHelper.GenerateLotteryTicket: sorted, two digits, spaced.
            numbers = string.Join(" ", picked.OrderBy(n => n).Select(n => n.ToString("D2")));
            cache[key] = numbers;
            return numbers;
        }

        // string.GetHashCode is not guaranteed stable between runs; this is.
        private static int Fnv1a(string text)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char c in text)
                {
                    hash ^= c;
                    hash *= 16777619;
                }
                return (int)hash;
            }
        }
    }

    // Draw night: use the foreseen numbers. This replaces the game's luck re-draws, which
    // pick between random draws by how much your tickets would win.
    [HarmonyPatch(typeof(LotteryHelper), nameof(LotteryHelper.DrawWinningNumberWithLuck))]
    internal static class DrawPatch
    {
        private static bool Prefix(ref string __result)
        {
            __result = Forecast.For(LotteryHelper.GetCurrentEdition());
            return false;
        }
    }
}
