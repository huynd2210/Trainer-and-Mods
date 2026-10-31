using System;
using birdseye.Localization;
using birdseye.Regiments.Aircraft;
using birdseye.Regiments.Commanders;
using birdseye.Regiments.Platoons;

namespace KillTracker
{
    /// <summary>Display names, as the game's own post-battle report writes them.</summary>
    internal static class Names
    {
        // Killer refs are compared by reference, not Unity's ==: a platoon destroyed after it
        // fired compares equal to null under Unity's overload, but its callsign is still readable.
        public static bool Exists(object o)
        {
            return !ReferenceEquals(o, null);
        }

        public static string Label(PlatoonCore p, string type)
        {
            return string.IsNullOrEmpty(p.callsign) ? type : type + " [" + p.callsign + "]";
        }

        public static string Type(PlatoonCore p)
        {
            return Safe(() => p.parameters.displayName, "unit");
        }

        // A mechanised platoon's name is its vehicle; its soldiers are named after their squad.
        public static string InfantryType(PlatoonCore p)
        {
            return Safe(() => p.parameters.infDisplayName, Type(p));
        }

        public static string AircraftType(AirUnitCore a)
        {
            return Safe(() => LocalizationManager.GetString(a.airUnitParameters.description.GetGameLongLocIDs()), "aircraft");
        }

        public static string Commander(Commander c)
        {
            return Safe(() => c.displayName, "you");
        }

        public static string Killer(PlatoonCore killer, Commander cmdr)
        {
            if (Exists(killer))
            {
                return Label(killer, Type(killer));
            }
            return Exists(cmdr) ? TacStrike() : null;
        }

        // Artillery, air strikes and other off-map support: a commander but no firing platoon.
        private static string TacStrike()
        {
            string s = Safe(() => LocalizationManager.GetString("LS_PostBattle_TacStrike"), null);
            return string.IsNullOrEmpty(s) || s.StartsWith("LS_", StringComparison.Ordinal) ? "Tac strike" : StripTags(s);
        }

        private static string StripTags(string s)
        {
            return System.Text.RegularExpressions.Regex.Replace(s, "<[^>]*>", "").Trim();
        }

        private static string Safe(Func<string> get, string fallback)
        {
            try
            {
                string s = get();
                return string.IsNullOrEmpty(s) ? fallback : StripTags(s);
            }
            catch (Exception)
            {
                return fallback;
            }
        }
    }
}
