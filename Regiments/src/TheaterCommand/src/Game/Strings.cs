using System.Collections.Generic;
using birdseye.Localization;
using HarmonyLib;

namespace TheaterCommand.Game
{
    /// <summary>
    /// Text for the game's own localized widgets (dropdowns, labels) that the mod reuses. The game
    /// resolves string IDs through LocalizationManager; IDs starting with "TC_" are answered here.
    /// </summary>
    public static class Strings
    {
        public const string Prefix = "TC_";

        private static readonly Dictionary<string, string> table = new Dictionary<string, string>
        {
            { "TC_Mode", "Mode" },
            { "TC_Mode_WarPath", "War Path" },
            { "TC_Mode_Theater", "Theater Command" },
            { "TC_Turns", "{0} turns" },
            { "TC_Back to map", "Back to Map" },
        };

        public static string Get(string id)
        {
            return table.TryGetValue(id, out string s) ? s : id.Substring(Prefix.Length);
        }

        [HarmonyPatch(typeof(LocalizationManager), "ParseString")]
        private static class ParseStringPatch
        {
            private static bool Prefix(string item, ref string __result)
            {
                if (item == null || !item.StartsWith(Strings.Prefix))
                {
                    return true;
                }
                __result = Get(item);
                return false;
            }
        }
    }
}
