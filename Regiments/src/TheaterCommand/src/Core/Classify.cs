using System.Collections.Generic;

namespace TheaterCommand.Core
{
    /// <summary>
    /// Reads a formation's branch and size from its name, for the NATO symbol. The first rule
    /// whose keyword appears in the name wins; anything unrecognised is mechanised and a regiment.
    /// </summary>
    public static class Classify
    {
        private static readonly KeyValuePair<string, UnitKind>[] KindWords =
        {
            Rule("recon", UnitKind.Recon), Rule("reconn", UnitKind.Recon), Rule("aufkl", UnitKind.Recon), Rule("cavalry", UnitKind.Recon), Rule("cheval", UnitKind.Recon), Rule("cavalerie", UnitKind.Recon), Rule("razved", UnitKind.Recon),
            Rule("airborne", UnitKind.Airborne), Rule("air assault", UnitKind.Airborne), Rule("fallschirm", UnitKind.Airborne), Rule("luftlande", UnitKind.Airborne), Rule("parachute", UnitKind.Airborne), Rule("desant", UnitKind.Airborne), Rule("vdv", UnitKind.Airborne),
            Rule("panzergrenadier", UnitKind.Mechanized), Rule("motor", UnitKind.Mechanized), Rule("mech", UnitKind.Mechanized), Rule("grenadier", UnitKind.Mechanized),
            Rule("tank", UnitKind.Armour), Rule("panzer", UnitKind.Armour), Rule("armor", UnitKind.Armour), Rule("armour", UnitKind.Armour), Rule("cuirass", UnitKind.Armour),
            Rule("infantry", UnitKind.Infantry), Rule("jäger", UnitKind.Infantry), Rule("jager", UnitKind.Infantry), Rule("rifle", UnitKind.Infantry),
        };

        private static readonly KeyValuePair<string, UnitSize>[] SizeWords =
        {
            Rule("division", UnitSize.Division), Rule("brigade", UnitSize.Brigade), Rule("regiment", UnitSize.Regiment),
            Rule("battalion", UnitSize.Battalion), Rule("tf ", UnitSize.Battalion), Rule("task force", UnitSize.Battalion), Rule("bataillon", UnitSize.Battalion), Rule("battlegroup", UnitSize.Regiment),
        };

        // The size mark drawn above the symbol.
        public static readonly Dictionary<UnitSize, string> SizeMark = new Dictionary<UnitSize, string>
        {
            { UnitSize.Battalion, "II" },
            { UnitSize.Regiment, "III" },
            { UnitSize.Brigade, "X" },
            { UnitSize.Division, "XX" },
        };

        public static UnitKind Kind(string name) => Find(KindWords, name, UnitKind.Mechanized);

        // "1st Brigade / 3rd Armored Division" is a brigade: the size is read from the part before
        // the slash, the branch from the whole name.
        public static UnitSize Size(string name)
        {
            string own = (name ?? "").Split('/')[0];
            return Find(SizeWords, own, Find(SizeWords, name, UnitSize.Regiment));
        }

        // Battlegroup names say nothing of their branch; the Pact's are mostly motor rifle with
        // a tank regiment in every three, as its divisions were organised.
        public static UnitKind PactKind(int index) => index % 3 == 1 ? UnitKind.Armour : UnitKind.Mechanized;

        // Battle maps named after a place, not a feature ("Fambach", not "Runway"), are used as
        // town names on the theater map.
        private static readonly string[] PlaceEndings =
        {
            "bach", "berg", "burg", "dorf", "feld", "hausen", "heim", "kirche", "leben", "tal", "thal", "taft", "jossa", "merz", "au", "stadt", "hof", "rode", "born", "brunn", "ingen",
        };

        public static bool IsPlaceName(string name)
        {
            string n = (name ?? "").Trim();
            if (n.Length < 4 || n.Contains(" ") || n.Contains("'") || !char.IsUpper(n[0]))
            {
                return false;
            }
            string lower = n.ToLowerInvariant();
            foreach (string e in PlaceEndings)
            {
                if (lower.EndsWith(e))
                {
                    return true;
                }
            }
            return false;
        }

        private static T Find<T>(KeyValuePair<string, T>[] rules, string name, T fallback)
        {
            string n = (name ?? "").ToLowerInvariant();
            foreach (KeyValuePair<string, T> r in rules)
            {
                if (n.Contains(r.Key))
                {
                    return r.Value;
                }
            }
            return fallback;
        }

        private static KeyValuePair<string, T> Rule<T>(string word, T value) => new KeyValuePair<string, T>(word, value);
    }
}
